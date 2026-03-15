namespace CloudCacheManager.Services
{
    public class FileValidationService
    {
        public ValidationResult Validate(string filePath, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return Fail("File content cannot be empty.");

            var ext = Path.GetExtension(filePath).ToLower();

            return ext switch
            {
                ".json" => ValidateJson(content),
                ".xml" => ValidateXml(content),
                ".config" => ValidateXml(content),  // web.config, app.config
                ".txt" => ValidateTxt(content),
                ".csv" => ValidateCsv(content),
                ".ini" => ValidateIni(content),
                ".yaml" or ".yml" => ValidateYaml(content),
                _ =>  Warn("Unknown file type — saved without validation.")
            };
        }
        // ── JSON ──────────────────────────────────────────────────────
        private ValidationResult ValidateJson(string content)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(content,
                    new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = false });

                // Check for empty object
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                    && !doc.RootElement.EnumerateObject().Any())
                    return Warn("JSON is valid but object is empty.");

                return Ok();
            }
            catch (System.Text.Json.JsonException ex)
            {
                return Fail($"Invalid JSON at line {ex.LineNumber}: {ex.Message}",
                            (int?)ex.LineNumber,
                            "Check for missing commas, brackets, or quotes.");
            }
        }

        // ── XML / Config ──────────────────────────────────────────────
        private ValidationResult ValidateXml(string content)
        {
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(content);

                // Rule 1: Duplicate keys
                var keys = doc.SelectNodes("//add/@key");
                if (keys != null)
                {
                    var duplicates = keys.Cast<System.Xml.XmlNode>()
                        .Select(k => k.Value ?? "")
                        .GroupBy(k => k)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                    if (duplicates.Any())
                        return Fail(
                            $"Duplicate keys found: {string.Join(", ", duplicates)}",
                            null,
                            "Remove or rename the duplicate keys."
                        );
                }

                // Rule 2: Empty value check
                var emptyValues = doc.SelectNodes("//add[@value='']");
                if (emptyValues != null && emptyValues.Count > 0)
                    return Warn($"{emptyValues.Count} key(s) have empty values. Verify this is intentional.");

                // Rule 3: Check required root element
                if (doc.DocumentElement == null)
                    return Fail("XML has no root element.");

                return Ok();
            }
            catch (System.Xml.XmlException ex)
            {
                return Fail(
                    $"Invalid XML at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}",
                    ex.LineNumber,
                    "Check for unclosed tags, missing quotes, or special characters."
                );
            }
        }

        // ── TXT ───────────────────────────────────────────────────────
        private ValidationResult ValidateTxt(string content)
        {
            // Rule: Warn if file is very large
            if (content.Length > 500_000)
                return Warn("File is very large (>500KB). Saving may take time.");

            // Rule: Check for null bytes (binary content accidentally saved as txt)
            if (content.Contains('\0'))
                return Fail("File contains null bytes — this may be a binary file, not text.");

            return Ok();
        }
        // ── CSV ───────────────────────────────────────────────────────
        private ValidationResult ValidateCsv(string content)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return Fail("CSV file is empty.");

            // Rule: Consistent column count
            var headerCount = lines[0].Split(',').Length;
            for (int i = 1; i < lines.Length; i++)
            {
                var colCount = lines[i].Split(',').Length;
                if (colCount != headerCount)
                    return Fail(
                        $"CSV row {i + 1} has {colCount} columns but header has {headerCount}.",
                        i + 1,
                        "Ensure all rows have the same number of columns as the header."
                    );
            }

            return Ok();
        }
        // ── INI ───────────────────────────────────────────────────────
        private ValidationResult ValidateIni(string content)
        {
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;
                if (line.StartsWith("[") && !line.EndsWith("]"))
                    return Fail($"Invalid section header at line {i + 1}: {line}", i + 1,
                                 "Section headers must be enclosed in [ ]");
                if (!line.StartsWith("[") && !line.Contains("="))
                    return Fail($"Invalid key-value pair at line {i + 1}: '{line}'", i + 1,
                                 "Each line must be in format: key=value");
            }
            return Ok();
        }
        // ── YAML ──────────────────────────────────────────────────────
        private ValidationResult ValidateYaml(string content)
        {
            // Basic YAML checks without external library
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // Check for tabs (YAML doesn't allow tabs for indentation)
                if (line.StartsWith("\t"))
                    return Fail(
                        $"Tab character found at line {i + 1}. YAML requires spaces for indentation.",
                        i + 1,
                        "Replace tabs with spaces."
                    );
            }
            return Ok();
        }
        // ── Helpers ───────────────────────────────────────────────────
        private ValidationResult Ok() =>
            new() { IsValid = true };

        private ValidationResult Warn(string message) =>
            new() { IsValid = true, Error = message };   // warn but allow save

        private ValidationResult Fail(string error, int? line = null, string? suggestion = null) =>
            new() { IsValid = false, Error = error, LineNumber = line, Suggestion = suggestion };

    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Error { get; set; } = string.Empty;
        public int? LineNumber { get; set; }
        public string? Suggestion { get; set; }
    }
}
