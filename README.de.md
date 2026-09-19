# Postclient — Desktop-Client für PEC, REM und IMAP

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-49.7%25-yellowgreen)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-47%25-yellowgreen)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-52.6%25-yellowgreen)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Sprache:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

Die Programmoberfläche ist Englisch, Italienisch, Französisch, Deutsch oder Spanisch. Diese Dokumente beschreiben denselben Open-Source-**Desktop-Mailclient** für alle, die **posta elettronica certificata (PEC)**, **eIDAS registered electronic mail (REM)** und gewöhnliche **IMAP- / POP3- / SMTP-Post** unter Windows, Linux und macOS senden und aufbewahren.

Postclient ist ein **lokaler Mailclient**, keine Webmail. Er spricht IMAP, POP3 und SMTP mit vorhandenen Konten: Gmail, Microsoft 365, italienische **PEC** bei einem qualifizierten Gestore (Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero und IMAP mit eigener Domain) sowie europäische **REM**, wenn der Betreiber IMAP/SMTP anbietet. Nachweise bleiben auf diesem PC als `.eml` plus durchsuchbares SQLite-Archiv.

Es ist **kein** qualifizierter Vertrauensdiensteanbieter (QTSP), **kein** PEC-Betreiber, **keine** Zeitstempelstelle und **kein** Cloud-Postfach. Umschläge und Nachweise stellt der Gestore / QTSP aus. Dieses Programm öffnet sie, zeigt den Zustellnachweis und behält die Originale auf dem Rechner.

| | |
|--|--|
| Art | Desktop-Mailclient für PEC, REM und IMAP |
| OS | Windows, Linux, macOS (Apple Silicon und Intel) |
| Stack | C#, .NET 10, Avalonia, MailKit |
| Lizenz | [Apache 2.0](LICENSE.md) |
| Ist nicht | Ein QTSP, eine neue PEC-/REM-Adresse, gehostete Mail, eine Scanner-App |

Änderungen: [CHANGELOG.md](CHANGELOG.md). Entwicklung: [CONTRIBUTING.md](CONTRIBUTING.md) und die [englische README](README.md) (Build, Tests, Release).

## Für zertifizierte Post auf dem Desktop

Desktop- und Webmail behandeln einen PEC-**Umschlag** oder einen ETSI-REM-Nachweis meist wie eine beliebige Anlage. Postclient ist um diesen Nachweis herum gebaut:

- **PEC-/REM-Umschläge öffnen** — innere `postacert.eml` (die Nachricht für den Empfänger) statt des Gestore-Wrappers; Spalte Typ **PEC**, **RIC**, **REM** oder digital signiert **SIG**.
- **Zustellspalte aus Betreiber-Nachweisen** — accettazione, avvenuta-consegna, ETSI `SubmissionAcceptance` / `Delivery` / `Retrieval`, LRE- und AR24-Avis, De-Mail-Zustellung und Abholbestätigung, acuse de recibo. Nach SMTP gleicht **Gesendet** Inbox/Ricevute über Message-Id, `daticert.xml` und Betreffpräfixe ab. Italienische PEC hat **keine Lesebestätigung**: avvenuta consegna ist Hinterlegung im zertifizierten Postfach.
- **FatturaPA-Vorschau** — italienische E-Rechnung (XML) als lesbare Ansicht (XML-Parser, keine generative KI).
- **Fascicolo originaler `.eml`** — ausgewählte Nachrichten als Dateien exportieren, die Notar, Behörde, Bank oder Gericht aufbewahren können.
- **Von ist eine Postfachauswahl** — keine frei eingetippte Absenderadresse; Versand nutzt die SMTP-Identität des verbundenen Kontos.
- **Mandatskennzeichen auf diesem PC** — WEG, Mandantenakte, Steuerbescheid. Kennzeichen sind lokal, keine Kanzlei-Datenbank im Netz.
- **Lokales Archiv des ganzen Ordners** — Nachrichten abrufen speichert den **gesamten IMAP-Ordner** in `mail.db` plus `.eml` auf der Platte (kein Last-N-Fenster). FTS über Betreff, Text, PDF-/XML-/HTML-/CSV-/JSON-Anlagen und Kennzeichen.
- **Bedeutungssuche auf dem Gerät (RAG)** — optionales EmbeddingGemma 300M unter **Einstellungen → Indizes…**. Vektoren bleiben in `mail.db`. Mail verlässt den Rechner nicht. Stichwort- und Bedeutungsindex neu aufbauen oder reparieren.
- **Regeln über Postfächer hinweg** — verschieben, löschen, kennzeichnen, etikettieren; Zielordner kann auf einem anderen Konto liegen, auch auf einer angehängten `.pst`-/`.ost`-Datei. Import von JSON oder einer älteren `.rwz`-Datei; Export ist JSON.
- **`.pst` / `.ost` als Postfach öffnen** — Unicode-Persönlicher Store ist schreibbar (Ordner, Flags, Verschieben, Löschen). Ein Offline-Store wird beim ersten Schreiben in ein Unicode-`.pst` kopiert. Zum Lesen der Datei ist kein weiteres Mailprogramm nötig.
- **Länderpakete** — **Einstellungen → Funktionen** schaltet Italien (PEC, FatturaPA, Fascicolo), Europa (eIDAS-REM), Frankreich, Deutschland, Spanien und die Schweiz ein oder aus. Gewöhnliches IMAP bleibt verfügbar.

