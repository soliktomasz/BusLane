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

---

**Last Updated**: December 2025  
**Version**: 1.0
