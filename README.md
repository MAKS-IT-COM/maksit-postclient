# Postclient — desktop PEC, REM, and IMAP mail client

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-56.5%25-yellowgreen)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-43.8%25-yellowgreen)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-57.1%25-yellowgreen)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Language:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

The application UI is English, Italian, French, German, or Spanish. These documents describe the same open-source **desktop email client** for people who send and keep **posta elettronica certificata (PEC)**, **eIDAS registered electronic mail (REM)**, and ordinary **IMAP / POP3 / SMTP** mail on Windows, Linux, and macOS.

Postclient is a **local mail client**, not a web mailbox. It talks IMAP, POP3, and SMTP to accounts you already have: Gmail, Microsoft 365, Italian **PEC** at a qualified gestore (Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero, and custom-domain IMAP), and European **REM** when the operator publishes IMAP/SMTP. Receipts stay on this PC as `.eml` plus a searchable SQLite archive.

It is **not** a qualified trust service provider (QTSP), **not** a PEC operator, **not** a timestamp authority, and **not** hosted mail. The gestore / QTSP issues envelopes and ricevute. This client unwraps them, shows delivery evidence, and keeps the originals on the machine.

| | |
|--|--|
| Kind | Desktop mail client for PEC, REM, and IMAP |
| OS | Windows, Linux, macOS (Apple Silicon and Intel) |
| Stack | C#, .NET 10, Avalonia, MailKit |
| License | [Apache 2.0](LICENSE.md) |
| Not | A QTSP, a new PEC/REM address, cloud mail, or a scanner |

Changes: [CHANGELOG.md](CHANGELOG.md). Contributing: [CONTRIBUTING.md](CONTRIBUTING.md).

## Built for certified mail on the desktop

Typical desktop and web mail treats a PEC **busta** or an ETSI REM evidence part as just another attachment. Postclient is written around that evidence:

- **Unwrap PEC / REM envelopes** — read the inner `postacert.eml` (the message the recipient was meant to see) instead of the gestore wrapper; Type column **PEC**, **RIC**, **REM**, or digitally signed **SIG**.
- **Delivery column from operator evidence** — accettazione, avvenuta-consegna, ETSI `SubmissionAcceptance` / `Delivery` / `Retrieval`, LRE and AR24 avis, De-Mail Zustellung and Abholbestätigung, acuse de recibo. After SMTP send, **Sent** is matched to Inbox/Ricevute ricevute by Message-Id, `daticert.xml`, and subject prefixes. Italian PEC has **no read receipt**: avvenuta consegna is deposit in the certified mailbox.
- **FatturaPA preview** — Italian e-invoice XML as a readable view (XML parser, not generative AI).
- **Fascicolo of original `.eml`** — export selected messages as the files a notaio, PA desk, bank, or court can keep.
- **From is a mailbox picker** — you cannot type a free From; send uses the SMTP identity of the connected account.
- **Practice labels on this PC** — tag a condominium, a client file, an IMU notice. Labels are local, not a studio-wide database.
- **Full-folder local archive** — Get Messages stores the **whole IMAP folder** in `mail.db` plus `.eml` on disk (not a last-N window). Keyword FTS on subject, body, PDF/XML/HTML/CSV/JSON attachment text, and labels.
- **On-device meaning search (RAG)** — optional EmbeddingGemma 300M in **Settings → Indices…**. Vectors stay in `mail.db`. Mail never leaves the machine. Rebuild or repair keyword and meaning indices.
- **Rules across mailboxes** — move, delete, flag, label; a destination folder can live on another account, including an attached `.pst` / `.ost` data file. Import JSON or a legacy `.rwz` file; export is JSON.
- **Open `.pst` / `.ost` as a mailbox** — Unicode personal store is writable (folders, flags, move, delete). An offline store is copied to a Unicode `.pst` on first write. No extra mail program required to read the file.
- **Country packs** — **Settings → Features** turns Italy (PEC, FatturaPA, fascicolo), Europe (eIDAS REM), France, Germany, Spain, and Switzerland evidence labels on or off. Ordinary IMAP stays available.

## Who it is for

### Private individuals

Daily client for one or more addresses: Gmail or Microsoft 365 next to a personal **PEC** (Italy) or an IMAP **REM** mailbox (EU). Search a condominium notice, a tax file, or a PA communication years later without leaving the originals on someone else’s server.

Typical use: certified and ordinary mail in one window; see whether the gestore accepted and deposited a PEC; print or save a PDF; export the original `.eml` if a municipality, bank, or court asks for it.

