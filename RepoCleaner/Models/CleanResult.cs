namespace RepoCleaner.Models;

public class CleanResult
{
    public string RepositoryPath { get; set; } = string.Empty;
    public List<string> RemovedFromWorkingDirectory { get; set; } = new();
    public List<string> RemovedFromHistory { get; set; } = new();
    public List<string> SanitizedFiles { get; set; } = new();
    public long SpaceFreedBytes { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string SafeKeepingPath { get; set; } = string.Empty;
    public bool HistoryRewritten { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
}