## Zielgruppen

### Privatpersonen

Alltäglicher Client für eine oder mehrere Adressen: Gmail oder Microsoft 365 neben einem persönlichen **PEC-Postfach** (Italien) oder einem IMAP-**REM**-Postfach (EU). Eine Hausverwaltung, eine Steuerakte oder ein Behördenbrief bleibt durchsuchbar, ohne Originale auf fremden Servern zu lassen.

Typische Nutzung: zertifizierte und gewöhnliche Post in einem Fenster; sehen, ob der Gestore angenommen und hinterlegt hat; drucken oder als PDF speichern; die originale `.eml` exportieren, wenn Gemeinde, Bank oder Gericht sie verlangt.

### Kanzleien und Agenturen

Nötig, wenn zertifizierte Identitäten nicht vermischt werden dürfen: persönliche PEC, Kanzlei-PEC, gewöhnliche Mail. Ein Anwalt, Steuerberater, Notar, Hausverwalter oder eine EU-Kanzlei mit Behördenverkehr kann jedes Betreiber-Postfach getrennt halten (IMAP-**QUOTA** in der Statusleiste), Versand und dazugehörige Nachweise gruppieren und ein Fascicolo exportieren, ohne die Akte in ein Drittarchiv zu laden.

Das Programm ersetzt den Gestore nicht, stellt keinen qualifizierten Nachweis aus und verteilt Mandatskennzeichen nicht über die PCs der Kanzlei.

## Konten und Protokolle

Eingang: **IMAP** (Ordnerbaum), **POP3** (nur Posteingang) oder eine angehängte **`.pst` / `.ost`**. Unicode-`.pst` ist schreibbar. Ausgang: **SMTP**. Verschlüsselung: Auto, SSL/TLS, STARTTLS, STARTTLS falls verfügbar, oder keine. MailKit nutzt die SASL-Verfahren, die der Server anbietet.

Mehrere Postfächer sind speicherbar. Profile füllen Hosts und Ports:

| Profil | Rolle |
|--|--|
| Gmail | IMAP/SMTP; Anmeldung bei Google (OAuth2 / XOAUTH2) oder Gmail-App-Passwort |
| Microsoft 365 | IMAP/SMTP; Anmeldung bei Microsoft. Die meisten dieser Konten lehnen das Postfachpasswort ab |
| IT PEC — Aruba, Legalmail, Namirial, Poste Italiane, Register.it, Libero | IMAP/SMTP-Hosts des italienischen Gestore (Hostnamen in der [englischen README](README.md)) |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Jeder Host, den der Betreiber mitgeteilt hat. **Postfachart** auf italienische PEC oder EU-REM setzen, wenn das Postfach zertifiziert ist (auch mit eigener Domain) |
| Ordnerarchiv | Ordner auf der Platte (`postclient.store.json` + `mail.db` + `.eml`). Kein Passwort, kein SMTP. Anlegen, anhängen, verschieben oder lösen über das Dateimenü. |

Zertifizierte IMAP-Postfächer nutzen das Gestore-Passwort oder ein App-Passwort bei 2FA. **De-Mail** ist in der Regel **kein** IMAP. Dasselbe gilt für **AR24** (Frankreich), **IncaMail** (Schweiz) und **Lleida** (Spanien). Hat der Betreiber IMAP/POP3-Hosts genannt, wählen Sie **IMAP / POP3**. Sonst öffnet dieser Client das Postfach nicht; ETSI-REM-Nachweise auf importierten `.eml` kann er trotzdem anzeigen.