### Practices and agencies

Use it when certified identities must not be mixed: personal PEC, studio PEC, ordinary mail. A commercialista, avvocato, notaio, CAF, condominium administrator, or EU practice that files with PA can keep each gestore mailbox separate (IMAP **QUOTA** in the status bar), group a send with its ricevute, and export a fascicolo without uploading the practice to a third-party archive.

The program does not replace the gestore, does not issue a qualified ricevuta, and does not share labels across office PCs.

## Accounts and protocols

Incoming: **IMAP** (full folder tree), **POP3** (Inbox only), or an attached **`.pst` / `.ost`**. Unicode `.pst` is writable. Outgoing: **SMTP**. Encryption: Auto, SSL/TLS, STARTTLS, STARTTLS if available, or none. MailKit uses the SASL methods the server advertises.

Several mailboxes at once. Presets fill hosts and ports:

| Preset | Role |
|--|--|
| Gmail | IMAP/SMTP; Google sign-in (OAuth2 / XOAUTH2) or a Gmail app password |
| Microsoft 365 | IMAP/SMTP; Microsoft sign-in. Most of these accounts reject a mailbox password |
| IT PEC — Aruba | `imaps.pec.aruba.it` / `smtps.pec.aruba.it` (POP3: `pop3s.pec.aruba.it`) |
| IT PEC — InfoCert Legalmail | `mbox.cert.legalmail.it` / `sendm.cert.legalmail.it`. Username is often the InfoCert User ID, not the address |
| IT/EU PEC — Namirial | `imaps.sicurezzapostale.it` / `smtps.sicurezzapostale.it` |
| IT PEC — Poste Italiane | `mail.postecert.it` |
| IT PEC — Register.it | `imap.pec-email.com` / `smtp.pec-email.com` |
| IT PEC — Libero | `mail.postacert.it.net` |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Any host the operator gave you. Set **mailbox kind** to Italian PEC or EU REM if that mailbox is certified (including a custom domain) |
| Data file (`.pst` / `.ost`) | Local store opened as a mailbox. Unicode `.pst` is writable; `.ost` / ANSI `.pst` is copied to a Unicode `.pst` on first write. No password. |

Certified IMAP mailboxes use the gestore password, or an app password if the gestore has 2FA. **AR24** (France), **De-Mail** (Germany), **IncaMail** (Switzerland), and **Lleida** (Spain) are usually not IMAP. If that operator gave you IMAP/POP3 hosts, choose **IMAP / POP3**. Otherwise this client cannot open that mailbox; it can still display ETSI REM evidence on messages you import as `.eml`.

OAuth needs a Google client ID (ends with `.apps.googleusercontent.com`) and, for **Web** clients, the **client secret** — paste both in Account Settings (the secret is stored in `secrets.bin`, not as the mailbox password). Desktop public clients need no secret. Environment variables `POSTCLIENT_GOOGLE_CLIENT_ID` / `POSTCLIENT_GOOGLE_CLIENT_SECRET` are also accepted. Enable the **Gmail API**, add scope `https://mail.google.com/` on the consent screen, allow loopback `http://127.0.0.1`, and enable **IMAP** in Gmail. Microsoft: Azure Application (client) ID (a GUID); public client; redirect `http://localhost`; IMAP/SMTP permissions.

Passwords and OAuth refresh tokens stay next to settings (`DPAPI` on Windows, file mode `600` on Linux/macOS), never in `settings.json`.

## Italian PEC (posta elettronica certificata)

Italian certified mail is a **busta** produced by the gestore:

- transport message with `X-Trasporto: posta-certificata`, `daticert.xml`, and the original as `postacert.eml`;
- ricevute (`X-Ricevuta`) in **Inbox** and **Ricevute**: accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, rilevazione-virus, preavviso-errore-consegna.

View → **Unwrap PEC/REM envelope** shows the inner `postacert.eml`. The Type column marks **PEC** (transport) or **RIC** (ricevuta).

Italian PEC has **no read receipt**. **Avvenuta consegna** means the message was deposited in the recipient’s certified mailbox, not that a person opened it.

## European REM (eIDAS registered electronic mail)

When the message carries ETSI REM evidence (`REMEvidence` / `urn:etsi:rem`, or a part name such as `remevidence` / `rem-md`), the client treats it as **REM**. Event types map onto the same delivery column as PEC:

