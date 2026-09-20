# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/) and [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.3.7] - 2026-09-20

Selected mail keeps its preview while the folder list refreshes.

### Fixed

- Selecting a message keeps that mail and its preview when the folder list refreshes; delete and move use the row's folder, and opening mail is not stuck behind a catalog scan.

## [0.3.6] - 2026-09-20

Folder unread and retention days sit in separate columns. Sign-in and Gmail trash retention survive a rebuild.

### Changed

- Folder tree keeps unread count and retention days in two fixed columns on the right, instead of collapsing them next to the folder name.

### Fixed

- **Empty folder** stays enabled on folders that have mail, including while Get Messages is still running.
- Installing a new build keeps Gmail and Outlook sign-in: leftover secrets and the Identity Hub profile are merged from the previous data folder, and a still-valid mailbox token is used if Hub refresh fails.
- Gmail **[Gmail]/Trash** retention days show on the nested folder and permanently delete mail by received date after Get Messages or **Run now**.

## [0.3.5] - 2026-09-20

Index status covers every mailbox, not one folder or one account.

### Changed

- Status bar keyword and meaning index lines show one done / total across all mailboxes, not the folder or mailbox currently being scanned.

## [0.3.4] - 2026-09-20

Status bar index lines no longer flash between batches.

### Fixed

- Status bar **Indexing** / **Indexing meaning** lines stay put between batches instead of collapsing and flashing.

## [0.3.3] - 2026-09-20

First sync paints mail as it arrives, meaning search can use a discrete GPU, and trash, retention, and logs follow typical desktop mail apps.

### Added

- **Help → Logs** opens a copyable window over the app log folder.

### Changed

- First mailbox sync paints the message list as header batches arrive (newest first). Keyword and meaning indexes wait until the first full folder catalog finishes.
- Default user data is `%LocalAppData%\MaksIT\Postclient`. Existing `%AppData%\Postclient` / `%LocalAppData%\Postclient` trees are moved on first launch.
- Status bar index lines show scanned / total and hide when idle. GPU init failures open the copyable error window and fall back to CPU in Auto.
- Meaning search DirectML prefers a discrete GPU (NVIDIA GTX 1660 Ti and similar) instead of always DXGI adapter 0, and uses sequential DirectML session options.
- Trash retention counts from the move-to-trash time. Folder tree shows retention days next to unread when days are greater than zero.
- Window titles drop menu mnemonics and trailing ellipses.

### Fixed

- Select-all delete in Trash, folder context commands while a huge first sync is painting, and Empty on Trash labeled **Delete all items**.
- Retention list no longer keeps Gmail labels that were deleted on the server.
- FatturaPA toolbar kind button matches the View menu. Source view selection no longer wraps the full raw EML.

## [0.3.2] - 2026-09-19

Expired folder mail goes to Trash; Trash can be kept forever or purged after a number of days.

### Changed

- **Settings → Retention…**: folder days (`0` = keep forever) now **move** older messages to Trash after Get Messages, instead of deleting them permanently. The Trash row uses the same days field: `0` keeps Trash forever, a number of days **permanently deletes** old Trash. The grid shows the action per row.
- Delete (toolbar, **Del**, empty folder, delete rules) moves messages to Trash. If Trash is missing, it is created. Deleting from Trash is still permanent. Local stores honor Trash when emptying a folder.

### Fixed

- Local folder stores no longer skip Trash when emptying a folder (messages go to **Deleted Items**).

## [0.3.1] - 2026-09-19

### Fixed

- Reinstalling **0.3.0** over **0.3.0** left the old exe in `C:\Program Files\MaksIT\Postclient` (Windows setup skips same-version files). This build is **0.3.1** so the installer replaces it. Uninstall 0.3.0 first if a same-version repair still leaves the 16:08 exe.
- The main window opens from the desktop shortcut. Closing it exits the app. The tray icon no longer hides the window on close. A worker process is not started until import or retention needs it.

## [0.3.0] - 2026-09-19

Folder stores replace attached Outlook data files, Gmail/Microsoft sign-in goes through Identity Hub, and folders can expire mail on a schedule.

