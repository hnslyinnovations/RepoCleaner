# Changelog

All notable changes to RepoCleaner will be documented in this file.

## [1.0.0] - 2025-05-04

### Added
- Initial release
- File scanning for 50+ prohibited extensions in working directory and git history
- Credential scanning with 20+ regex patterns (connection strings, API keys, tokens, passwords)
- Interactive workflow with confirmation prompts
- `--dry-run` flag for preview mode
- `--yes` flag for automated/CI usage
- `--config` flag for custom configuration files
- Full repository backup before any destructive operations
- Safe-keeping folder for removed files (moved, not deleted)
- `.backup` file creation before credential sanitization
- Git history rewriting via git filter-repo
- Spectre.Console UI with progress spinners and formatted tables
- Report generation (file scan + credential scan reports)
- Support for bare and regular git repositories
- Configurable prohibited extensions, exclude patterns, and size limits
- Multi-branch scanning (all local branches)
- Blob deduplication for efficient history scanning
