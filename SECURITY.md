# Security Policy

## Overview

BusLane takes security seriously. This document outlines our security practices and how to report vulnerabilities.

## Supported Versions

We provide security updates for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 0.4.x   | :white_check_mark: |
| < 0.4   | :x:                |

## Security Features

### Connection String Encryption

BusLane encrypts all saved Azure Service Bus connection strings using industry-standard encryption:

- **Algorithm**: AES-256-CBC encryption
- **Key Derivation**: PBKDF2 with SHA-256 (100,000 iterations)
- **Machine-Specific Keys**: Encryption keys are derived from machine-specific entropy (machine name, username, user profile path)
- **Random IVs**: Each encryption operation uses a unique, randomly generated initialization vector
- **Storage Location**: Encrypted connection strings are stored in `%APPDATA%/BusLane/connections.json` (Windows) or `~/.config/BusLane/connections.json` (macOS/Linux)

This means:
- ✅ Connection strings are never stored in plaintext
- ✅ Encrypted data cannot be transferred between machines and decrypted
- ✅ Each encryption operation produces different ciphertext (even for the same input)
- ✅ Data is protected from unauthorized access on the local machine

### Azure Token Caching

When using Azure authentication:
- **Secure Token Cache**: Azure Identity SDK stores authentication tokens in the system's secure credential store
- **Windows**: Uses Windows Credential Manager (DPAPI encryption)
- **macOS**: Uses Keychain
- **Linux**: Uses GNOME Keyring or KDE Wallet when available
- **Session Persistence**: Tokens are cached to avoid repeated authentication prompts

### No Secrets in Code

- ✅ No API keys, passwords, or connection strings are hardcoded in the source code
- ✅ No secrets are committed to the repository
- ✅ GitHub Actions workflows only use standard `GITHUB_TOKEN` (scoped and time-limited)

### UI Protection

- **Password Masking**: Connection strings are displayed with bullet characters (●) in the UI
- **Validation**: Connection strings are validated before being saved
- **Secure Input**: Sensitive data entry fields use secure input controls

## Best Practices for Users

### Connection String Security

1. **Never share connection strings** - They contain credentials to access your Azure Service Bus
2. **Use Azure RBAC** - When possible, use Azure authentication instead of connection strings
3. **Rotate keys regularly** - If you must use connection strings, rotate the keys periodically in Azure Portal
4. **Use least privilege** - Create connection strings with minimal required permissions:
   - Read-only for viewing messages
   - Send permissions only when needed
   - Manage permissions only for administrative tasks

### Azure Authentication

1. **Preferred Method** - Use Azure authentication with your Microsoft account when possible
2. **MFA Protection** - Enable multi-factor authentication on your Azure account
3. **RBAC Roles** - Ensure your account has only the necessary permissions:
   - `Azure Service Bus Data Receiver` - to peek/receive messages
   - `Azure Service Bus Data Sender` - to send messages
   - `Reader` - to browse namespaces, queues, and topics

### Application Security

1. **Keep Updated** - Always use the latest version of BusLane
2. **Verify Downloads** - Download releases only from the official GitHub repository
3. **macOS Users** - If you see a "damaged" warning, run: `xattr -cr "/Applications/Bus Lane.app"`
4. **File Permissions** - Ensure your user directory has appropriate permissions
5. **Shared Computers** - Don't use saved connections on shared or public computers

## Reporting a Vulnerability

We take all security vulnerabilities seriously. If you discover a security issue, please follow responsible disclosure:

### How to Report

**Please DO NOT open a public GitHub issue for security vulnerabilities.**

Instead, please report security vulnerabilities by:

1. **Email**: Send details to [tomek.solik@gmail.com](mailto:tomek.solik@gmail.com)
2. **Subject Line**: Use "BusLane Security Vulnerability" in the subject
3. **Include**:
   - Description of the vulnerability
   - Steps to reproduce the issue
   - Potential impact
   - Any suggested fixes (if you have them)
   - Your contact information for follow-up

### What to Expect

- **Acknowledgment**: We will acknowledge receipt of your report within 48 hours
- **Assessment**: We will assess the vulnerability and determine its severity
- **Updates**: We will keep you informed of our progress
- **Fix Timeline**: We aim to release security fixes within 30 days for high-severity issues
- **Credit**: We will credit you for the discovery (unless you prefer to remain anonymous)
- **Coordinated Disclosure**: We will work with you on a coordinated disclosure timeline

### Severity Levels

We classify vulnerabilities using the following severity levels:

- **Critical**: Remote code execution, full system compromise, or exposure of all user credentials
- **High**: Unauthorized access to sensitive data, privilege escalation, or authentication bypass
- **Medium**: Information disclosure, denial of service, or limited data exposure
- **Low**: Minor information leakage or issues with minimal security impact

## Security Updates

Security updates will be:
- Released as patch versions (e.g., 0.4.6)
- Documented in the CHANGELOG with severity level
- Announced in GitHub Releases
- Tagged with the `security` label

## Out of Scope

The following are considered out of scope for security vulnerabilities:

- Issues requiring physical access to an unlocked machine
- Vulnerabilities in third-party dependencies (report these to the respective projects)
- Social engineering attacks
- Denial of service via excessive API calls to Azure (rate-limited by Azure)

## Secure Development Practices

Our development process includes:

- **Code Review**: All changes are reviewed before merging
- **Dependency Management**: Regular updates of dependencies via Dependabot
- **Static Analysis**: Automated security scanning of code
- **No Secrets in Repo**: Automated checks to prevent committing secrets
- **.gitignore**: Comprehensive exclusion of sensitive file patterns

## Questions?

If you have questions about security that are not sensitive in nature, feel free to:
- Open a GitHub Discussion
- Open a non-security-related GitHub Issue

For sensitive security concerns, always use the private reporting method described above.

---

**Last Updated**: December 2025  
**Version**: 1.0
