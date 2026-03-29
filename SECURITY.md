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

### App Lock And Recovery Code

BusLane also supports an optional app-access lock for the application shell itself:

- **Scope**: App lock is an opt-in access gate that runs when BusLane launches
- **No Encryption Model Change**: Enabling app lock does not re-encrypt saved connections or change the existing data-at-rest encryption model
- **Password Storage**: The app-lock password is stored only as a salted PBKDF2-SHA256 hash in `%APPDATA%/BusLane/app-lock.json` (Windows) or `~/.config/BusLane/app-lock.json` (macOS/Linux)
- **Recovery Code Storage**: Recovery codes are generated once per enable or regeneration event, shown once to the user, and then stored only as a salted hash
- **Secure File Permissions**: The app-lock file is written with owner-only permissions where the host platform supports them
- **Launch-Only Lock**: This feature does not currently add inactivity relock or a manual lock command

This means:
- ✅ BusLane never stores the app-lock password or recovery code in plaintext
- ✅ Recovery codes can reset the password or disable app lock for future launches if the password is forgotten
- ✅ A compromised `app-lock.json` file does not reveal the original password or recovery code

### Biometric Unlock

After a password has been set, BusLane can optionally use platform biometrics as an unlock shortcut:

- **macOS**: Touch ID via the system LocalAuthentication prompt when available
- **Windows**: Windows Hello when the device and OS report support
- **Linux**: No biometric integration; password and recovery code remain the supported path
- **Fallback Behavior**: If Windows Hello is unavailable or unsupported, BusLane falls back to password plus recovery code without blocking app access permanently

Biometric unlock is a convenience feature, not a replacement for the app-lock password. Sensitive security changes in Settings still require re-authentication with the current password or an available biometric prompt.

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
- **Blocking Lock Overlay**: When app lock is enabled, BusLane blocks shell interaction until unlock succeeds
- **Shortcut Guarding**: Global shortcuts and escape-driven modal dismissal are disabled while the app is locked

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
3. **macOS Users** - If you see a "damaged" warning, run: `xattr -cr "/Applications/BusLane.app"`
4. **File Permissions** - Ensure your user directory has appropriate permissions
5. **Shared Computers** - Don't use saved connections on shared or public computers
6. **Store Recovery Codes Separately** - Keep the recovery code outside the app and avoid storing it alongside the BusLane data folder

---

**Last Updated**: March 2026  
**Version**: 1.0
