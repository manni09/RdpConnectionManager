# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in RDP Connection Manager, please report it responsibly:

1. **Do not** open a public GitHub issue for security vulnerabilities
2. Send details to the repository maintainer via private message or email
3. Include steps to reproduce the vulnerability
4. Allow reasonable time for a fix before public disclosure

## Security Practices

### Credential Storage

- **Passwords are encrypted** using Windows Data Protection API (DPAPI) with `CurrentUser` scope
- Encrypted passwords are tied to your Windows user account and cannot be decrypted by other users
- Credentials are stored in Windows Credential Manager using the `TERMSRV/hostname` format for seamless RDP authentication

### Data Storage

- Settings are stored locally in `%LOCALAPPDATA%\RdpManager\settings.json`
- No data is transmitted to external servers
- No telemetry or analytics are collected

### Export Security

- Exported JSON files can optionally include encrypted passwords
- Encrypted passwords in exports are DPAPI-protected and only work on the same Windows user account
- For sharing connections between users/machines, export **without** passwords

### RDP Connection Security

- Uses the native Windows RDP ActiveX control (mstscax.dll)
- All security features depend on the underlying Windows RDP implementation
- Supports Network Level Authentication (NLA) when the server requires it
- TLS/SSL encryption is handled by the Windows RDP stack

## Best Practices

1. **Use strong, unique passwords** for each remote connection
2. **Enable NLA** on your RDP servers when possible
3. **Don't export with passwords** when sharing connection files
4. **Restrict RDP access** to trusted networks or use VPN
5. **Keep Windows updated** for the latest RDP security patches

## Disclaimer

This software stores credentials locally using Windows security features. While we follow security best practices, no software is completely immune to vulnerabilities. Use at your own risk and follow your organization's security policies.
