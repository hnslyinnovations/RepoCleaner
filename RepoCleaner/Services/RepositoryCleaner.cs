using System.Diagnostics;
using RepoCleaner.Models;
using Spectre.Console;

namespace RepoCleaner.Services;

public class RepositoryCleaner
{
    private readonly CleanerConfiguration _config;

    public RepositoryCleaner(CleanerConfiguration config)
    {
        _config = config;
    }

    public CleanResult Clean(string repositoryPath, ScanResult scanResult, bool removeFromHistory, bool sanitizeCredentials)
    {
        var result = new CleanResult
        {
            RepositoryPath = repositoryPath
        };

        try
        {
            // Step 1: Create full backup
            var backupPath = CreateBackup(repositoryPath);
            result.BackupPath = backupPath;
            AnsiConsole.MarkupLine($"[green]✓[/] Backup created at: [blue]{backupPath}[/]");

            // Step 2: Remove prohibited files from working directory
            if (scanResult.WorkingDirectoryFiles.Count > 0)
            {
                RemoveFromWorkingDirectory(repositoryPath, scanResult, result);
            }

            // Step 3: Remove from git history using git filter-repo
            if (removeFromHistory && scanResult.GitHistoryFiles.Count > 0)
            {
                RemoveFromGitHistory(repositoryPath, scanResult, result);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Cleaning failed: {ex.Message}");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }

        return result;
    }

    private string CreateBackup(string repositoryPath)
    {
        var repoName = Path.GetFileName(repositoryPath.TrimEnd(Path.DirectorySeparatorChar));
        var backupDir = Path.Combine(
            Path.GetDirectoryName(repositoryPath)!,
            _config.BackupFolder,
            $"{repoName}_{DateTime.Now:yyyyMMdd_HHmmss}");

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Creating backup...", ctx =>
            {
                CopyDirectory(repositoryPath, backupDir);
            });

        return backupDir;
    }

    private void RemoveFromWorkingDirectory(string repositoryPath, ScanResult scanResult, CleanResult result)
    {
        var safeKeepingDir = Path.Combine(
            Path.GetDirectoryName(repositoryPath)!,
            _config.SafeKeepingFolder);

        Directory.CreateDirectory(safeKeepingDir);
        result.SafeKeepingPath = safeKeepingDir;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Removing prohibited files from working directory...", ctx =>
            {
                foreach (var file in scanResult.WorkingDirectoryFiles)
                {
                    var fullPath = Path.Combine(repositoryPath, file.FilePath);
                    if (!File.Exists(fullPath)) continue;

                    try
                    {
                        // Move to safe-keeping
                        var destPath = Path.Combine(safeKeepingDir, file.FilePath);
                        var destDir = Path.GetDirectoryName(destPath)!;
                        Directory.CreateDirectory(destDir);
                        File.Move(fullPath, destPath, overwrite: true);

                        result.RemovedFromWorkingDirectory.Add(file.FilePath);
                        result.SpaceFreedBytes += file.SizeBytes;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to remove {file.FilePath}: {ex.Message}");
                    }
                }
            });

        AnsiConsole.MarkupLine($"[green]✓[/] Removed {result.RemovedFromWorkingDirectory.Count} files from working directory");
    }

    private void RemoveFromGitHistory(string repositoryPath, ScanResult scanResult, CleanResult result)
    {
        // Build the list of paths to remove
        var pathsToRemove = scanResult.GitHistoryFiles
            .Select(f => f.FilePath)
            .Distinct()
            .ToList();

        AnsiConsole.MarkupLine($"[yellow]Rewriting git history to remove {pathsToRemove.Count} file paths...[/]");

        // Create a paths file for git filter-repo
        var pathsFile = Path.Combine(Path.GetTempPath(), $"repocleaner_paths_{Guid.NewGuid():N}.txt");
        File.WriteAllLines(pathsFile, pathsToRemove);

        try
        {
            var args = $"filter-repo --invert-paths --paths-from-file \"{pathsFile}\" --force";
            var exitCode = RunGitCommand(repositoryPath, args);

            if (exitCode == 0)
            {
                result.HistoryRewritten = true;
                result.RemovedFromHistory.AddRange(pathsToRemove);
                AnsiConsole.MarkupLine($"[green]✓[/] Git history rewritten successfully");
            }
            else
            {
                result.Errors.Add("git filter-repo failed. Ensure git-filter-repo is installed.");
                AnsiConsole.MarkupLine("[red]Error:[/] git filter-repo failed. Is git-filter-repo installed?");
                AnsiConsole.MarkupLine("[yellow]Install with:[/] pip install git-filter-repo");
            }
        }
        finally
        {
            if (File.Exists(pathsFile))
                File.Delete(pathsFile);
        }
    }

    private static int RunGitCommand(string workingDirectory, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return -1;

            process.WaitForExit(TimeSpan.FromMinutes(30));
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error running git:[/] {ex.Message}");
            return -1;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var dirName = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(destination, dirName));
        }
    }
}
