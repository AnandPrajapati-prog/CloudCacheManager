using CloudCacheManager.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using System.Text;

namespace CloudCacheManager.Services;

public class FileManagerService
{
    private readonly AppSettings _settings;
    private readonly ILogger<FileManagerService> _logger;
    private readonly FileValidationService _validator;

    // Resolved once from appsettings — if appsettings changes, restart reflects it
    public string BasePath => _settings.BasePath;
    private BackupSettings BackupCfg => _settings.Backup;
            
    public FileManagerService(IOptions<AppSettings> settings, ILogger<FileManagerService> logger, FileValidationService validator)
    {
        _settings = settings.Value;
        _logger = logger;
        _validator = validator;

    }

    // ── Directory tree ────────────────────────────────────────────────────────

    public FolderNode GetDirectoryTree(string? relativePath = null)
    {
        var fullPath = relativePath == null
            ? BasePath
            : Path.Combine(BasePath, relativePath);

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");

        return BuildNode(fullPath);
    }

    private FolderNode BuildNode(string fullPath)
    {
        var node = new FolderNode
        {
            Name = string.IsNullOrEmpty(Path.GetFileName(fullPath))
                ? Path.GetFileName(BasePath.TrimEnd('\\', '/'))
                : Path.GetFileName(fullPath),
            Path = Path.GetRelativePath(BasePath, fullPath).Replace("\\", "/"),
        };

        try
        {
            // Exclude the UserBackup root folder from the tree so it's not shown to users
            var userBackupRoot = Path.Combine(BasePath, BackupCfg.UserBackupFolderName);

            node.Files = Directory.GetFiles(fullPath)
                .Select(f => new FileItem
                {
                    Name = Path.GetFileName(f),
                    Path = Path.GetRelativePath(BasePath, f).Replace("\\", "/"),
                    Size = new FileInfo(f).Length,
                    LastModified = File.GetLastWriteTime(f),
                    IsJson = Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase)
                }).ToList();
            node.HasFiles = node.Files.Count > 0;

            node.SubFolders = Directory.GetDirectories(fullPath)
                .Where(d => !d.Equals(userBackupRoot, StringComparison.OrdinalIgnoreCase)
                         && !Path.GetFileName(d).Equals(BackupCfg.LocalFolderName, StringComparison.OrdinalIgnoreCase))
                .Select(d => BuildNode(d))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not fully read directory {Dir}: {Msg}", fullPath, ex.Message);
        }

