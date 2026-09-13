# Postclient

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-49.7%25-yellowgreen)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-47%25-yellowgreen)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-52.6%25-yellowgreen)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Language:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

The application UI is English or Italian. These documents describe the same client for people who use certified electronic mail (Italian PEC and eIDAS REM) as private users or as a practice / agency.

Postclient is a desktop mail client for Windows, Linux, and macOS (Apple Silicon and Intel). It connects with **IMAP**, **POP3**, and **SMTP** to mailboxes you already have: ordinary mail, Italian **PEC** at a qualified gestore, and European **REM** (registered electronic mail) when the operator publishes IMAP/SMTP.

It is **not** a qualified trust service provider (QTSP), **not** a PEC operator, **not** a timestamp authority, and **not** a cloud mailbox. Receipts and transport envelopes come from the gestore / QTSP. This client unwraps them, shows the operator’s evidence, and keeps a copy on the machine.

| | |
|--|--|
| Kind | Desktop mail client |
| OS | Windows, Linux, macOS |
| Stack | C#, .NET 10, Avalonia, MailKit |
| License | [Apache 2.0](LICENSE.md) |
| Not | A QTSP, a new PEC/REM address, hosted mail, or a scanner |

Changes: [CHANGELOG.md](CHANGELOG.md). Contributing: [CONTRIBUTING.md](CONTRIBUTING.md).

## Who it is for

### Private individuals

Use it as the daily client for one or more addresses: Gmail or Microsoft 365 next to a personal PEC (Italy) or an IMAP REM mailbox (EU). Mail is indexed on this PC so you can search a condominium notice, a tax file, or a PA communication years later without leaving the originals on someone else’s server.

Typical private use: keep the certified mailbox and the ordinary mailbox in one window; see whether a PEC was accepted and deposited; print or save a PDF of a message; export the original `.eml` if a municipality, bank, or court asks for it.

### Practices and agencies

Use it when several certified identities must not be mixed: personal PEC, studio PEC, and ordinary mail (Gmail / Microsoft 365). **From** is a picker of connected mailboxes, not a free-typed address. A commercialista, avvocato, notaio, CAF, condominium administrator, or EU practice that files with PA can:

- keep each gestore mailbox separate, with its own IMAP quota in the status bar;
- label messages on this PC by practice (for example a condominium or a client file) — labels are **local**, not a shared studio database;
- group a send with the gestore ricevute that refer to it;
- export selected originals as a **fascicolo** of `.eml` files for a notaio, a court, or a PA desk;
- open **FatturaPA** XML attachments as a readable view (XML parser, not AI);
- import a colleague’s Thunderbird profile or an Outlook **PST/OST** without installing Outlook.

The program does not replace the gestore, does not issue a qualified ricevuta, and does not share the practice table across the office.

## Accounts and protocols

Incoming: **IMAP** (full folder tree) or **POP3** (Inbox only). Outgoing: **SMTP**. Encryption: Auto, SSL/TLS, STARTTLS, STARTTLS if available, or none. MailKit uses the SASL methods the server advertises.

Several mailboxes can be stored at once. Presets fill hosts and ports:

| Preset | Role |
|--|--|
| Gmail | IMAP/SMTP; sign in with Google (OAuth2 / XOAUTH2) or a Gmail app password |
| Outlook / Microsoft 365 | IMAP/SMTP; sign in with Microsoft. Most of these accounts reject a mailbox password |
| IT PEC — Aruba | `imaps.pec.aruba.it` / `smtps.pec.aruba.it` (POP3: `pop3s.pec.aruba.it`) |
| IT PEC — InfoCert Legalmail | `mbox.cert.legalmail.it` / `sendm.cert.legalmail.it`. Username is often the InfoCert User ID, not the address |
| IT/EU PEC — Namirial | `imaps.sicurezzapostale.it` / `smtps.sicurezzapostale.it` |
| IT PEC — Poste Italiane | `mail.postecert.it` |
| IT PEC — Register.it | `imap.pec-email.com` / `smtp.pec-email.com` |
| IT PEC — Libero | `mail.postacert.it.net` |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Any other host the operator gave you |

Certified IMAP mailboxes use the gestore password, or an app password if the gestore has 2FA. **AR24** (France), **De-Mail** (Germany), **IncaMail** (Switzerland), and **Lleida** (Spain) are usually not IMAP. If that operator gave you IMAP/POP3 hosts, choose **IMAP / POP3** and enter them. Otherwise this client cannot open that mailbox; it can still display ETSI REM evidence on messages you import as `.eml`.

