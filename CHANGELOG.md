# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/) and [Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-09-13

First release of Postclient: a desktop mail client for Windows, Linux, and macOS (Apple Silicon and Intel).

### Added

- Avalonia / .NET 10 client with English and Italian UI, list-above-reading or three-column layout, HTML (OS web engine), plain text, raw source, and FatturaPA views.
- IMAP (full folder tree), POP3 (Inbox only), and SMTP; encryption Auto, SSL/TLS, or STARTTLS; several mailboxes at once.
- Provider presets: Gmail and Microsoft 365 (OAuth2 / XOAUTH2), Italian PEC (Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero), Intesi Group, and generic IMAP/POP3.
- Passwords and OAuth refresh tokens stored next to settings (`DPAPI` on Windows, file mode `600` on Linux/macOS), never in `settings.json`.
- Italian PEC: unwrap `postacert.eml` / `daticert.xml`; Type column PEC/RIC; delivery from gestore ricevute (`accettazione`, `avvenuta-consegna`, failures). Avvenuta consegna is deposit in the certified mailbox, not a read receipt.
- eIDAS REM: ETSI evidence mapped onto the same delivery column (accepted, delivered, retrieved, failed). S/MIME is shown as digitally signed, not as PEC or REM.
- After SMTP send, Sent reconciles gestore ricevute by Message-Id, `daticert.xml`, and subject prefixes. Desktop notices for new PEC, ricevute, and REM.
- Local SQLite archive of the whole IMAP folder (not a last-N window), FTS of subject, body, attachment text, and practice labels; export copies `mail.db` and `.eml` on this PC only.
- Practice labels on this machine; IMAP QUOTA from the gestore in the status bar; From picker bound to a connected mailbox.
- Conversation grouping in the folder list (Message-ID / In-Reply-To / References, indent, no conversation pane). Date column shows local date and time.
- Print / PDF / selected `.eml` fascicolo; save all attachments as ZIP; FatturaPA XML preview (parser, not AI).
- Import EML, Thunderbird mbox, and Outlook PST/OST (no Outlook install).
- Self-contained `win-x64` portable zip and Windows setup exe, Linux Flatpak via WSL Debian (no .NET SDK on the user PC).
- Apple-style squircle product icon (envelope glyph, MAKS.IT cyan→navy) for the exe, window chrome, tray, WiX setup, and Flatpak. Not the brand origami M.
- Unsigned `osx-arm64` and `osx-x64` DMGs attached when a GitHub Release is published (first launch: Open from the context menu).