Gmail und Microsoft 365 laufen über **Identity Hub** (`https://identity.maks-it.com`). Kontoeinstellungen öffnen `/desktop-login?provider=Google` oder `Microsoft`. Hub-JWT und Refresh-Token bleiben in `secrets.bin`; IMAP nutzt ein kurzlebiges Mailbox-Token (XOAUTH2). Beim Verbinden erneuert der Client die Hub-Sitzung aus dem Refresh-Token, damit Gmail und Outlook nach dem Beenden angemeldet bleiben. Hat ein älterer Build nur das JWT gespeichert, einmal neu in den Kontoeinstellungen anmelden. Override: `POSTCLIENT_IDENTITY_HUB`. Passwort bleibt für PEC und generisches IMAP gültig.

Passwörter und OAuth-Refresh-Token liegen neben den Einstellungen (`DPAPI` unter Windows, Dateimodus `600` unter Linux/macOS), nie in `settings.json`.

## Italienische PEC (posta elettronica certificata)

Die PEC ist ein **Umschlag** des Gestore:

- Transportnachricht mit `X-Trasporto: posta-certificata`, `daticert.xml` und Original in `postacert.eml`;
- Nachweise (`X-Ricevuta`) in **Posteingang** und **Ricevute**: accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, Virus, Fehler-Vorankündigung.

Ansicht → **Unwrap PEC/REM envelope** zeigt das innere `postacert.eml`. Die Spalte Typ kennzeichnet **PEC** (Transport) oder **RIC** (Nachweis).

Italienische PEC hat **keine Lesebestätigung**. **Avvenuta consegna** heißt Hinterlegung im zertifizierten Postfach des Empfängers, nicht dass eine Person die Nachricht geöffnet hat.

## Europäische REM (eIDAS registered electronic mail)

Trägt die Nachricht ETSI-REM-Nachweis (`REMEvidence` / `urn:etsi:rem` oder einen Teilnamen `remevidence` / `rem-md`), behandelt der Client sie als **REM**. Ereignisse landen in derselben Zustellspalte wie bei der PEC:

- `SubmissionAcceptance` → der QTSP hat angenommen;
- `Delivery` / LRE- bzw. AR24-Avis / De-Mail-**Zustellung** / acuse de recibo → hinterlegt;
- `ContentConsignment` / `Retrieval` / De-Mail-**Abholbestätigung** → der Empfänger hat den Inhalt abgeholt (nächstes europäisches Pendant zu „gelesen“);
- Ablehnung, Virus, Nichtzustellung, `DeliveryExpiration` → fehlgeschlagen.

S/MIME (`smime.p7s` / `smime.p7m`) erscheint als digital signiert; das ist weder PEC noch REM.

## Zustellspalte nach dem Senden