OAuth needs a Google client ID (ends with `.apps.googleusercontent.com`) and, for **Web** clients, the **client secret** — paste both in Account Settings (the secret is stored in `secrets.bin`, not as the mailbox password). Desktop public clients need no secret. Environment variables `POSTCLIENT_GOOGLE_CLIENT_ID` / `POSTCLIENT_GOOGLE_CLIENT_SECRET` are also accepted. Enable the **Gmail API**, add scope `https://mail.google.com/` on the consent screen, allow loopback `http://127.0.0.1`, and enable **IMAP** in Gmail. Microsoft: Azure Application (client) ID (a GUID); public client; redirect `http://localhost`; IMAP/SMTP permissions.

Passwords and OAuth refresh tokens stay next to settings (`DPAPI` on Windows, file mode `600` on Linux/macOS), never in `settings.json`.

## Italian PEC

Italian certified mail is a **busta** (envelope) produced by the gestore:

- transport message with `X-Trasporto: posta-certificata`, `daticert.xml`, and the original as `postacert.eml`;
- ricevute (`X-Ricevuta`) in **Inbox** and **Ricevute**: accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, rilevazione-virus, preavviso-errore-consegna.

View → **Unwrap PEC/REM envelope** shows the inner `postacert.eml` (the message the recipient was meant to read) instead of the outer gestore wrapper. The Type column marks **PEC** (transport) or **RIC** (ricevuta).

Italian PEC has **no read receipt**. **Avvenuta consegna** means the message was deposited in the recipient’s certified mailbox, not that a person opened it.

## European REM (eIDAS)

When the message carries ETSI REM evidence (`REMEvidence` / `urn:etsi:rem`, or a part name such as `remevidence` / `rem-md`), the client treats it as **REM** (region Europe). Event types are mapped onto the same delivery column as PEC:

- `SubmissionAcceptance` → accepted by the QTSP;
- `Delivery` / LRE or AR24 avis de réception / De-Mail Zustellung / acuse de recibo → deposited;
- `ContentConsignment` / `Retrieval` / De-Mail `Abholbestätigung` → the recipient retrieved the content (closest European equivalent of “read”);
- rejection, virus, non-delivery, `DeliveryExpiration` → failed.

S/MIME (`smime.p7s` / `smime.p7m`) is shown as digitally signed; it is not PEC and not REM.

## Delivery column after send

After SMTP send, Postclient stores the outbound **Message-Id** and, when you open **Sent**, reconciles gestore ricevute from **Inbox** and **Ricevute**. Matching uses, in order: `daticert.xml` `<msgid>`, `X-Riferimento-Message-ID`, `In-Reply-To`, PEC `identificativo`, then subject prefixes (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

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
- Compose is **plain text**. To, Cc, Bcc are address chips. Attachments are listed on the composer. Send uses the selected mailbox’s SMTP identity only.

HTML uses the OS engine: **WebView2** on Windows (profile under the data folder), **WebKitGTK** on Linux, **WKWebView** on macOS. If the engine is missing, the text body is shown instead.

Linux packages for the HTML view:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora: `gtk3 webkit2gtk4.1 libsoup3`.

## Archive, search, labels

Get Messages indexes the **whole IMAP folder** into a local SQLite store (`mail.db`) plus `.eml` objects on disk. POP3 only fills Inbox. Indexing runs in the background; the status bar shows progress.

Search (current folder) covers subject, body, attachment text (PDF and text/XML/HTML/CSV/JSON), and practice labels. Labels exist only on this PC.

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
| Import Thunderbird | Local mbox stores from the Thunderbird profile |
| Import Outlook PST/OST | Mail without installing Outlook |
| Print | Readable HTML of the (optionally unwrapped) message |
| Save PDF | Same content as a PDF |
| Save attachments ZIP | All attachments of the open message |
| Export selected EML (fascicolo) | The original `.eml` files — what a notaio or PA can keep as the message |

Opened messages are also stored as `.eml` under the data folder. The `.eml` files are the originals; PDF/print are a reading copy.

## Interface language

View → Language: **English** or **Italiano**. Folder names from the server (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) are mapped to Inbox/Sent/… in the UI language.

## Downloads

When a GitHub Release exists: Windows portable zip and setup (`postclient-{version}.exe`, self-contained — no .NET SDK on the PC), Linux Flatpak, macOS DMG (`osx-arm64` and `osx-x64`). macOS builds are unsigned: first launch is **Open** from the context menu. WinGet listing comes later from the same setup exe.

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
