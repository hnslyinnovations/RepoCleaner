# RepoCleaner - GitHub Migration Tool

A .NET 8.0 console application that scans and cleans git repositories for prohibited files and credentials before migrating from Azure DevOps to GitHub.

## Features

- **File Scanning** - Detects prohibited file extensions in working directory and git history
- **Credential Scanning** - Finds passwords, API keys, tokens, and connection strings in config files
- **Interactive Workflow** - Guided prompts with confirmation before destructive operations
- **History Rewriting** - Removes prohibited files from all git commits using git filter-repo
- **Safety First** - Automatic backups, safe-keeping folders, and .backup files before sanitizing
- **Beautiful CLI** - Spectre.Console powered UI with progress indicators and formatted reports

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [git-filter-repo](https://github.com/newren/git-filter-repo) (for history rewriting)
  ```
  pip install git-filter-repo
  ```

## Usage

```bash
# Scan current directory
RepoCleaner

# Scan a specific repository
RepoCleaner "C:\repos\my-project"

# Dry run (preview only, no changes)
RepoCleaner --dry-run

# Automated cleanup (skip prompts)
RepoCleaner --yes

# Use custom config
RepoCleaner --config path/to/config.json
```

## Configuration

Copy `config.example.json` to `repocleaner.json` in your working directory to customize:

- Prohibited file extensions
- Exclude patterns (directories to skip)
- File size limits
- Config file patterns for credential scanning

## How It Works

1. **Scan** - Scans working directory and git history for prohibited files and credentials
2. **Report** - Displays findings with statistics
3. **Confirm** - Asks for confirmation before making changes
4. **Backup** - Creates full repository backup
5. **Clean** - Removes files and sanitizes credentials
6. **Report** - Generates detailed reports of all actions taken

## Building

```bash
cd RepoCleaner
dotnet build
```

## Running

```bash
dotnet run --project RepoCleaner/RepoCleaner.csproj -- [path] [options]
```