- `SubmissionAcceptance` → accepted by the QTSP;
- `Delivery` / LRE or AR24 avis de réception / De-Mail Zustellung / acuse de recibo → deposited;
- `ContentConsignment` / `Retrieval` / De-Mail `Abholbestätigung` → the recipient retrieved the content (closest European equivalent of “read”);
- rejection, virus, non-delivery, `DeliveryExpiration` → failed.

S/MIME (`smime.p7s` / `smime.p7m`) is shown as digitally signed; it is not PEC and not REM.

## Delivery column after send

After SMTP send, Postclient stores the outbound **Message-Id** and, when you open **Sent**, reconciles gestore ricevute from **Inbox** and **Ricevute**. That wait applies only to accounts whose kind is PEC or REM (a PEC/REM preset, or IMAP/POP3 with that mailbox kind). Ordinary Gmail, Microsoft 365, and IMAP just report as sent. Matching uses, in order: `daticert.xml` `<msgid>`, `X-Riferimento-Message-ID`, `In-Reply-To`, PEC `identificativo`, then subject prefixes (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

| Evidence | Mark | Meaning |
|--|--|--|
| PEC `accettazione` / `presa-in-carico`, ETSI `SubmissionAcceptance` | Accepted | The gestore took the message |
| PEC `avvenuta-consegna`, ETSI `Delivery`, LRE/AR24 avis, De-Mail Zustellung | Delivered | Deposited in the recipient **certified mailbox** — Italian PEC has **no read receipt** |
| ETSI `ContentConsignment` / `Retrieval`, De-Mail `Abholbestätigung` | Retrieved | Recipient picked up the content |
| `non-accettazione`, `mancata-consegna`, virus, ETSI rejection | Failed | Gestore reported non-acceptance or failed delivery |

This client **shows** operator evidence. It does not issue a qualified ricevuta, PEC envelope, or timestamp.

Desktop notices fire for new PEC, ricevute, and REM (Windows toast, macOS notification, Linux `notify-send`).

## Reading, folders, compose

- Folder tree follows the server (Inbox, Ricevute/Receipts, Drafts, Sent, Archive, Junk, Trash, plus custom folders). System folders cannot be deleted. You can create a folder, empty one (to Trash, or permanently in Trash), mark all read/unread, delete a custom folder, and drag messages onto a folder.
- List columns: unread, flag, attachments, delivery, type (PEC / RIC / REM / SIG), from, subject, date (local `yyyy-MM-dd HH:mm`), practice label.
- View: HTML (OS web engine), plain text, raw source, or **FatturaPA**. Layout: list above reading, or three columns (folders, list, reading).
- **Group conversations** indents replies in the folder list using Message-ID / In-Reply-To / References (depth up to 8). PEC ricevute that share the same original sit together. There is no separate conversation pane.
- Message actions: reply, reply all, forward, open in a new window, read/unread, flag, priority, delete.
- Compose is **plain text**. To, Cc, Bcc are address chips. Attachments are listed on the composer. **Send attachments as ZIP** packs dropped files (optional password). Send uses the selected mailbox’s SMTP identity only.

HTML uses the OS engine: **WebView2** on Windows (profile under the data folder), **WebKitGTK** on Linux, **WKWebView** on macOS. If the engine is missing, the text body is shown instead.

Linux packages for the HTML view:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora: `gtk3 webkit2gtk4.1 libsoup3`.

## Local archive, keyword search, and meaning index

Get Messages indexes the **whole IMAP folder** into a local SQLite store (`mail.db`) plus `.eml` objects on disk. POP3 only fills Inbox. Indexing runs in the background; the status bar shows progress.

- **Settings → Indices…**: keyword index (FTS) plus optional EmbeddingGemma 300M (~300 MB) meaning index. The ONNX package downloads from Hugging Face (`onnx-community/embeddinggemma-300m-ONNX`). Vectors stay in `mail.db` on this PC. Device: Auto / CPU / GPU (Windows DirectML). Rebuild or repair if search looks wrong. Weights use Google’s Gemma Terms.

Search (current folder) covers subject, body, attachment text (PDF and text/XML/HTML/CSV/JSON), practice labels, and meaning when the model is ready. Labels exist only on this PC.

The archive is **only on this PC**. File → Open archive folder / Export archive copies `mail.db` and the `.eml` files. Paths:

| OS | Config | Data |
|--|--|--|
| Windows | `%AppData%\Postclient\` | `%LocalAppData%\Postclient\` |
| Linux | `~/.config/postclient/` | `~/.local/share/postclient/` |
| macOS | `~/Library/Application Support/Postclient/` | same |

Override with `POSTCLIENT_CONFIG` / `POSTCLIENT_DATA_DIR`. A portable Windows zip that contains `settings.json` with `installType: portable` keeps config next to the executable.

IMAP **QUOTA** from the gestore (when the server supports it) is shown in the status bar. That is the operator mailbox, not cloud storage from this application.

## Import, export, print

| Action | What you get |
|--|--|
| Import EML | Originals into the current folder |
| Import mbox | Local mbox stores from a desktop mail profile |
| Import `.pst` / `.ost` | Copies mail from the data file into the **selected** IMAP/POP3 account. Nested stores inside the file are imported too. Close any program that has the file open. |
| Attach data file | Opens a `.pst` / `.ost` as its own mailbox. Unicode `.pst` can be written. `.ost` cannot be written in place — the first change copies it to a Unicode `.pst` beside the original. File menu or account type *data file*. |
| New data file | File menu: creates an empty Unicode `.pst` (Inbox, Drafts, Sent, Deleted) and attaches it as a mailbox. |
| Rules | **Settings → Rules**: **Import rules…** reads this app’s JSON export or a legacy `.rwz` file. **Export rules…** writes JSON. Each rule is bound to an **account**; a move folder can live on another mailbox (including an attached data file). They run on Get Messages, on import, and from **Run all rules**. |
| Print | Readable HTML of the (optionally unwrapped) message |
| Save PDF | Same content as a PDF |
| Save attachments ZIP | All attachments of the open message |
| Export selected EML (fascicolo) | The original `.eml` files — what a notaio or PA can keep as the message |

Opened messages are also stored as `.eml` under the data folder. The `.eml` files are the originals; PDF/print are a reading copy.

## Interface language

View → Language: **English**, **Italiano**, **Français**, **Deutsch**, or **Español**. Folder names from the server (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) are mapped to Inbox/Sent/… in the UI language.

## Country packs

**Settings → Features** is a checkbox matrix of **certified-mail** UI only. Ordinary IMAP/POP3, search, practice labels, mbox/`.pst` import, and attached data files stay on. Rows are certified-mail features; columns are **Italy**, **Europe**, **France**, **Germany**, **Spain**, **Switzerland**. The header checkbox turns every feature in that column on or off.

| Pack | What it unlocks | Default |
|--|--|--|
| Italy | PEC envelope / ricevute / delivery, Italian PEC presets, FatturaPA preview, fascicolo export | On |
| Europe | eIDAS REM evidence, Intesi REM preset | On |
| France / Germany / Spain / Switzerland | Operator evidence labels (AR24/LRE, De-Mail, Lleida, IncaMail). Those networks are usually not IMAP. | Off |

Turn off a pack to hide its menus, columns, and account presets. Existing mailboxes stay; only the extra UI goes away.

## Downloads

When a GitHub Release exists: Windows portable zip and setup (`postclient-{version}.exe`, self-contained — no .NET SDK on the PC; default path `C:\Program Files\MaksIT\Postclient`), Linux Flatpak, macOS DMG (`osx-arm64` and `osx-x64`). macOS builds are unsigned: first launch is **Open** from the context menu. WinGet listing comes later from the same setup exe.

### Linux (Flatpak)

GitHub releases include `postclient-{version}.flatpak` when the Windows release run finds WSL Debian + Flatpak builder.

```bash
flatpak install --user ./postclient-{version}.flatpak
flatpak run eu.postclient.desktop
```

## Run from source

```powershell
dotnet run --project src/MaksIT.PostClient.UI
```

From `src/` so `global.json` applies:

```powershell
cd src
dotnet build MaksIT.PostClient.slnx
```

## Tests

```powershell
utils\Invoke-TestEngine.bat
```

Coverage shields at the top of this file are maintained by the test engine (**CoverageBadges**).

## Release

1. Update [CHANGELOG.md](CHANGELOG.md) and bump `<Version>` in [src/Directory.Build.props](src/Directory.Build.props).
2. Tag `v{version}` on `main` when a remote exists.
3. Run `utils\Invoke-ReleasePackage.bat` (self-contained Windows zip + setup exe, Linux Flatpak via WSL). Publishing the GitHub Release starts [macOS release assets](.github/workflows/macos-release.yml), which attaches unsigned `osx-arm64` and `osx-x64` DMGs.

## License

Apache License 2.0. Copyright 2026 Maksym Sadovnychyy (MAKS-IT).
