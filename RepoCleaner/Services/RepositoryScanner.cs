using LibGit2Sharp;
using RepoCleaner.Models;
using Spectre.Console;

namespace RepoCleaner.Services;

public class RepositoryScanner
{
    private readonly CleanerConfiguration _config;

    public RepositoryScanner(CleanerConfiguration config)
    {
        _config = config;
    }

    public ScanResult Scan(string repositoryPath)
    {
        var result = new ScanResult
        {
            RepositoryPath = repositoryPath
        };

        var isBare = Repository.IsValid(repositoryPath) &&
                     !Directory.Exists(Path.Combine(repositoryPath, ".git"));

        // Check if it's a bare repo or has a .git folder
        var gitPath = isBare ? repositoryPath : Path.Combine(repositoryPath, ".git");
        if (!Repository.IsValid(isBare ? repositoryPath : repositoryPath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Not a valid git repository.");
            return result;
        }

        result.IsBareRepository = isBare;

        // Scan working directory (only for non-bare repos)
        if (!isBare)
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start("Scanning working directory...", ctx =>
                {
                    ScanWorkingDirectory(repositoryPath, result);
                });
        }

        // Scan git history
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Scanning git history...", ctx =>
            {
                ScanGitHistory(repositoryPath, isBare, result);
            });

        return result;
    }

    private void ScanWorkingDirectory(string repoPath, ScanResult result)
    {
        var files = Directory.EnumerateFiles(repoPath, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(repoPath, file);

            // Skip excluded patterns
            if (IsExcluded(relativePath))
                continue;

            var extension = Path.GetExtension(file).ToLowerInvariant();
            var fileInfo = new FileInfo(file);

            // Check prohibited extensions
            if (_config.ProhibitedExtensions.Contains(extension))
            {
                result.WorkingDirectoryFiles.Add(new ProhibitedFileInfo
                {
                    FilePath = relativePath,
                    Extension = extension,
                    SizeBytes = fileInfo.Length,
                    IsInHistory = false
                });
            }

            // Check file size
            if (fileInfo.Length > _config.MaxFileSizeBytes)
            {
                result.LargeFiles.Add(new LargeFileInfo
                {
                    FilePath = relativePath,
                    SizeBytes = fileInfo.Length,
                    IsInHistory = false
                });
            }
        }
    }

    private void ScanGitHistory(string repoPath, bool isBare, ScanResult result)
    {
        var repoOpenPath = isBare ? repoPath : repoPath;
        using var repo = new Repository(repoOpenPath);

        var processedBlobs = new HashSet<string>(); // Deduplication

        foreach (var branch in repo.Branches.Where(b => !b.IsRemote))
        {
            foreach (var commit in branch.Commits)
            {
                ScanTree(commit.Tree, "", branch.FriendlyName, commit.Sha, processedBlobs, result);
            }
        }
    }

    private void ScanTree(LibGit2Sharp.Tree tree, string basePath, string branchName, string commitSha,
        HashSet<string> processedBlobs, ScanResult result)
    {
        foreach (var entry in tree)
        {
            var fullPath = string.IsNullOrEmpty(basePath) ? entry.Name : $"{basePath}/{entry.Name}";

            if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                ScanTree((LibGit2Sharp.Tree)entry.Target, fullPath, branchName, commitSha, processedBlobs, result);
            }
            else if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                var blob = (Blob)entry.Target;

                // Deduplicate by blob SHA
                if (!processedBlobs.Add(blob.Id.Sha))
                    continue;

                var extension = Path.GetExtension(entry.Name).ToLowerInvariant();

                if (_config.ProhibitedExtensions.Contains(extension))
                {
                    result.GitHistoryFiles.Add(new ProhibitedFileInfo
                    {
                        FilePath = fullPath,
                        Extension = extension,
                        SizeBytes = blob.Size,
                        CommitSha = commitSha,
                        BranchName = branchName,
                        IsInHistory = true
                    });
                }

                if (blob.Size > _config.MaxFileSizeBytes)
                {
                    result.LargeFiles.Add(new LargeFileInfo
                    {
                        FilePath = fullPath,
                        SizeBytes = blob.Size,
                        CommitSha = commitSha,
                        IsInHistory = true
                    });
                }
            }
        }
    }

    private bool IsExcluded(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => _config.ExcludePatterns.Contains(part, StringComparer.OrdinalIgnoreCase));
    }
}
