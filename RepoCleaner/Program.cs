using System.CommandLine;
using RepoCleaner.Models;
using RepoCleaner.Services;
using Spectre.Console;

namespace RepoCleaner;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("RepoCleaner - GitHub Migration Tool")
        {
            Description = "Scans and cleans git repositories for prohibited files and credentials before GitHub migration"
        };

        var pathArgument = new Argument<string>(
            "path",
            getDefaultValue: () => Directory.GetCurrentDirectory(),
            description: "Path to the git repository to scan");

        var dryRunOption = new Option<bool>(
            "--dry-run",
            "Preview only - don't make any changes");

        var yesOption = new Option<bool>(
            "--yes",
            "Skip confirmation prompts (automated cleanup)");

        var configOption = new Option<string?>(
            "--config",
            "Path to configuration JSON file");

        rootCommand.AddArgument(pathArgument);
        rootCommand.AddOption(dryRunOption);
        rootCommand.AddOption(yesOption);
        rootCommand.AddOption(configOption);

        rootCommand.SetHandler(RunCleaner, pathArgument, dryRunOption, yesOption, configOption);

        return await rootCommand.InvokeAsync(args);
    }

    static void RunCleaner(string path, bool dryRun, bool autoYes, string? configPath)
    {
        PrintBanner();

        // Load configuration
        var configService = new ConfigurationService();
        var config = configService.LoadConfiguration(configPath);

        // Validate repository path
        var repoPath = Path.GetFullPath(path);
        if (!Directory.Exists(repoPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Directory not found: {repoPath}");
            return;
        }

        AnsiConsole.MarkupLine($"[blue]Repository:[/] {repoPath}");
        AnsiConsole.WriteLine();

        if (dryRun)
        {
            AnsiConsole.MarkupLine("[yellow]DRY RUN MODE - No changes will be made[/]");
            AnsiConsole.WriteLine();
        }

        // Phase 1: Scan for prohibited files
        AnsiConsole.Write(new Rule("[blue]Phase 1: File Scanning[/]").LeftJustified());
        var scanner = new RepositoryScanner(config);
        var scanResult = scanner.Scan(repoPath);

        // Phase 2: Scan for credentials
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[blue]Phase 2: Credential Scanning[/]").LeftJustified());
        var credScanner = new CredentialScanner(config);
        var credResult = credScanner.Scan(repoPath);

        // Phase 3: Display report
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[blue]Scan Results[/]").LeftJustified());
        DisplayScanReport(scanResult, credResult);

        // If nothing found, exit early
        if (scanResult.TotalProhibitedFiles == 0 && scanResult.LargeFiles.Count == 0 && credResult.TotalCredentialsFound == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ Repository is clean! No issues found.[/]");
            return;
        }

        if (dryRun)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]DRY RUN complete. No changes were made.[/]");
            GenerateReport(repoPath, scanResult, credResult, null, config);
            return;
        }

        // Phase 4: Interactive cleanup
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[blue]Phase 3: Cleanup[/]").LeftJustified());

        var removeFromHistory = false;
        var sanitizeCreds = false;

        if (scanResult.TotalProhibitedFiles > 0)
        {
            if (autoYes)
            {
                removeFromHistory = true;
            }
            else
            {
                removeFromHistory = AnsiConsole.Confirm(
                    "Remove prohibited files from git history?", defaultValue: true);
            }
        }

        if (credResult.TotalCredentialsFound > 0)
        {
            if (autoYes)
            {
                sanitizeCreds = true;
            }
            else
            {
                sanitizeCreds = AnsiConsole.Confirm(
                    "Sanitize detected credentials?", defaultValue: true);
            }
        }

        if (!removeFromHistory && !sanitizeCreds)
        {
            AnsiConsole.MarkupLine("[yellow]No cleanup actions selected.[/]");
            return;
        }

        // Show summary of actions
        AnsiConsole.WriteLine();
        DisplayActionSummary(scanResult, credResult, removeFromHistory, sanitizeCreds);

        if (!autoYes)
        {
            if (!AnsiConsole.Confirm("Proceed with cleanup?", defaultValue: false))
            {
                AnsiConsole.MarkupLine("[yellow]Cleanup cancelled.[/]");
                return;
            }
        }

        // Execute cleanup
        AnsiConsole.WriteLine();
        var cleaner = new RepositoryCleaner(config);
        var cleanResult = cleaner.Clean(repoPath, scanResult, removeFromHistory, sanitizeCreds);

        if (sanitizeCreds)
        {
            credScanner.SanitizeCredentials(repoPath, credResult);
            AnsiConsole.MarkupLine($"[green]✓[/] Sanitized credentials in {credResult.Matches.Select(m => m.FilePath).Distinct().Count()} files");
        }

        // Phase 5: Final report
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Cleanup Complete[/]").LeftJustified());
        DisplayCleanupReport(cleanResult, credResult, sanitizeCreds);
        GenerateReport(repoPath, scanResult, credResult, cleanResult, config);

        // Next steps
        DisplayNextSteps(cleanResult, credResult, sanitizeCreds);
    }

    static void PrintBanner()
    {
        AnsiConsole.Write(new FigletText("RepoCleaner")
            .Color(Color.Blue));
        AnsiConsole.MarkupLine("[grey]GitHub Migration Tool - Clean repos for push protection compliance[/]");
        AnsiConsole.WriteLine();
    }

    static void DisplayScanReport(ScanResult scanResult, CredentialScanResult credResult)
    {
        // Summary table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Category")
            .AddColumn("Count")
            .AddColumn("Details");

        table.AddRow(
            "[yellow]Prohibited Files (Working Dir)[/]",
            scanResult.WorkingDirectoryFiles.Count.ToString(),
            string.Join(", ", scanResult.WorkingDirectoryFiles.Select(f => f.Extension).Distinct().Take(5)));

        table.AddRow(
            "[yellow]Prohibited Files (Git History)[/]",
            scanResult.GitHistoryFiles.Count.ToString(),
            string.Join(", ", scanResult.GitHistoryFiles.Select(f => f.Extension).Distinct().Take(5)));

        table.AddRow(
            "[red]Large Files (>100MB)[/]",
            scanResult.LargeFiles.Count.ToString(),
            FormatBytes(scanResult.LargeFiles.Sum(f => f.SizeBytes)));

        table.AddRow(
            "[red]Credentials Found[/]",
            credResult.TotalCredentialsFound.ToString(),
            string.Join(", ", credResult.ByType.Select(kv => $"{kv.Key}: {kv.Value}")));

        AnsiConsole.Write(table);

        // Detailed file list (top 20)
        if (scanResult.WorkingDirectoryFiles.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Prohibited files in working directory:[/]");
            foreach (var file in scanResult.WorkingDirectoryFiles.Take(20))
            {
                AnsiConsole.MarkupLine($"  • {file.FilePath} [grey]({FormatBytes(file.SizeBytes)})[/]");
            }
            if (scanResult.WorkingDirectoryFiles.Count > 20)
                AnsiConsole.MarkupLine($"  [grey]... and {scanResult.WorkingDirectoryFiles.Count - 20} more[/]");
        }

        if (scanResult.GitHistoryFiles.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Prohibited files in git history:[/]");
            foreach (var file in scanResult.GitHistoryFiles.Take(20))
            {
                AnsiConsole.MarkupLine($"  • {file.FilePath} [grey]({file.Extension})[/]");
            }
            if (scanResult.GitHistoryFiles.Count > 20)
                AnsiConsole.MarkupLine($"  [grey]... and {scanResult.GitHistoryFiles.Count - 20} more[/]");
        }

        if (credResult.TotalCredentialsFound > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Credentials detected:[/]");
            foreach (var match in credResult.Matches.Take(20))
            {
                AnsiConsole.MarkupLine($"  • [grey]{match.FilePath}:{match.LineNumber}[/] - {match.PatternName}");
            }
            if (credResult.TotalCredentialsFound > 20)
                AnsiConsole.MarkupLine($"  [grey]... and {credResult.TotalCredentialsFound - 20} more[/]");
        }
    }

    static void DisplayActionSummary(ScanResult scanResult, CredentialScanResult credResult,
        bool removeFromHistory, bool sanitizeCreds)
    {
        var panel = new Panel(new Markup(string.Join("\n", new[]
        {
            removeFromHistory ? $"[yellow]• Remove {scanResult.TotalProhibitedFiles} prohibited files from history[/]" : null,
            scanResult.WorkingDirectoryFiles.Count > 0 ? $"[yellow]• Remove {scanResult.WorkingDirectoryFiles.Count} files from working directory[/]" : null,
            sanitizeCreds ? $"[yellow]• Sanitize {credResult.TotalCredentialsFound} credentials in {credResult.Matches.Select(m => m.FilePath).Distinct().Count()} files[/]" : null,
            "[green]• Full backup will be created before any changes[/]",
            "[green]• Removed files will be moved to safe-keeping folder[/]"
        }.Where(s => s != null))))
        {
            Header = new PanelHeader("[blue] Actions to Perform [/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
    }

    static void DisplayCleanupReport(CleanResult cleanResult, CredentialScanResult credResult, bool sanitized)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Action")
            .AddColumn("Result");

        table.AddRow("Files removed (working dir)", cleanResult.RemovedFromWorkingDirectory.Count.ToString());
        table.AddRow("Files removed (history)", cleanResult.RemovedFromHistory.Count.ToString());
        table.AddRow("Space freed", FormatBytes(cleanResult.SpaceFreedBytes));
        table.AddRow("History rewritten", cleanResult.HistoryRewritten ? "[green]Yes[/]" : "[grey]No[/]");

        if (sanitized)
            table.AddRow("Credentials sanitized", credResult.TotalCredentialsFound.ToString());

        table.AddRow("Backup location", cleanResult.BackupPath);

        if (!string.IsNullOrEmpty(cleanResult.SafeKeepingPath))
            table.AddRow("Safe-keeping folder", cleanResult.SafeKeepingPath);

        AnsiConsole.Write(table);

        if (cleanResult.Errors.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Errors encountered:[/]");
            foreach (var error in cleanResult.Errors)
            {
                AnsiConsole.MarkupLine($"  [red]•[/] {error}");
            }
        }
    }

    static void DisplayNextSteps(CleanResult cleanResult, CredentialScanResult credResult, bool sanitized)
    {
        AnsiConsole.WriteLine();
        var steps = new List<string>
        {
            "Verify the repository still builds and tests pass",
            "Review the cleanup report in the reports folder"
        };

        if (cleanResult.HistoryRewritten)
        {
            steps.Add("Force push to update remote: [blue]git push --force --all[/]");
            steps.Add("Notify team members to re-clone the repository");
        }

        if (sanitized)
        {
            steps.Add("[red]⚠ IMPORTANT: Rotate all detected credentials immediately![/]");
            steps.Add("Credentials were removed from files but may still exist in git history backups");
        }

        steps.Add("Try pushing to GitHub: [blue]git push origin main[/]");

        var panel = new Panel(new Markup(string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"))))
        {
            Header = new PanelHeader("[blue] Next Steps [/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
    }

    static void GenerateReport(string repoPath, ScanResult scanResult, CredentialScanResult credResult,
        CleanResult? cleanResult, CleanerConfiguration config)
    {
        var reportDir = Path.Combine(
            Path.GetDirectoryName(repoPath)!,
            config.ReportOutputFolder);
        Directory.CreateDirectory(reportDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // File scan report
        var fileScanReport = Path.Combine(reportDir, $"file_scan_report_{timestamp}.txt");
        using (var writer = new StreamWriter(fileScanReport))
        {
            writer.WriteLine("=== RepoCleaner File Scan Report ===");
            writer.WriteLine($"Repository: {repoPath}");
            writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Bare Repository: {scanResult.IsBareRepository}");
            writer.WriteLine();
            writer.WriteLine($"Prohibited Files (Working Directory): {scanResult.WorkingDirectoryFiles.Count}");
            foreach (var f in scanResult.WorkingDirectoryFiles)
                writer.WriteLine($"  {f.FilePath} ({FormatBytes(f.SizeBytes)})");
            writer.WriteLine();
            writer.WriteLine($"Prohibited Files (Git History): {scanResult.GitHistoryFiles.Count}");
            foreach (var f in scanResult.GitHistoryFiles)
                writer.WriteLine($"  {f.FilePath} ({f.Extension}) [commit: {f.CommitSha?[..7]}]");
            writer.WriteLine();
            writer.WriteLine($"Large Files: {scanResult.LargeFiles.Count}");
            foreach (var f in scanResult.LargeFiles)
                writer.WriteLine($"  {f.FilePath} ({FormatBytes(f.SizeBytes)})");
        }

        // Credential scan report
        var credReport = Path.Combine(reportDir, $"credential_scan_report_{timestamp}.txt");
        using (var writer = new StreamWriter(credReport))
        {
            writer.WriteLine("=== RepoCleaner Credential Scan Report ===");
            writer.WriteLine($"Repository: {repoPath}");
            writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Files Scanned: {credResult.FilesScanned}");
            writer.WriteLine($"Files Skipped: {credResult.FilesSkipped}");
            writer.WriteLine($"Credentials Found: {credResult.TotalCredentialsFound}");
            writer.WriteLine();
            writer.WriteLine("⚠ WARNING: Rotate all detected credentials immediately!");
            writer.WriteLine();
            foreach (var match in credResult.Matches)
            {
                writer.WriteLine($"  [{match.Type}] {match.FilePath}:{match.LineNumber} - {match.PatternName}");
            }
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Reports saved to: [blue]{reportDir}[/]");
    }

    static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
