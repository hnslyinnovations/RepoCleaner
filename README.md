# RepoCleaner - GitHub Migration Tool

Quick Summary on Why?
I spent countless hours on trying to find the right tools and searching up Git syntax to figure this out as we were in the middle of a large migration from ADO to Github. Github implied some rules for the commits that caused some issues with legacy projects and bad source code management. After about 7 migrations and doing this manually, I decided it was enough so I present RepoCleaner.

A .NET 8.0 console application that scans and cleans git repositories for prohibited files and credentials before migrating from Azure DevOps to GitHub.

## ⚠️ Disclaimer

**THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.** The author(s) assume no liability for any damages, data loss, or unintended consequences resulting from the use of this tool. By using RepoCleaner, you acknowledge and accept that:

- This tool rewrites git history, which is a **destructive and irreversible operation**
- You are solely responsible for backing up your repositories before use
- The author(s) are not liable for any lost data, corrupted repositories, or broken workflows
- You use this tool entirely at your own risk
- This tool is not a substitute for proper security practices or professional code review

**ALWAYS CREATE A FULL BACKUP OF YOUR REPOSITORY BEFORE RUNNING THIS TOOL.**

## Features

- **File Scanning** - Detects prohibited file extensions in working directory and git history
- **Credential Scanning** - Finds passwords, API keys, tokens, and connection strings in config files
- **Interactive Workflow** - Guided prompts with confirmation before destructive operations
- **History Rewriting** - Removes prohibited files from all git commits using git filter-repo
- **Safety First** - Automatic backups, safe-keeping folders, and .backup files before sanitizing
- **Beautiful CLI** - Spectre.Console powered UI with progress indicators and formatted reports

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [git](https://git-scm.com/downloads) (must be on PATH)
- [git-filter-repo](https://github.com/newren/git-filter-repo) (for history rewriting)
  ```
  pip install git-filter-repo
  ```

## Quick Start

```bash
# 1. Clone this repository
git clone https://github.com/hnslyinnovations/RepoCleaner.git
cd RepoCleaner

# 2. Build
dotnet build

# 3. Run against a target repository (dry-run first!)
dotnet run --project RepoCleaner -- "C:\path\to\your\repo" --dry-run
```

## Usage

```bash
# Scan current directory
RepoCleaner

# Scan a specific repository
RepoCleaner "C:\repos\my-project"

# Dry run (preview only, no changes)
RepoCleaner --dry-run

# Automated cleanup (skip confirmation prompts)
RepoCleaner --yes

# Use custom config file
RepoCleaner --config path/to/config.json

# Combine flags
RepoCleaner "C:\repos\my-project" --dry-run --config myconfig.json
```

### Command Line Options

| Option | Description |
|--------|-------------|
| `path` | Path to the git repository (defaults to current directory) |
| `--dry-run` | Preview mode - scans and reports but makes no changes |
| `--yes` | Skip all confirmation prompts (for CI/automation) |
| `--config` | Path to a custom JSON configuration file |

## Configuration

Copy `config.example.json` to `repocleaner.json` in your working directory to customize settings.

### Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `ProhibitedExtensions` | 50+ extensions | File extensions to flag and remove |
| `ExcludePatterns` | node_modules, .git, bin, obj... | Directories to skip during scanning |
| `MaxFileSizeBytes` | 104857600 (100MB) | Files larger than this are flagged |
| `CredentialScanMaxFileSize` | 10485760 (10MB) | Skip credential scanning for files larger than this |
| `ConfigFilePatterns` | *.config, appsettings*.json... | File patterns to scan for credentials |
| `SafeKeepingFolder` | _repocleaner_removed | Where removed files are moved to |
| `BackupFolder` | _repocleaner_backup | Where full backups are stored |
| `ReportOutputFolder` | _repocleaner_reports | Where scan/cleanup reports are saved |

## How It Works

1. **Scan** - Scans working directory and all git history (all branches, all commits) for prohibited files
2. **Credential Scan** - Scans configuration files for passwords, API keys, tokens, and connection strings
3. **Report** - Displays findings with statistics in a formatted table
4. **Confirm** - Asks for explicit confirmation before any destructive operations
5. **Backup** - Creates a full copy of the repository before making changes
6. **Clean** - Removes prohibited files and sanitizes credentials
7. **Report** - Generates detailed text reports of all findings and actions

## What Gets Detected

### Prohibited File Extensions (50+)
Archives (`.zip`, `.tar`, `.gz`, `.rar`, `.7z`), binaries (`.dll`, `.exe`, `.jar`, `.war`), packages (`.nupkg`, `.whl`), documents (`.pdf`, `.docx`, `.xlsx`), media files, fonts, and more.

### Credential Patterns (20+)
- SQL connection strings with passwords
- Azure Storage account keys
- Azure Service Bus connection strings
- API keys and tokens (Stripe, AWS, generic)
- JWT secrets and bearer tokens
- SMTP passwords
- Client secrets
- Private key headers
- Hardcoded passwords in key-value pairs

## Safety Features

RepoCleaner is designed with multiple safety layers:

1. **Dry-run mode** - Always preview before committing to changes
2. **Full repository backup** - Complete copy created before any modifications
3. **Safe-keeping folder** - Removed files are moved, not deleted
4. **Backup files** - `.backup` copies created before credential sanitization
5. **Explicit confirmation** - Interactive prompts before every destructive action
6. **Never modifies .git directly** - Uses git filter-repo for history operations

## Backup Recommendations

Before using RepoCleaner on any repository:

1. **Create a manual backup** of the entire repository folder (including `.git`)
2. **Push all branches** to a remote as an additional safety net
3. **Run with `--dry-run` first** to review what will be changed
4. **Verify the backup** is complete and accessible before proceeding
5. **Test on a clone** before running on your only copy

```bash
# Example: create a backup before running
xcopy /E /I "C:\repos\my-project" "C:\backups\my-project-backup"

# Or use git clone for a fresh copy
git clone "C:\repos\my-project" "C:\backups\my-project-backup"
```

## Reports

RepoCleaner generates two report files after each run:

- `file_scan_report_<timestamp>.txt` - All prohibited files found (working dir + history)
- `credential_scan_report_<timestamp>.txt` - All credentials detected with file paths and line numbers

Reports are saved to the `_repocleaner_reports` folder (configurable).

## Post-Cleanup Steps

After running RepoCleaner:

1. Verify the repository still builds and tests pass
2. Review generated reports
3. If history was rewritten, force push: `git push --force --all`
4. Notify team members to re-clone (history rewrite invalidates existing clones)
5. **Rotate all detected credentials immediately** - removing them from code does not revoke them

## Building

```bash
cd RepoCleaner
dotnet build
```

## Running from Source

```bash
dotnet run --project RepoCleaner -- [path] [options]
```

## Publishing as a Standalone Tool

```bash
dotnet publish RepoCleaner -c Release -r win-x64 --self-contained
```

## Known Limitations

- Credential scanning uses regex pattern matching - it may produce false positives or miss obfuscated credentials
- git filter-repo must be installed separately (Python dependency)
- History rewriting changes all commit SHAs - all team members must re-clone
- Very large repositories with thousands of commits may take significant time to scan
- Binary file detection is heuristic-based (checks for null bytes)

## License

MIT License - see [LICENSE](LICENSE) for details.

This software is provided without warranty. See the Disclaimer section above.
