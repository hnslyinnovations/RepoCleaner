using System.Text.Json;
using RepoCleaner.Models;

namespace RepoCleaner.Services;

public class ConfigurationService
{
    private const string DefaultConfigFileName = "repocleaner.json";

    private static readonly List<string> DefaultProhibitedExtensions = new()
    {
        ".zip", ".jar", ".dll", ".exe", ".pdf", ".war", ".ear",
        ".tar", ".gz", ".tgz", ".rar", ".7z", ".bz2", ".xz",
        ".msi", ".msm", ".msp", ".cab", ".iso", ".img", ".dmg",
        ".nupkg", ".snupkg", ".whl",
        ".bin", ".obj", ".o", ".a", ".lib", ".so", ".dylib",
        ".class", ".pyc", ".pyo", ".pdb",
        ".suo", ".user", ".userosscache", ".sln.docstates",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".mp3", ".mp4", ".avi", ".mov", ".wmv", ".flv",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico",
        ".ttf", ".otf", ".woff", ".woff2", ".eot"
    };

    private static readonly List<string> DefaultExcludePatterns = new()
    {
        "node_modules", ".git", "bin", "obj", "packages",
        ".vs", ".idea", "__pycache__", "dist", "build"
    };

    private static readonly List<string> DefaultConfigFilePatterns = new()
    {
        "*.config", "appsettings*.json", "web.config", "app.config",
        "connectionstrings.json", "secrets.json", "*.settings",
        "launchSettings.json"
    };

    public CleanerConfiguration LoadConfiguration(string? configPath = null)
    {
        var path = configPath ?? FindConfigFile();

        if (path != null && File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<CleanerConfiguration>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return config ?? GetDefaultConfiguration();
            }
            catch
            {
                return GetDefaultConfiguration();
            }
        }

        return GetDefaultConfiguration();
    }

    public CleanerConfiguration GetDefaultConfiguration()
    {
        return new CleanerConfiguration
        {
            ProhibitedExtensions = DefaultProhibitedExtensions,
            ExcludePatterns = DefaultExcludePatterns,
            ConfigFilePatterns = DefaultConfigFilePatterns,
            MaxFileSizeBytes = 100 * 1024 * 1024,
            CredentialScanMaxFileSize = 10 * 1024 * 1024
        };
    }

    private string? FindConfigFile()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, DefaultConfigFileName);
        return File.Exists(configPath) ? configPath : null;
    }
}
