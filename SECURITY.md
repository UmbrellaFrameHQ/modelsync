# Security Policy

## Supported Versions

| Version | Supported |
|---|---|
| 1.x (latest) | Yes |

## Reporting a Vulnerability

Please do not report security vulnerabilities through public GitHub Issues.

To report a security issue, email us at: **security@umbrellaframe.dev**

Include:
- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Any suggested fix

We will respond within 72 hours and aim to release a patch within 7 days for confirmed critical issues.

## Scope

This policy covers:
- The core SQL generation engine
- All provider packages
- The Roslyn Analyzer package
- The optional ModelSync Notes extension and VSIX packaging

Out of scope:
- Vulnerabilities in third-party dependencies
- Issues in documentation

## Notes Extension Boundary

ModelSync Notes stores developer notes in a solution-local JSON file. The extension enforces owner-only edit/delete in the UI and service layer, but local JSON storage is not a tamper-proof audit system. Anyone with filesystem write access can modify `.modelsync/notes.json`.

Use a backend service with authenticated users and server-side authorization if your organization needs regulated audit history or strong identity enforcement.
