# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/) and [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed

- Connecting a large Outlook data file no longer freezes the UI: PST open/list/index work runs off the UI thread, catalog walks the store in short chunks, and folder switches show the local archive without waiting on indexing.
- Large Outlook data files keep readable subjects (MS-PST prefix bytes are stripped) and open by the store item id instead of a subject hash. ANSI `.pst` files are copied once to a Unicode `.postclient.pst` beside the original so encoding stays intact.

### Changed

- Windows setup default install path is `C:\Program Files\MaksIT\Postclient` (manufacturer `MaksIT`, product folder `Postclient`).

## [0.2.0] - 2026-09-14

On-device meaning search, Outlook data files as mailboxes, rules, extra UI languages, and country packs.

### Added

- **Settings → Indices…**: keyword index (FTS) and optional meaning index (RAG) with EmbeddingGemma 300M. The ~300 MB ONNX package downloads from Hugging Face (`onnx-community/embeddinggemma-300m-ONNX`: graph, weights sidecar, tokenizer). GitHub releases do not carry those files. Vectors stay in `mail.db` on this PC. Device Auto / CPU / GPU (Windows DirectML). Rebuild keyword, rebuild meaning, or repair leftover rows.
- **File → Check for updates…** asks GitHub for the latest release and can download the matching package. **File → About Postclient** matches the WVC210 About card (brand, version, credits, email, license, copyright, maks-it.com).
- Attach a `.pst` / `.ost` as its own mailbox (File menu or account type *data file*). Unicode `.pst` is writable (folders, flags, move, delete). An `.ost` or ANSI `.pst` is copied to a Unicode `.pst` on first write. **New data file** creates an empty Unicode `.pst`. Right-click an attached store to **Detach data file**; the file stays on disk. File → Import still copies mail into another account.
- **Settings → Rules**: import Outlook `.rwz` (Rules Wizard) or JSON, export JSON, and edit rules (enable, conditions, move/delete/flag/label). Each rule belongs to an account; a move can target a folder on another mailbox, including an attached data file. A move folder name stays red until it matches that mailbox (name, full path, or last path segment). **Run all rules** applies them to existing mail. Saved in `settings.json`. They also run on Get Messages and on import.
- Compose: **Send attachments as ZIP** packs dropped files into one archive before SMTP. A prompt asks for the ZIP name (from the subject) and an optional password. Attach files by dragging them onto the compose window.
- View → Language: **Français**, **Deutsch**, and **Español** (English and Italian were already there).
- **Settings → Features**: country packs for certified-mail UI (Italy PEC/FatturaPA/fascicolo, Europe eIDAS REM, France/Germany/Spain/Switzerland evidence labels). Ordinary IMAP stays on. Header checkboxes turn a whole pack on or off.

### Fixed

- PEC/REM delivery marks (✓ / ✓✓) on the list after Get Messages. The archive reload dropped `DeliveryStatus`; ricevute and Sent now restore it from `daticert` tipo and the sent-receipt store.
- Search box stays responsive: typing no longer waits for FTS or EmbeddingGemma. Results refresh after a short pause, on a background thread.
- Selecting one or more messages enables Delete, Move, Flag, and mark read/unread again after drag-and-drop or a list refresh. Opening a message no longer locks the toolbar.
- Move and delete drop keyword and meaning rows for those messages; the meaning indexer wakes so destination mail is re-indexed.
- Indices window stays usable while the meaning model downloads; **Download model** is disabled when the files are already in the models folder.
- **Settings → Rules**: **Import rules…** is the same button for this app’s JSON export and for Outlook `.rwz`.

### Changed

- Keyword/body indexing processes all messages in one queue (like the meaning index), not folder by folder. The status bar shows `Indexing N left` instead of a folder name.
- Toolbar layout control is one toggle: list above reading vs three columns.
- Settings menu: **Indices…** (was Semantic search) manages the keyword index and the meaning index together.
- Meaning-model download is Hugging Face only (public onnx-community repo). GitHub is not a source for the weights.
- After SMTP send, Sent reconciles gestore ricevute only for PEC/REM accounts (presets, or IMAP/POP3 with mailbox kind PEC/REM). Ordinary IMAP/Gmail/Outlook just reports sent. Leftover “Waiting for ricevuta” marks from ordinary mailboxes are dropped.
- Account Settings: IMAP/POP3 accounts set **mailbox kind** (ordinary, Italian PEC, EU REM) so a custom-domain gestore still waits for ricevute. Presets keep their kind; Gmail, Outlook, and PST stay ordinary.

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
