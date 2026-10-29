# Microsoft Store description (en-us)

Paste into Partner Center → Store listings → **English (United States)**. Plain text only. URLs in these fields are not clickable; put the privacy policy, support site, and license in their own submission fields.

Leave **What's new in this version** empty on the first submission. Use it only for a later update (1,500 characters).

The other languages are listed in [../description.md](../description.md). Hardware checkboxes stay in [CONTRIBUTING.md](../../../CONTRIBUTING.md) under **System requirements (Properties)**.

## Short description

Shown at the top of the listing. The field allows 1,000 characters. Keep this under 270 so every Store view shows the full sentence.

```text
A desktop mail client for mailboxes you already have. Read and send with IMAP, POP3, and SMTP, including Italian PEC and European REM. Receipts stay on this PC. Optional meaning search runs on the device and does not upload your mail.
```

## Description

Required. Up to 10,000 characters. This text is different from the short description so the page does not repeat the same paragraph.

```text
Keep ordinary mail and certified mail in one window on Windows. Postclient connects to the provider you already use. It does not issue a PEC address, a qualified receipt, or a timestamp.

Connect with IMAP, POP3, or SMTP. Presets fill hosts and ports for Gmail, Microsoft 365, Aruba PEC, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero, and Intesi Group. Any other host works as a custom account. Set the mailbox kind to Italian PEC or EU REM when a custom domain is certified. A folder store is a directory on this PC: no password and no SMTP. Several mailboxes stay open.

The folder tree follows the server, including nested folders and Gmail labels. Standard folders that the server stores under INBOX sit next to Inbox. You can create a folder, empty one, mark all read or unread, and drag messages onto a folder. The list shows unread, flag, attachments, delivery, type, from, subject, date, size, and a practice label. Open HTML, plain text, the raw source, or an Italian e-invoice. Group conversations indents replies. Reply, reply all, forward, flag, mark spam, and delete. Delete moves mail to Trash. Deleting from Trash is permanent. Compose is plain text. To, Cc, and Bcc are address chips. Send uses the selected mailbox only. From is that mailbox. You do not type a free From address.

Certified mail is shown as evidence, not as a generic attachment. Unwrap a PEC transport message and read the inner postacert.eml. The type column marks PEC, a receipt, REM, or a digital signature. The delivery column reads accettazione, avvenuta consegna, ETSI REM events, and the French, German, and Spanish receipt labels when those packs are on. Italian PEC has no read receipt: avvenuta consegna means the message was deposited in the certified mailbox. After you send, Sent is matched to receipts in Inbox and Ricevute by Message-Id, daticert.xml, and the subject prefix. This app shows that evidence. It does not issue a qualified receipt, a PEC envelope, or a timestamp.

Get Messages stores the whole IMAP folder on this PC, as mail.db plus the original .eml files. Search covers subject, body, text extracted from common attachments, and practice labels. Meaning search is optional. A model is downloaded once onto this PC, vectors stay in mail.db, and mail is not sent to a cloud AI service. Rules run on sync and on import. A destination folder can live on another account. Import a rules file or export JSON.

File actions cover EML and mbox import, an Outlook PST copied into a folder store, retention per folder, a passphrase-locked migration bundle, and an export of the original .eml files. Mark as spam remembers that message on this computer and can hint at similar mail in every mailbox. Country packs hide the certified-mail extras you do not use. Ordinary IMAP stays available. The interface is English, Italian, French, German, or Spanish.

Passwords and sign-in tokens stay beside settings, not inside settings.json. Gmail and Microsoft 365 use Identity Hub. Layout and accounts are stored on this computer.

You need 64-bit Windows 10 or Windows 11 and a mailbox you already have, or a local folder store. Install WebView2 if you want HTML mail. Otherwise the text body is shown. The meaning-search model is a separate download, only if you turn that index on.
```

Product features (up to 20 bullets) are in [product-features.md](product-features.md). Keywords are in [keywords.md](keywords.md). Copyright, additional license terms, and Developed by are in [additional-info.md](additional-info.md).

## Additional system requirements

Optional text lines, separate from the Properties hardware table. Up to 11 minimum and 11 recommended. Each line is at most 200 characters.

Minimum:

```text
64-bit Windows 10 or Windows 11
An IMAP, POP3, or SMTP mailbox you already have, or a local folder of messages
Network access to that mail server when the account is not a local folder store
```

Recommended:

```text
WebView2, so HTML mail can use the page view. The text body is shown when it is missing
Disk space for the local archive. The optional meaning-search model is about 300 MB
```

Category is set once on the product, not per language: Productivity.