### Added

- Folder **stores** replace Outlook data files as mailboxes: **File → New store…**, **Attach store…**, **Move store…**, and **Detach store**. Import PST copies into a store under `%LocalAppData%\Postclient\stores\` (or portable `data/stores/`). Each IMAP/POP3 account and each store has its own `mail.db` (FTS + meaning index). Search and move merge or copy across those databases without rebuilding embeddings.
- **Settings → Retention…**: per-folder days (`0` = keep forever). Older mail is deleted permanently (not Trash) after Get Messages.
- IMAP/POP3 usage on the account node in the folder tree. Gmail and Microsoft sign-in go through **Identity Hub** (`https://identity.maks-it.com/desktop-login?provider=`). Hub JWT is stored in `secrets.bin`; IMAP uses a short-lived mailbox token (XOAUTH2). Override the Hub origin with `POSTCLIENT_IDENTITY_HUB`.
- Folder tree expand/collapse for accounts and nested folders is stored in `settings.json`.
- **Shift+click** and **Ctrl+click** select several messages or folders. Delete (including the **Del** key) and drag-move apply to the whole selection.
- Unexpected errors open a dialog with the full stack trace and a **Copy details** button. The same report is written under the app log folder.

### Changed

- Folder tree representation is per provider. Gmail keeps `[Gmail]` labels (`P.IVA` is one folder), infers the `[Gmail]` mailbox, and hides All Mail / Starred / Important. PEC and classic IMAP lift `INBOX.Drafts` / Sent / Trash next to Inbox and do not use Gmail label rules.
- Status bar **Index** and **Meaning** lines are empty when idle (progress and errors only). Quota left the status bar. **Help** holds Check for updates and About; File keeps Exit.
- Attached `.pst` accounts migrate once to folder stores (same mailbox id).
- Windows setup default install path is `C:\Program Files\MaksIT\Postclient` (manufacturer `MaksIT`, product folder `Postclient`).

### Fixed

- PEC/IMAP Drafts, Sent, Trash, and other standard folders named `INBOX.Drafts` (and similar) show next to Inbox, not nested under it. User folders such as `INBOX.Clients` stay under Inbox.
- Gmail labels with a dot in the name (for example `P.IVA`) stay a single folder. Hierarchy follows the IMAP delimiter (`/` on Gmail, `.` only under `INBOX.` on classic servers).
- Hotmail / Outlook.com IMAP inboxes list messages again: FLAGS-only FETCH rows no longer replace envelopes, Outlook skips PEC extra-header FETCH, and Inbox opens via the special INBOX mailbox.
- Gmail and Hotmail/Outlook OAuth stay signed in after restart: Hub login waits for the refresh token (not a JWT-only snapshot), expiry is stored as UTC, connect refreshes the Hub session before requesting a mailbox token, and saving a Gmail/Outlook account no longer rewrites auth to password.
- Identity Hub Google/Microsoft sign-in now picks up the Hub session after the callback page finishes (the WebView no longer waits for you to close a tab). Save Account then stores the Hub tokens.
- Microsoft Identity Hub sign-in in the embedded WebView: Entra popups no longer replace the Hub page, script polling stays on `identity.maks-it.com`, and login uses a separate WebView2 profile from HTML mail. Sign-in now checks that Hub can issue a mailbox token.
- Identity Hub sign-in opens `identity.maks-it.com` after the WebView adapter is ready (the popup no longer stays on `about:blank`). Google/Microsoft redirects stay in that view; Hub login uses its own WebView2 data folder so it does not clash with HTML mail. The sign-in dialog is owned by Account Settings when that window is open.
- Connecting a large Outlook data file no longer freezes the UI: PST open/list/index work runs off the UI thread, catalog walks the store in short chunks, and folder switches show the local archive without waiting on indexing.
- Large Outlook data files keep readable subjects (MS-PST prefix bytes are stripped) and open by the store item id instead of a subject hash. ANSI `.pst` files are copied once to a Unicode `.postclient.pst` beside the original so encoding stays intact.

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