Nach dem SMTP-Versand speichert Postclient die ausgehende **Message-Id** und gleicht beim Öffnen von **Gesendet** die Nachweise aus **Posteingang** und **Ricevute** ab. Das Warten gilt nur für PEC- oder REM-Konten (Profil oder IMAP/POP3 mit dieser Postfachart). Gewöhnliches Gmail, Microsoft 365 und IMAP gelten nur als gesendet. Zuordnung der Reihe nach: `<msgid>` in `daticert.xml`, `X-Riferimento-Message-ID`, `In-Reply-To`, PEC-`identificativo`, dann Betreffpräfixe (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

| Nachweis | Status | Bedeutung |
|--|--|--|
| PEC `accettazione` / `presa-in-carico`, ETSI `SubmissionAcceptance` | Angenommen | Der Gestore hat die Nachricht übernommen |
| PEC `avvenuta-consegna`, ETSI `Delivery`, LRE/AR24-Avis, Zustellung | Zugestellt | Hinterlegt im **zertifizierten Postfach** — PEC bestätigt nicht das Lesen |
| ETSI `ContentConsignment` / `Retrieval`, Abholbestätigung | Abgeholt | Der Empfänger hat den Inhalt abgeholt |
| `non-accettazione`, `mancata-consegna`, Virus, ETSI-Ablehnung | Fehlgeschlagen | Der Gestore meldet Annahme- oder Zustellfehler |

Der Client **zeigt** den Nachweis des Betreibers. Er stellt keinen qualifizierten Nachweis, PEC-Umschlag oder Zeitstempel aus.

Systemhinweise erscheinen bei neuer PEC, neuen Ricevute und neuer REM (Windows-Toast, macOS-Mitteilung, `notify-send` unter Linux).

## Lesen, Ordner, Verfassen

- Der Baum folgt dem Server (Posteingang, Ricevute, Entwürfe, Gesendet, Archiv, Junk, Papierkorb, plus eigene Ordner). Verschachtelte IMAP-Pfade erscheinen als Unterordner, auch Gmail-Labels `[Gmail]/…` unter `[Gmail]`. Entwürfe, Gesendet und Papierkorb als `INBOX.Drafts` / `INBOX.Sent` / `INBOX.Trash` stehen neben dem Posteingang, nicht darunter. Gmail trennt mit `/`, daher bleibt ein Label wie `P.IVA` ein Ordner. Systemordner lassen sich nicht löschen. Ordner anlegen, leeren (in den Papierkorb bzw. endgültig im Papierkorb), alles gelesen/ungelesen, eigenen Ordner löschen, Nachrichten auf einen Ordner ziehen. Welche Konten und Unterordner aufgeklappt oder zugeklappt sind, bleibt in `settings.json`.
- Spalten: ungelesen, Kennzeichnung, Anlagen, Zustellung, Typ (PEC / RIC / REM / SIG), von, Betreff, Datum (lokal `yyyy-MM-dd HH:mm`), Mandatskennzeichen.
- Ansicht: HTML (Web-Engine des Systems), Text, Quelltext oder **FatturaPA**. Layout: Liste über dem Lesen, oder drei Spalten (Ordner, Liste, Lesen).
- **Unterhaltungen gruppieren** rückt Antworten in der Liste ein (Message-ID / In-Reply-To / References, Tiefe bis 8). PEC-Nachweise zum selben Original stehen beieinander. Es gibt kein eigenes Unterhaltungsfenster.
- Aktionen: antworten, allen antworten, weiterleiten, in neuem Fenster, gelesen/ungelesen, Kennzeichnung, Priorität, löschen.
- Das Verfassen ist **Klartext**. An, Cc, Bcc sind Adress-Chips. **Anlagen als ZIP senden** packt per Drag-and-Drop übergebene Dateien (optionales Passwort). Der Versand nutzt nur die SMTP-Identität des gewählten Postfachs.

HTML: **WebView2** unter Windows, **WebKitGTK** unter Linux, **WKWebView** unter macOS. Fehlt die Engine, erscheint der Textkörper.

Linux-Pakete für die HTML-Ansicht:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora: `gtk3 webkit2gtk4.1 libsoup3`.

## Lokales Archiv, Stichwortsuche und Bedeutungsindex

„Nachrichten abrufen“ indiziert den **gesamten** IMAP-Ordner in SQLite (`mail.db`) plus `.eml` auf der Platte. POP3 füllt nur den Posteingang. Die Indizierung läuft im Hintergrund; die Statusleiste zeigt den Fortschritt.

- **Einstellungen → Indizes…**: Stichwortindex (FTS) und optional EmbeddingGemma 300M (~300 MB) für Bedeutung. Das ONNX-Paket kommt von Hugging Face (`onnx-community/embeddinggemma-300m-ONNX`). Vektoren bleiben in `mail.db` auf diesem PC. Gerät: Auto / CPU / GPU (Windows DirectML). Index neu aufbauen oder reparieren, wenn die Suche falsch wirkt. Die Gewichte unterliegen Googles Gemma-Bedingungen.

Die Suche (aktueller Ordner) umfasst Betreff, Text, Anlagentext (PDF sowie text/XML/HTML/CSV/JSON), Mandatskennzeichen und Bedeutung, sobald das Modell bereit ist. Kennzeichen existieren nur auf diesem PC.

Das Archiv liegt **nur auf diesem PC**. Datei → Archivordner öffnen / Archiv exportieren kopiert `mail.db` und die `.eml`. Pfade:

| OS | Konfiguration | Daten |
|--|--|--|
| Windows | `%AppData%\Postclient\` | `%LocalAppData%\Postclient\` |
| Linux | `~/.config/postclient/` | `~/.local/share/postclient/` |
| macOS | `~/Library/Application Support/Postclient/` | dasselbe |

Override: `POSTCLIENT_CONFIG` / `POSTCLIENT_DATA_DIR`. Das portable Windows-Zip mit `installType: portable` in `settings.json` hält die Konfiguration neben der EXE.

Das IMAP-**QUOTA** des Gestore (wenn der Server es anbietet) steht in der Statusleiste. Das ist der Speicher beim Betreiber, kein Cloud-Speicher dieser Anwendung.

## Import, Export, Druck

| Aktion | Ergebnis |
|--|--|
| EML importieren | Originale in den aktuellen Ordner |
| mbox importieren | Lokale mbox-Stores aus einem Desktop-Mailprofil |
| `.pst` / `.ost` importieren | Kopiert Mail aus der Datendatei in das **gewählte** IMAP-/POP3-Konto. Verschachtelte Stores in der Datei werden mitimportiert. Schließen Sie jedes Programm, das die Datei geöffnet hält. |
| Datendatei anhängen | Öffnet eine `.pst` / `.ost` als eigenes Postfach. Unicode-`.pst` ist schreibbar. `.ost` wird nicht am Ort beschrieben — die erste Änderung kopiert sie in ein Unicode-`.pst` neben dem Original. Dateimenü oder Kontotyp *Datendatei*. |
| Neue Datendatei | Dateimenü: leeres Unicode-`.pst` (Posteingang, Entwürfe, Gesendet, Gelöscht) und als Postfach anhängen. |
| Regeln | **Einstellungen → Regeln**: **Regeln importieren…** liest den JSON-Export dieser App oder eine ältere `.rwz`-Datei. **Regeln exportieren…** schreibt JSON. Jede Regel gehört zu einem **Konto**; ein Zielordner kann auf einem anderen Postfach liegen (auch einer angehängten Datendatei). Sie laufen beim Abrufen, beim Import und unter **Alle Regeln ausführen**. |
| Drucken | Lesbares HTML der (ggf. ausgepackten) Nachricht |
| PDF speichern | Derselbe Inhalt als PDF |
| Anlagen-ZIP | Alle Anlagen der geöffneten Nachricht |
| Ausgewählte EML exportieren (Fascicolo) | Die originalen `.eml` — was Notar oder Behörde als Nachricht aufbewahren können |

Geöffnete Nachrichten liegen zusätzlich als `.eml` im Datenordner. Die `.eml` sind die Originale; PDF und Druck sind eine Lesekopie.

## Oberfläche

Ansicht → Sprache: **English**, **Italiano**, **Français**, **Deutsch** oder **Español**. Serverseitige Ordnernamen (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) werden Posteingang / Gesendet / … in der UI-Sprache zugeordnet.

## Länderpakete

**Einstellungen → Funktionen** ist eine Matrix nur für **zertifizierte Post**. Gewöhnliches IMAP/POP3, Suche, Mandatskennzeichen, mbox-/`.pst`-Import und angehängte Datendateien bleiben an. Zeilen sind zertifizierte Funktionen; Spalten sind **Italien**, **Europa**, **Frankreich**, **Deutschland**, **Spanien**, **Schweiz**. Die Kopf-Checkbox schaltet die ganze Spalte.

| Paket | Was es freischaltet | Standard |
|--|--|--|
| Italien | PEC-Umschlag / Nachweise / Zustellung, PEC-Profile, FatturaPA-Vorschau, Fascicolo | An |
| Europa | eIDAS-REM-Nachweis, Intesi-REM-Profil | An |
| Frankreich / Deutschland / Spanien / Schweiz | Nachweisbeschriftungen (AR24/LRE, De-Mail, Lleida, IncaMail). Diese Netze sind in der Regel kein IMAP. | Aus |

Ein Paket auszuschalten blendet Menüs, Spalten und Profile aus. Vorhandene Postfächer bleiben; nur die Extra-UI verschwindet.

## Downloads

Wenn ein GitHub-Release existiert: portables Windows-Zip und Setup (`postclient-{version}.exe`, eigenständig — kein .NET-SDK auf dem PC; Standardpfad `C:\Program Files\MaksIT\Postclient`), Linux-Flatpak, macOS-DMG (`osx-arm64` und `osx-x64`). macOS-Builds sind unsigniert: beim ersten Start **Öffnen** im Kontextmenü.

### Linux (Flatpak)

```bash
flatpak install --user ./postclient-{version}.flatpak
flatpak run eu.postclient.desktop
```

Build, Tests und Release: [englische README](README.md).

## Lizenz

Apache License 2.0. Copyright 2026 Maksym Sadovnychyy (MAKS-IT).
