using System.Text.RegularExpressions;
using RepoCleaner.Models;
using Spectre.Console;

namespace RepoCleaner.Services;

public class CredentialScanner
{
    private readonly CleanerConfiguration _config;
    private readonly List<CredentialPattern> _patterns;

    public CredentialScanner(CleanerConfiguration config)
    {
        _config = config;
        _patterns = BuildPatterns();
    }

    public CredentialScanResult Scan(string repositoryPath)
    {
        var result = new CredentialScanResult
        {
            RepositoryPath = repositoryPath
        };

        var configFiles = FindConfigFiles(repositoryPath);

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Scanning for credentials...", ctx =>
            {
                foreach (var file in configFiles)
                {
                    var fileInfo = new FileInfo(file);

                    // Skip binary and large files
                    if (fileInfo.Length > _config.CredentialScanMaxFileSize || IsBinaryFile(file))
                    {
                        result.FilesSkipped++;
                        continue;
                    }

                    ScanFile(file, repositoryPath, result);
                    result.FilesScanned++;
                }
            });

        return result;
    }

    private void ScanFile(string filePath, string repoPath, CredentialScanResult result)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            var relativePath = Path.GetRelativePath(repoPath, filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                foreach (var pattern in _patterns)
                {
                    var match = pattern.Pattern.Match(line);
                    if (match.Success)
                    {
                        result.Matches.Add(new CredentialMatch
                        {
                            FilePath = relativePath,
                            LineNumber = i + 1,
                            LineContent = line.Trim(),
                            PatternName = pattern.Name,
                            Type = pattern.Type,
                            MatchedValue = match.Value
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not scan {filePath}: {ex.Message}");
        }
    }

    private List<string> FindConfigFiles(string repositoryPath)
    {
        var files = new List<string>();

        foreach (var pattern in _config.ConfigFilePatterns)
        {
            try
            {
                var matchingFiles = Directory.EnumerateFiles(repositoryPath, pattern, SearchOption.AllDirectories)
                    .Where(f => !IsExcluded(Path.GetRelativePath(repositoryPath, f)));
                files.AddRange(matchingFiles);
            }
            catch { /* Skip inaccessible directories */ }
        }

        return files.Distinct().ToList();
    }

    private bool IsExcluded(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => _config.ExcludePatterns.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsBinaryFile(string filePath)
    {
        try
        {
            var buffer = new byte[8192];
            using var stream = File.OpenRead(filePath);
            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0) return true;
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    public void SanitizeCredentials(string repositoryPath, CredentialScanResult scanResult)
    {
        var fileGroups = scanResult.Matches.GroupBy(m => m.FilePath);

        foreach (var group in fileGroups)
        {
            var fullPath = Path.Combine(repositoryPath, group.Key);
            if (!File.Exists(fullPath)) continue;

            // Create backup
            var backupPath = fullPath + ".backup";
            File.Copy(fullPath, backupPath, overwrite: true);

            var lines = File.ReadAllLines(fullPath).ToList();

            foreach (var match in group.OrderByDescending(m => m.LineNumber))
            {
                var lineIndex = match.LineNumber - 1;
                if (lineIndex < lines.Count)
                {
                    lines[lineIndex] = SanitizeLine(lines[lineIndex], match);
                }
            }

            File.WriteAllLines(fullPath, lines);
        }
    }

    private static string SanitizeLine(string line, CredentialMatch match)
    {
        // Preserve key names, only remove values
        // Handle patterns like: key="value" or key=value or "key": "value"
        var patterns = new[]
        {
            (@"(password\s*=\s*)"".+?""", "$1\"[REMOVED]\""),
            (@"(password\s*=\s*)[^;""]+", "$1[REMOVED]"),
            (@"(pwd\s*=\s*)"".+?""", "$1\"[REMOVED]\""),
            (@"(pwd\s*=\s*)[^;""]+", "$1[REMOVED]"),
            (@"(""(?:password|secret|key|token|apikey|api_key|connectionstring)""\s*:\s*)"".+?""", "$1\"[REMOVED]\""),
            (@"(value\s*=\s*)"".+?""", "$1\"[REMOVED]\""),
        };

        var result = line;
        foreach (var (pattern, replacement) in patterns)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            if (regex.IsMatch(result))
            {
                result = regex.Replace(result, replacement);
                break;
            }
        }

        return result;
    }

    private static List<CredentialPattern> BuildPatterns()
    {
        return new List<CredentialPattern>
        {
            // Connection strings
            new() { Name = "SQL Connection String Password", Type = CredentialType.ConnectionString,
                Pattern = new Regex(@"(?i)(password|pwd)\s*=\s*[^;""'\s]{3,}", RegexOptions.Compiled) },
            new() { Name = "Connection String with User ID", Type = CredentialType.ConnectionString,
                Pattern = new Regex(@"(?i)User\s+Id\s*=\s*[^;""]+;\s*(password|pwd)\s*=\s*[^;""]+", RegexOptions.Compiled) },
            new() { Name = "Data Source Connection String", Type = CredentialType.ConnectionString,
                Pattern = new Regex(@"(?i)Data\s+Source\s*=.*?(password|pwd)\s*=\s*[^;""]+", RegexOptions.Compiled) },

            // API Keys and Tokens
            new() { Name = "API Key Assignment", Type = CredentialType.ApiKey,
                Pattern = new Regex(@"(?i)(api[_-]?key|apikey)\s*[=:]\s*[""']?[A-Za-z0-9+/=_\-]{16,}[""']?", RegexOptions.Compiled) },
            new() { Name = "Bearer Token", Type = CredentialType.Token,
                Pattern = new Regex(@"(?i)bearer\s+[A-Za-z0-9+/=_\-\.]{20,}", RegexOptions.Compiled) },
            new() { Name = "Authorization Header", Type = CredentialType.Token,
                Pattern = new Regex(@"(?i)(authorization|auth[_-]?token)\s*[=:]\s*[""']?[A-Za-z0-9+/=_\-\.]{16,}[""']?", RegexOptions.Compiled) },

            // Passwords
            new() { Name = "Password in Config", Type = CredentialType.Password,
                Pattern = new Regex(@"(?i)(password|passwd|pwd)\s*[=:]\s*[""']?[^\s""'<>;]{3,}[""']?", RegexOptions.Compiled) },
            new() { Name = "Secret Value", Type = CredentialType.Secret,
                Pattern = new Regex(@"(?i)(secret|client[_-]?secret)\s*[=:]\s*[""']?[A-Za-z0-9+/=_\-]{8,}[""']?", RegexOptions.Compiled) },

            // Azure/Cloud specific
            new() { Name = "Azure Storage Key", Type = CredentialType.ApiKey,
                Pattern = new Regex(@"(?i)AccountKey\s*=\s*[A-Za-z0-9+/=]{44,}", RegexOptions.Compiled) },
            new() { Name = "Azure Connection String", Type = CredentialType.ConnectionString,
                Pattern = new Regex(@"(?i)DefaultEndpointsProtocol=https?;AccountName=.+;AccountKey=.+", RegexOptions.Compiled) },
            new() { Name = "SAS Token", Type = CredentialType.Token,
                Pattern = new Regex(@"(?i)[?&]sig=[A-Za-z0-9%+/=]{20,}", RegexOptions.Compiled) },

            // Private Keys
            new() { Name = "Private Key Header", Type = CredentialType.PrivateKey,
                Pattern = new Regex(@"-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----", RegexOptions.Compiled) },
            new() { Name = "Certificate Thumbprint", Type = CredentialType.Certificate,
                Pattern = new Regex(@"(?i)(thumbprint|fingerprint)\s*[=:]\s*[""']?[A-Fa-f0-9]{40}[""']?", RegexOptions.Compiled) },

            // JSON config patterns
            new() { Name = "JSON Password Field", Type = CredentialType.Password,
                Pattern = new Regex(@"""(?i)(password|secret|key|token|apiKey|api_key)""\s*:\s*""[^""]{3,}""", RegexOptions.Compiled) },

            // Generic patterns
            new() { Name = "Hardcoded Credential", Type = CredentialType.Password,
                Pattern = new Regex(@"(?i)(?:credential|cred)[s]?\s*[=:]\s*[""']?[^\s""']{8,}[""']?", RegexOptions.Compiled) },
            new() { Name = "Access Key", Type = CredentialType.ApiKey,
                Pattern = new Regex(@"(?i)(access[_-]?key[_-]?(?:id|secret)?)\s*[=:]\s*[""']?[A-Za-z0-9+/=_\-]{16,}[""']?", RegexOptions.Compiled) },
            new() { Name = "AWS Key Pattern", Type = CredentialType.ApiKey,
                Pattern = new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled) },
            new() { Name = "Generic Token Pattern", Type = CredentialType.Token,
                Pattern = new Regex(@"(?i)(token|jwt)\s*[=:]\s*[""']?eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.Compiled) },
            new() { Name = "Encryption Key", Type = CredentialType.Secret,
                Pattern = new Regex(@"(?i)(encryption[_-]?key|decrypt[_-]?key|master[_-]?key)\s*[=:]\s*[""']?[^\s""']{8,}[""']?", RegexOptions.Compiled) },
        };
    }
}
