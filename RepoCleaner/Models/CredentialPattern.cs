using System.Text.RegularExpressions;

namespace RepoCleaner.Models;

public class CredentialPattern
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Regex Pattern { get; set; } = null!;
    public CredentialType Type { get; set; }
}

public enum CredentialType
{
    ConnectionString,
    ApiKey,
    Token,
    Password,
    Secret,
    Certificate,
    PrivateKey
}
