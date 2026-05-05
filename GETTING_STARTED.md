# Getting Started with RepoCleaner

This guide walks you through your first run of RepoCleaner.

## Step 1: Install Prerequisites

### .NET 8.0 SDK
Download and install from: https://dotnet.microsoft.com/download/dotnet/8.0

Verify installation:
```bash
dotnet --version
```

### git-filter-repo
Required only if you need to rewrite git history (remove files from past commits).

```bash
pip install git-filter-repo
```

Verify installation:
```bash
git filter-repo --version
```

## Step 2: Build RepoCleaner

```bash
git clone https://github.com/hnslyinnovations/RepoCleaner.git
cd RepoCleaner
dotnet build
```

## Step 3: Back Up Your Repository

**This is not optional.** Always back up before running RepoCleaner.

```bash
# Option A: Copy the entire folder
xcopy /E /I "C:\repos\my-project" "D:\backups\my-project-pre-clean"

# Option B: Clone a fresh copy to work on
git clone "C:\repos\my-project" "C:\repos\my-project-cleaning"
```

## Step 4: Dry Run

Always start with a dry run to see what RepoCleaner will find:

```bash
dotnet run --project RepoCleaner/RepoCleaner.csproj -- "C:\repos\my-project" --dry-run
```

Review the output carefully. Check:
- Are the detected files actually prohibited? (no false positives on files you need)
- Are the credential detections accurate? (check for false positives)
- Is anything being missed that you expected to find?

## Step 5: Customize Configuration (Optional)

If the defaults don't fit your needs, create a config file:

```bash
copy RepoCleaner\config.example.json repocleaner.json
```

Edit `repocleaner.json` to:
- Add or remove prohibited extensions
- Adjust the file size limit
- Add exclude patterns for directories you want to skip
- Add config file patterns for credential scanning

## Step 6: Run the Cleanup

Once you're satisfied with the dry-run results:

```bash
dotnet run --project RepoCleaner/RepoCleaner.csproj -- "C:\repos\my-project"
```

RepoCleaner will:
1. Scan and display results
2. Ask if you want to remove prohibited files from history
3. Ask if you want to sanitize credentials
4. Ask for final confirmation
5. Create a backup
6. Perform the cleanup
7. Generate reports

## Step 7: Verify

After cleanup:
```bash
cd C:\repos\my-project
dotnet build          # or your project's build command
dotnet test           # run your tests
```

## Step 8: Push to GitHub

If history was rewritten:
```bash
git push --force --all
git push --force --tags
```

If only working directory files were removed:
```bash
git add -A
git commit -m "Remove prohibited files for GitHub migration"
git push
```

## Step 9: Rotate Credentials

**Do this immediately after cleanup.** Any credentials that were in your repository should be considered compromised:

- Change database passwords
- Regenerate API keys
- Rotate Azure/AWS access keys
- Revoke and recreate service tokens
- Update all deployment configurations with new values

## Troubleshooting

### "Not a valid git repository"
Make sure you're pointing to the root of a git repository (the folder containing `.git`).

### "git filter-repo failed"
- Ensure git-filter-repo is installed: `pip install git-filter-repo`
- Ensure git is on your PATH: `git --version`
- The repository must have at least one commit

### Scan takes a long time
Large repositories with many commits take longer to scan. The tool deduplicates blobs across commits, but initial traversal still requires reading all commit trees. Consider using exclude patterns to skip known-safe directories.

### False positives in credential scanning
Edit your `repocleaner.json` to add exclude patterns for directories or files that contain test data or documentation with example credentials.
