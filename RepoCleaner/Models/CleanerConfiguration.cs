namespace RepoCleaner.Models;

public class CleanerConfiguration
{
    public List<string> ProhibitedExtensions { get; set; } = new();
    public List<string> ExcludePatterns { get; set; } = new();
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    public long CredentialScanMaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB
    public List<string> ConfigFilePatterns { get; set; } = new();
    public string SafeKeepingFolder { get; set; } = "_repocleaner_removed";
    public string BackupFolder { get; set; } = "_repocleaner_backup";
    public string ReportOutputFolder { get; set; } = "_repocleaner_reports";
}
