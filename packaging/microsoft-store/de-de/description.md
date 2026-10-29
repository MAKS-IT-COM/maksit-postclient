# Microsoft Store description (de-de)

Paste into Partner Center → Store listings → **German (Germany)**. Plain text only. URLs in these fields are not clickable; put the privacy policy, support site, and license in their own submission fields.

Leave **What's new in this version** empty on the first submission. Use it only for a later update (1,500 characters).

The other languages are listed in [../description.md](../description.md). Hardware checkboxes stay in [CONTRIBUTING.md](../../../CONTRIBUTING.md) under **System requirements (Properties)**.

## Short description

Shown at the top of the listing. The field allows 1,000 characters. Keep this under 270 so every Store view shows the full sentence.

```text
Desktop-Mailclient für Postfächer, die Sie schon haben. Lesen und senden mit IMAP, POP3 und SMTP, einschließlich italienischer PEC und europäischer REM. Nachweise bleiben auf diesem PC. Die Bedeutungssuche läuft auf dem Gerät und lädt keine Post hoch.
```

## Description

Required. Up to 10,000 characters. This text is different from the short description so the page does not repeat the same paragraph.

```text
Halten Sie gewöhnliche Post und zertifizierte Post in einem Fenster unter Windows. Postclient verbindet sich mit dem Anbieter, den Sie schon nutzen. Es vergibt keine PEC-Adresse, keinen qualifizierten Nachweis und keinen Zeitstempel.

Verbinden Sie sich mit IMAP, POP3 oder SMTP. Profile füllen Hosts und Ports für Gmail, Microsoft 365, PEC Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero und Intesi Group. Jeder andere Host ist ein eigenes Konto. Setzen Sie die Postfachart auf italienische PEC oder europäische REM, wenn eine eigene Domain zertifiziert ist. Ein Ordnerspeicher ist ein Verzeichnis auf diesem PC: kein Kennwort und kein SMTP. Mehrere Postfächer bleiben geöffnet.

Der Ordnerbaum folgt dem Server, einschließlich verschachtelter Ordner und Gmail-Labels. Standardordner, die der Server unter INBOX ablegt, stehen neben dem Posteingang. Sie können einen Ordner anlegen, leeren, alles als gelesen oder ungelesen markieren und Nachrichten auf einen Ordner ziehen. Die Liste zeigt ungelesen, Kennzeichnung, Anlagen, Zustellung, Typ, Von, Betreff, Datum, Größe und eine Aktenmarke. Öffnen Sie HTML, einfachen Text, die Quelle oder eine italienische E-Rechnung. Unterhaltungen gruppieren rückt Antworten ein. Antworten, allen antworten, weiterleiten, kennzeichnen, als Spam markieren und löschen. Löschen verschiebt die Post in den Papierkorb. Löschen aus dem Papierkorb ist endgültig. Verfassen ist einfacher Text. An, Cc und Bcc sind Adressen. Senden nutzt nur das gewählte Postfach. Von ist dieses Postfach. Eine freie Absenderadresse wird nicht eingetippt.

Zertifizierte Post wird als Nachweis gezeigt, nicht als beliebige Anlage. Öffnen Sie die PEC-Transportnachricht und lesen Sie die innere postacert.eml. Die Spalte Typ markiert PEC, einen Nachweis, REM oder eine digitale Signatur. Die Spalte Zustellung liest Annahme, erfolgte Zustellung, ETSI-REM-Ereignisse und die französischen, deutschen und spanischen Nachweistexte, wenn diese Pakete an sind. Italienische PEC hat keine Lesebestätigung: erfolgte Zustellung heißt, die Nachricht wurde im zertifizierten Postfach abgelegt. Nach dem Senden wird Gesendet den Nachweisen in Posteingang und Ricevute über Message-Id, daticert.xml und das Betreffpräfix zugeordnet. Dieses Programm zeigt diesen Nachweis. Es stellt keinen qualifizierten Nachweis, keinen PEC-Umschlag und keinen Zeitstempel aus.

Nachrichten abrufen speichert den ganzen IMAP-Ordner auf diesem PC, als mail.db plus die ursprünglichen .eml-Dateien. Die Suche umfasst Betreff, Text, aus üblichen Anlagen gelesenen Text und Aktenmarken. Die Bedeutungssuche ist optional. Ein Modell wird einmal auf diesen PC geladen, die Vektoren bleiben in mail.db, und Post geht nicht an einen Cloud-KI-Dienst. Regeln laufen beim Abgleich und beim Import. Ein Zielordner kann auf einem anderen Konto liegen. Importieren Sie eine Regeldatei oder exportieren Sie JSON.

Dateiaktionen umfassen EML- und mbox-Import, eine in einen Ordnerspeicher kopierte Outlook-PST, Aufbewahrung je Ordner, ein mit einer Passphrase gesperrtes Migrationspaket und den Export der ursprünglichen .eml-Dateien. Als Spam markieren merkt sich diese Nachricht auf diesem Computer und kann ähnliche Post in jedem Postfach andeuten. Länderpakete blenden zertifizierte Extras aus, die Sie nicht nutzen. Gewöhnliches IMAP bleibt verfügbar. Die Oberfläche ist Englisch, Italienisch, Französisch, Deutsch oder Spanisch.

Kennwörter und Anmeldetoken bleiben neben den Einstellungen, nicht in settings.json. Gmail und Microsoft 365 nutzen Identity Hub. Layout und Konten liegen auf diesem Computer.

Sie brauchen 64-Bit-Windows 10 oder Windows 11 und ein Postfach, das Sie schon haben, oder einen Ordnerspeicher. Installieren Sie WebView2, wenn Sie HTML-Mail wollen. Sonst wird der Text gezeigt. Das Modell für die Bedeutungssuche ist ein eigener Download, nur wenn Sie diesen Index einschalten.
```

Product features (up to 20 bullets) are in [product-features.md](product-features.md). Keywords are in [keywords.md](keywords.md). Copyright, additional license terms, and Developed by are in [additional-info.md](additional-info.md).

## Additional system requirements

Optional text lines, separate from the Properties hardware table. Up to 11 minimum and 11 recommended. Each line is at most 200 characters.

Minimum:

```text
64-Bit-Windows 10 oder Windows 11
Ein vorhandenes IMAP-, POP3- oder SMTP-Postfach oder ein lokaler Nachrichtenordner
Netzwerkzugriff auf diesen Mailserver, wenn das Konto kein Ordnerspeicher ist
```

Recommended:

```text
WebView2, damit HTML-Mail die Seitenansicht nutzt. Sonst wird der Textkörper gezeigt
Speicherplatz für das lokale Archiv. Das optionale Modell für die Bedeutungssuche ist etwa 300 MB
```

Category is set once on the product, not per language: Productivity.
