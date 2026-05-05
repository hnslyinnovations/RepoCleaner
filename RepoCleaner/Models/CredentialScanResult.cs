namespace RepoCleaner.Models;

public class CredentialScanResult
{
    public string RepositoryPath { get; set; } = string.Empty;
    public List<CredentialMatch> Matches { get; set; } = new();
    public int FilesScanned { get; set; }
    public int FilesSkipped { get; set; }
    public int TotalCredentialsFound => Matches.Count;

    public Dictionary<CredentialType, int> ByType =>
        Matches.GroupBy(m => m.Type)
               .ToDictionary(g => g.Key, g => g.Count());
}
