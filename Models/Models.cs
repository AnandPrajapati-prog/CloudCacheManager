namespace CloudCacheManager.Models;

/// <summary>
/// Role names — must exactly match the "Role" values in appsettings.json → AppSettings:Users
/// </summary>
public static class Roles
{
    public const string Admin   = "Admin";
    public const string Manager = "Manager";
    public const string Viewer  = "Viewer";
}

/// <summary>
/// Named authorization policies registered in Program.cs
/// </summary>
public static class Policies
{
    /// <summary>Admin + Manager + Viewer — any authenticated user</summary>
    public const string CanDownload = "CanDownload";

    /// <summary>Admin + Manager only — edit files and add key/value</summary>
    public const string CanEdit = "CanEdit";
}

public class FolderNode
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool HasFiles { get; set; }
    public List<FileItem> Files { get; set; } = new();
    public List<FolderNode> SubFolders { get; set; } = new();
}

public class FileItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsJson { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class EditFileRequest
{
    public string FilePath { get; set; } = "";
    public string Content { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class AddKeyValueRequest
{
    public string FilePath { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
    public BackupResult? Backup { get; set; }
}

/// <summary>
/// Holds both backup paths created on every write operation.
/// </summary>
public class BackupResult
{
    /// <summary>Backup next to the original file — original filename preserved.</summary>
    public string LocalBackupPath { get; set; } = "";

    /// <summary>Backup in UserBackup folder — filename_datetime_username format.</summary>
    public string UserBackupPath { get; set; } = "";
}

// ── Config POCOs bound from appsettings.json ──────────────────────────────────

public class AppSettings
{
    public string BasePath { get; set; } = "";
    public BackupSettings Backup { get; set; } = new();
    public List<UserConfig> Users { get; set; } = new();
}

public class BackupSettings
{
    /// <summary>Folder name created beside each edited file. e.g. "_Backups"</summary>
    public string LocalFolderName { get; set; } = "_Backups";

    /// <summary>Top-level folder under BasePath for user-tagged backups. e.g. "UserBackup"</summary>
    public string UserBackupFolderName { get; set; } = "UserBackup";

    /// <summary>DateTime format string used in backup filenames. e.g. "yyyyMMdd_HHmmss"</summary>
    public string DateTimeFormat { get; set; } = "yyyyMMdd_HHmmss";
}

public class UserConfig
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = Roles.Viewer;
    public string Description { get; set; } = "";
}


