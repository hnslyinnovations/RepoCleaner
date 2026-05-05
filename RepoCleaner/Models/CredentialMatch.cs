namespace RepoCleaner.Models;

public class CredentialMatch
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public string PatternName { get; set; } = string.Empty;
    public CredentialType Type { get; set; }
    public string MatchedValue { get; set; } = string.Empty;
}
