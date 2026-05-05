namespace RepoCleaner.Models;

public class ScanResult
{
    public string RepositoryPath { get; set; } = string.Empty;
    public bool IsBareRepository { get; set; }
    public List<ProhibitedFileInfo> WorkingDirectoryFiles { get; set; } = new();
    public List<ProhibitedFileInfo> GitHistoryFiles { get; set; } = new();
    public List<LargeFileInfo> LargeFiles { get; set; } = new();
    public int TotalProhibitedFiles => WorkingDirectoryFiles.Count + GitHistoryFiles.Count;
    public long TotalSizeBytes => WorkingDirectoryFiles.Sum(f => f.SizeBytes) + LargeFiles.Sum(f => f.SizeBytes);
}

public class ProhibitedFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? CommitSha { get; set; }
    public string? BranchName { get; set; }
    public bool IsInHistory { get; set; }
}

public class LargeFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? CommitSha { get; set; }
    public bool IsInHistory { get; set; }
}