        return node;
    }

    // ── Read / Download ───────────────────────────────────────────────────────

    public byte[] DownloadFile(string relativePath)
    {
        var fullPath = GetSafePath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}");
        return File.ReadAllBytes(fullPath);
    }

    public string ReadFileContent(string relativePath)
    {
        var fullPath = GetSafePath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}");
        return File.ReadAllText(fullPath);
    }

    // ── Edit ─────────────────────────────────────────────────────────────────

    public BackupResult EditFile(string relativePath, string content, string username, string displayName)
    {
        // Validate before anything else
        var result = _validator.Validate(relativePath, content);
        if (!result.IsValid)
            throw new InvalidDataException(result.Error);

        var fullPath = GetSafePath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}");

        var ext = Path.GetExtension(fullPath);
        var originalContent = File.ReadAllText(fullPath);

        // Validate content based on file type
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            try { JToken.Parse(content); }
            catch { throw new InvalidDataException("Invalid JSON content"); }
        }
        else if (ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            try { XDocument.Parse(content); }
            catch { throw new InvalidDataException("Invalid XML content"); }
        }

        // Create local backup BEFORE changes (original content) ← BACK HERE
        CreateLocalBackup(fullPath);
        
        // Save the NEW content to main file
        File.WriteAllText(fullPath, content);

        // Create user backup AFTER changes (new version with changes)
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            username = displayName + "_" + username;
        }
        var userBackupPath = CreateUserBackup(fullPath, username);
        
        // Save changes-only file
        SaveChangesOnly(fullPath, originalContent, content, username, ext);
        
        _logger.LogInformation("File edited: {Path} | User backup: {U}", fullPath, userBackupPath);
        
        return new BackupResult
        {
            LocalBackupPath = GetLocalBackupPath(fullPath),
            UserBackupPath = userBackupPath
        };
    }

    // ── Add key/value ─────────────────────────────────────────────────────────

    public BackupResult AddKeyValue(string relativePath, string key, string value, string username, string displayName)
    {

        var fullPath = GetSafePath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}");

        var ext = Path.GetExtension(fullPath);
        var originalContent = File.ReadAllText(fullPath);
        
        // Create local backup BEFORE changes (original content) ← BACK HERE
        CreateLocalBackup(fullPath);

        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            JObject json;
            try { json = JObject.Parse(originalContent); }
            catch { throw new InvalidDataException("File is not valid JSON."); }

            json[key] = value;
            var newContent = json.ToString(Formatting.Indented);
            
            // Save NEW content
            File.WriteAllText(fullPath, newContent);

            // Create user backup AFTER changes
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                username = displayName + "_" + username;
            }
            var userBackupPath = CreateUserBackup(fullPath, username);
            
            // Save changes-only file
            SaveChangesOnly(fullPath, originalContent, newContent, username, ext);
            
            _logger.LogInformation("Key '{Key}' added to {Path} | User backup: {U}", key, fullPath, userBackupPath);
            
            return new BackupResult
            {
                LocalBackupPath = GetLocalBackupPath(fullPath),
                UserBackupPath = userBackupPath
            };
        }
        else if (ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            var doc = XDocument.Parse(originalContent);
            var root = doc.Root ?? throw new InvalidDataException("XML file has no root element.");
            
            // Find or create the <add> section
            var addSection = root.Elements("add")
                .FirstOrDefault(e => e.Element("key")?.Value == key);

            if (addSection != null)
            {
                // Update existing key
                var valueElement = addSection.Element("value");
                if (valueElement != null)
                    valueElement.Value = value;
                else
                    addSection.Add(new XElement("value", value));
            }
            else
            {
                // Add new key-value pair
                root.Add(new XElement("add",
                    new XElement("key", key),
                    new XElement("value", value)
                ));
            }

            // Save NEW content
            doc.Save(fullPath);
            var newContent = File.ReadAllText(fullPath);

            // Create user backup AFTER changes
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                username += "_" + displayName;
            }
            var userBackupPath = CreateUserBackup(fullPath, username);
            
            // Save changes-only file
            SaveChangesOnly(fullPath, originalContent, newContent, username, ext);
            
            _logger.LogInformation("Key '{Key}' added to {Path} | User backup: {U}", key, fullPath, userBackupPath);
            
            return new BackupResult
            {
                LocalBackupPath = GetLocalBackupPath(fullPath),
                UserBackupPath = userBackupPath
            };
        }
        else
        {
            throw new InvalidDataException("Only JSON and XML files are supported for key-value operations.");
        }
    }

    // ── Save Changes Only ─────────────────────────────────────────────────────

    /// <summary>
    /// Saves only the changed data in a separate file in UserBackup folder
    /// </summary>
    private void SaveChangesOnly(string fullPath, string originalContent, string newContent, string username, string extension)
    {
        try
        {
            var changesContent = new StringBuilder();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                var originalDoc = XDocument.Parse(originalContent);
                var newDoc = XDocument.Parse(newContent);
                var originalRecords = originalDoc.Descendants("Table1").ToList();
                var newRecords = newDoc.Descendants("Table1").ToList();

                var originalDict = new Dictionary<string, XElement>();
                foreach (var record in originalRecords)
                {
                    var id = record.Element("AirportCode")?.Value 
                        ?? record.Element("CityCode")?.Value 
                        ?? record.Element("key")?.Value 
                        ?? Guid.NewGuid().ToString();
                    originalDict[id] = record;
                }

                var processedIds = new HashSet<string>();
                var changesDoc = new XDocument(new XElement("Changes"));

                // Check new/modified records
                foreach (var newRecord in newRecords)
                {
                    var newId = newRecord.Element("AirportCode")?.Value 
                        ?? newRecord.Element("CityCode")?.Value 
                        ?? newRecord.Element("key")?.Value 
                        ?? Guid.NewGuid().ToString();
                    
                    if (originalDict.TryGetValue(newId, out var matchingOriginal))
                    {
                        processedIds.Add(newId);
                        var changes = CompareXmlElements(matchingOriginal, newRecord);
                        
                        if (changes.Count > 0)
                        {
                            var changeElement = new XElement("Change",
                                new XAttribute("Action", "UPDATE"),
                                new XAttribute("RecordId", newId),
                                new XAttribute("Timestamp", timestamp)
                            );
                            
                            foreach (var change in changes)
                            {
                                changeElement.Add(new XElement("Field",
                                    new XAttribute("Name", change.FieldName),
                                    new XElement("OldValue", change.OldValue ?? ""),
                                    new XElement("NewValue", change.NewValue ?? "")
                                ));
                            }
                            
                            changesDoc.Root!.Add(changeElement);
                        }
                    }
                    else
                    {
                        // New record - save the entire record
                        var changeElement = new XElement("Change",
                            new XAttribute("Action", "ADD"),
                            new XAttribute("RecordId", newId),
                            new XAttribute("Timestamp", timestamp),
                            new XElement("NewRecord", newRecord)
                        );
                        changesDoc.Root!.Add(changeElement);
                    }
                }

                // Check deleted records
                foreach (var kvp in originalDict)
                {
                    if (!processedIds.Contains(kvp.Key))
                    {
                        var changeElement = new XElement("Change",
                            new XAttribute("Action", "DELETE"),
                            new XAttribute("RecordId", kvp.Key),
                            new XAttribute("Timestamp", timestamp),
                            new XElement("DeletedRecord", kvp.Value)
                        );
                        changesDoc.Root!.Add(changeElement);
                    }
                }

                if (changesDoc.Root!.HasElements)
                {
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fullPath);
                    var timestampStr = DateTime.Now.ToString(BackupCfg.DateTimeFormat);
                    var userBackupRoot = Path.Combine(BasePath, BackupCfg.UserBackupFolderName);
                    var userBackupDir = Path.Combine(userBackupRoot, username);
                    Directory.CreateDirectory(userBackupDir);

                    var changesFileName = $"{fileNameWithoutExt}_{timestampStr}_{username}_CHANGES.xml";
                    var changesFilePath = Path.Combine(userBackupDir, changesFileName);
                    changesDoc.Save(changesFilePath);
                }
            }
            else if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                var originalObj = JObject.Parse(originalContent);
                var newObj = JObject.Parse(newContent);

                var allKeys = originalObj.Properties().Select(p => p.Name)
                    .Union(newObj.Properties().Select(p => p.Name))
                    .Distinct();

                changesContent.AppendLine($"[{timestamp}] JSON Changes:");

                foreach (var key in allKeys)
                {
                    var oldValue = originalObj[key]?.ToString();
                    var newValue = newObj[key]?.ToString();

                    if (oldValue != newValue)
                    {
                        if (oldValue == null)
                            changesContent.AppendLine($"  + {key}: {newValue}");
                        else if (newValue == null)
                            changesContent.AppendLine($"  - {key}: {oldValue}");
                        else
                            changesContent.AppendLine($"  ~ {key}: {oldValue} → {newValue}");
                    }
                }

                if (changesContent.Length > 0)
                {
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fullPath);
                    var timestampStr = DateTime.Now.ToString(BackupCfg.DateTimeFormat);
                    var userBackupRoot = Path.Combine(BasePath, BackupCfg.UserBackupFolderName);
                    var userBackupDir = Path.Combine(userBackupRoot, username);
                    Directory.CreateDirectory(userBackupDir);

                    var changesFileName = $"{fileNameWithoutExt}_{timestampStr}_{username}_CHANGES.txt";
                    var changesFilePath = Path.Combine(userBackupDir, changesFileName);
                    File.WriteAllText(changesFilePath, changesContent.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not save changes file: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Compares two XML elements and returns changed fields
    /// </summary>
    private List<FieldChange> CompareXmlElements(XElement original, XElement updated)
    {
        var changes = new List<FieldChange>();
        var allFieldNames = original.Elements().Select(e => e.Name.LocalName)
            .Union(updated.Elements().Select(e => e.Name.LocalName))
            .Distinct();

        foreach (var fieldName in allFieldNames)
        {
            var oldValue = original.Element(fieldName)?.Value;
            var newValue = updated.Element(fieldName)?.Value;

            if (oldValue != newValue)
            {
                changes.Add(new FieldChange
                {
                    FieldName = fieldName,
                    OldValue = oldValue,
                    NewValue = newValue
                });
            }
        }

        return changes;
    }

    // ── Backup logic ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates local backup with ORIGINAL content (before changes)
    /// </summary>
    private void CreateLocalBackup(string fullPath)
    {
        var fileDir = Path.GetDirectoryName(fullPath)!;
        var originalFileName = Path.GetFileName(fullPath);
        var localBackupDir = Path.Combine(fileDir, BackupCfg.LocalFolderName);
        Directory.CreateDirectory(localBackupDir);
        var localBackupPath = Path.Combine(localBackupDir, originalFileName);
        File.Copy(fullPath, localBackupPath, overwrite: true);
    }

    /// <summary>
    /// Creates user backup with NEW content (after changes)
    /// File must already have the new content written to it
    /// </summary>
    private string CreateUserBackup(string fullPath, string username)
    {
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fullPath);
        var ext = Path.GetExtension(fullPath);
        var timestamp = DateTime.Now.ToString(BackupCfg.DateTimeFormat);

        var userBackupRoot = Path.Combine(BasePath, BackupCfg.UserBackupFolderName);
        var userBackupDir = Path.Combine(userBackupRoot, username);
        Directory.CreateDirectory(userBackupDir);

        var userBackupFileName = $"{fileNameWithoutExt}_{timestamp}_{username}{ext}";
        var userBackupPath = Path.Combine(userBackupDir, userBackupFileName);
        
        // Copy the file AFTER it has been updated with new content
        File.Copy(fullPath, userBackupPath, overwrite: false);

        return userBackupPath;
    }

    /// <summary>
    /// Gets the local backup path
    /// </summary>
    private string GetLocalBackupPath(string fullPath)
    {
        var fileDir = Path.GetDirectoryName(fullPath)!;
        var originalFileName = Path.GetFileName(fullPath);
        var localBackupDir = Path.Combine(fileDir, BackupCfg.LocalFolderName);
        return Path.Combine(localBackupDir, originalFileName);
    }

    // ── Security ─────────────────────────────────────────────────────────────

    private string GetSafePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(BasePath, relativePath));
        if (!fullPath.StartsWith(BasePath, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Access denied: path is outside the base directory");
        return fullPath;
    }
}

// ── Helper class for field-level change tracking ─────────────────────────────
internal class FieldChange
{
    public string FieldName { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

