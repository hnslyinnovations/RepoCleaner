# Security Policy

## Important Notice

RepoCleaner is designed to help detect and remove credentials from repositories. However:

- **This tool does NOT revoke or rotate credentials.** Any credentials found in your repository history should be considered compromised and must be rotated immediately, regardless of whether they have been removed from the code.

- **Credential detection is pattern-based.** It may miss credentials that don't match known patterns, or flag false positives. Do not rely solely on this tool for security audits.

- **Git history is permanent until rewritten.** Even after removing files from the working directory, they remain accessible in git history. Anyone who has cloned or forked the repository may still have access to the original credentials.

## After Running RepoCleaner

1. Rotate ALL detected credentials (passwords, API keys, tokens, connection strings)
2. Revoke any access tokens or service principals that were exposed
3. Audit access logs for any services whose credentials were found
4. Update all environments (dev, staging, production) with new credentials
5. Consider enabling secret scanning on your GitHub organization

## Reporting Security Issues

If you discover a security vulnerability in RepoCleaner itself, please report it responsibly by opening a private issue or contacting the maintainer directly. Do not open a public issue for security vulnerabilities.
