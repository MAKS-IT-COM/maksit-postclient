# Postclient

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-0%25-red)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-0%25-red)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-0%25-red)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Sprache:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

Die Programmoberfläche ist Englisch oder Italienisch. Dieser Text beschreibt den Client für Nutzer **zertifizierter elektronischer Post** (italienische PEC und eIDAS-REM), privat oder in der Kanzlei / Agentur.

Postclient ist ein Desktop-Mailclient für Windows, Linux und macOS (Apple Silicon und Intel). Er verbindet sich per **IMAP**, **POP3** und **SMTP** mit vorhandenen Postfächern: gewöhnliche Mail, italienische **PEC** bei einem qualifizierten Gestore, und europäische **REM** (elektronisch eingeschriebene Zustellung), wenn der Betreiber IMAP/SMTP anbietet.

Es ist **kein** qualifizierter Vertrauensdiensteanbieter (QTSP), **kein** PEC-Betreiber, **keine** Zeitstempelstelle und **kein** Cloud-Postfach. Umschläge und Nachweise stellt der Gestore / QTSP aus. Dieses Programm öffnet sie, zeigt den Nachweis des Betreibers und legt eine Kopie auf dem Rechner ab.

| | |
|--|--|
| Art | Desktop-Mailclient |
| OS | Windows, Linux, macOS |
| Stack | C#, .NET 10, Avalonia, MailKit |
| Lizenz | [Apache 2.0](LICENSE.md) |
| Ist nicht | Ein QTSP, eine neue PEC-/REM-Adresse, gehostete Mail, eine Scanner-App |

Änderungen: [CHANGELOG.md](CHANGELOG.md). Entwicklung: [CONTRIBUTING.md](CONTRIBUTING.md) und die [englische README](README.md) (Build, Tests, Release).

## Zielgruppen

### Privatpersonen

Alltäglicher Client für eine oder mehrere Adressen: Gmail oder Microsoft 365 neben einem persönlichen PEC-Postfach (Italien) oder einem IMAP-REM-Postfach. Das Archiv liegt **auf diesem PC**: eine Hausverwaltung, eine Steuerakte oder ein Behördenbrief bleibt durchsuchbar, ohne die Originale auf fremden Servern zu lassen.

Typische Nutzung: zertifizierte und gewöhnliche Post in einem Fenster; sehen, ob der Gestore angenommen und hinterlegt hat; drucken oder als PDF speichern; die originale `.eml` exportieren, wenn Gemeinde, Bank oder Gericht sie verlangt.

### Kanzleien und Agenturen

Nötig, wenn zertifizierte Identitäten nicht vermischt werden dürfen: persönliche PEC, Kanzlei-PEC, gewöhnliche Mail. **Von** ist eine Auswahl verbundener Postfächer, keine frei eingetippte Adresse. Ein Anwalt, Steuerberater, Notar, Hausverwalter oder eine EU-Kanzlei mit Behördenverkehr kann:

- jedes Betreiber-Postfach getrennt halten, mit IMAP-Kontingent in der Statusleiste;
- Nachrichten **auf diesem PC** nach Mandat kennzeichnen (WEG, Mandantenakte) — das ist keine geteilte Kanzleitabelle;
- den Versand mit den dazugehörigen Gestore-Nachweisen zusammenhalten;
- ausgewählte Originale als **Fascicolo** aus `.eml`-Dateien für Notar, Gericht oder Behörde exportieren;
- **FatturaPA**-XML (italienische E-Rechnung) als lesbare Ansicht öffnen (XML-Parser, keine KI);
- ein Thunderbird-Profil oder eine Outlook-**PST/OST** importieren, ohne Outlook zu installieren.

Das Programm ersetzt den Gestore nicht, stellt keinen qualifizierten Nachweis aus und verteilt die Mandatskennzeichen nicht über die PCs der Kanzlei.

## Konten und Protokolle

Eingang: **IMAP** (Ordnerbaum) oder **POP3** (nur Posteingang). Ausgang: **SMTP**. Verschlüsselung: Auto, SSL/TLS, STARTTLS, STARTTLS falls verfügbar, oder keine. MailKit nutzt die SASL-Verfahren, die der Server anbietet.

Mehrere Postfächer sind speicherbar. Profile füllen Hosts und Ports:

| Profil | Rolle |
|--|--|
| Gmail | IMAP/SMTP; Anmeldung bei Google (OAuth2 / XOAUTH2) oder App-Passwort |
| Outlook / Microsoft 365 | IMAP/SMTP; Anmeldung bei Microsoft. Die meisten dieser Konten lehnen das Postfachpasswort ab |
| IT PEC — Aruba, Legalmail, Namirial, Poste Italiane, Register.it, Libero | IMAP/SMTP-Hosts des italienischen Gestore (Hostnamen in der [englischen README](README.md)) |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Jeder Host, den der Betreiber mitgeteilt hat |

Zertifizierte IMAP-Postfächer nutzen das Gestore-Passwort oder ein App-Passwort bei 2FA. **De-Mail** ist in der Regel **kein** IMAP. Dasselbe gilt für **AR24** (Frankreich), **IncaMail** (Schweiz) und **Lleida** (Spanien). Hat der Betreiber IMAP/POP3-Hosts genannt, wählen Sie **IMAP / POP3** und tragen sie ein. Sonst öffnet dieser Client das Postfach nicht; ETSI-REM-Nachweise auf importierten `.eml` kann er trotzdem anzeigen.

OAuth: Google-Client-ID (endet auf `.apps.googleusercontent.com`) und bei **Web**-Clients das **Client-Secret** — beides in den Kontoeinstellungen (Secret in `secrets.bin`). Öffentliche Desktop-Clients brauchen kein Secret. Variablen `POSTCLIENT_GOOGLE_CLIENT_ID` / `POSTCLIENT_GOOGLE_CLIENT_SECRET`. **Gmail API** aktivieren, Scope `https://mail.google.com/`, Loopback `http://127.0.0.1`, **IMAP** in Gmail. Microsoft: Azure-Anwendungs-ID (GUID); öffentlicher Client; Redirect `http://localhost`; IMAP/SMTP-Berechtigungen.

Passwörter und OAuth-Refresh-Token liegen neben den Einstellungen (`DPAPI` unter Windows, Dateimodus `600` unter Linux/macOS), nie in `settings.json`.

## Italienische PEC

Die PEC ist ein **Umschlag** des Gestore:

- Transportnachricht mit `X-Trasporto: posta-certificata`, `daticert.xml` und Original in `postacert.eml`;
- Nachweise (`X-Ricevuta`) in **Posteingang** und **Ricevute**: accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, Virus, Fehler-Vorankündigung.

Ansicht → **Unwrap PEC/REM envelope** zeigt das innere `postacert.eml` (den für den Empfänger bestimmten Text) statt des Gestore-Wrappers. Die Spalte Typ kennzeichnet **PEC** (Transport) oder **RIC** (Nachweis).

Italienische PEC hat **keine Lesebestätigung**. **Avvenuta consegna** heißt Hinterlegung im zertifizierten Postfach des Empfängers, nicht dass eine Person die Nachricht geöffnet hat.

## Europäische REM (eIDAS)

Trägt die Nachricht ETSI-REM-Nachweis (`REMEvidence` / `urn:etsi:rem` oder einen Teilnamen `remevidence` / `rem-md`), behandelt der Client sie als **REM** (Region Europa). Ereignisse landen in derselben Zustellspalte wie bei der PEC:

- `SubmissionAcceptance` → der QTSP hat angenommen;
- `Delivery` / LRE- bzw. AR24-Avis / De-Mail-**Zustellung** / acuse de recibo → hinterlegt;
- `ContentConsignment` / `Retrieval` / De-Mail-**Abholbestätigung** → der Empfänger hat den Inhalt abgeholt (nächstes europäisches Pendant zu „gelesen“);
- Ablehnung, Virus, Nichtzustellung, `DeliveryExpiration` → fehlgeschlagen.

S/MIME (`smime.p7s` / `smime.p7m`) erscheint als digital signiert; das ist weder PEC noch REM.

## Zustellspalte nach dem Senden

Nach dem SMTP-Versand speichert Postclient die ausgehende **Message-Id** und gleicht beim Öffnen von **Gesendet** die Nachweise aus **Posteingang** und **Ricevute** ab. Zuordnung der Reihe nach: `<msgid>` in `daticert.xml`, `X-Riferimento-Message-ID`, `In-Reply-To`, PEC-`identificativo`, dann Betreffpräfixe (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

| Nachweis | Status | Bedeutung |
|--|--|--|
| PEC `accettazione` / `presa-in-carico`, ETSI `SubmissionAcceptance` | Angenommen | Der Gestore hat die Nachricht übernommen |
| PEC `avvenuta-consegna`, ETSI `Delivery`, LRE/AR24-Avis, Zustellung | Zugestellt | Hinterlegt im **zertifizierten Postfach** — PEC bestätigt nicht das Lesen |
| ETSI `ContentConsignment` / `Retrieval`, Abholbestätigung | Abgeholt | Der Empfänger hat den Inhalt abgeholt |
| `non-accettazione`, `mancata-consegna`, Virus, ETSI-Ablehnung | Fehlgeschlagen | Der Gestore meldet Annahme- oder Zustellfehler |

Der Client **zeigt** den Nachweis des Betreibers. Er stellt keinen qualifizierten Nachweis, PEC-Umschlag oder Zeitstempel aus.

Systemhinweise erscheinen bei neuer PEC, neuen Ricevute und neuer REM (Windows-Toast, macOS-Mitteilung, `notify-send` unter Linux).

## Lesen, Ordner, Verfassen

- Der Baum folgt dem Server (Posteingang, Ricevute, Entwürfe, Gesendet, Archiv, Junk, Papierkorb, plus eigene Ordner). Systemordner lassen sich nicht löschen. Ordner anlegen, leeren (in den Papierkorb bzw. endgültig im Papierkorb), alles gelesen/ungelesen, eigenen Ordner löschen, Nachrichten auf einen Ordner ziehen.
- Spalten: ungelesen, Kennzeichnung, Anlagen, Zustellung, Typ (PEC / RIC / REM / SIG), von, Betreff, Datum (lokal `yyyy-MM-dd HH:mm`), Mandatskennzeichen.
- Ansicht: HTML (Web-Engine des Systems), Text, Quelltext oder **FatturaPA**. Layout: Liste über dem Lesen, oder drei Spalten.
- **Unterhaltungen gruppieren** rückt Antworten in der Liste ein (Message-ID / In-Reply-To / References, Tiefe bis 8). PEC-Nachweise zum selben Original stehen beieinander. Es gibt kein eigenes Unterhaltungsfenster.
- Aktionen: antworten, allen antworten, weiterleiten, in neuem Fenster, gelesen/ungelesen, Kennzeichnung, Priorität, löschen.
- Das Verfassen ist **Klartext**. An, Cc, Bcc sind Adress-Chips. Der Versand nutzt nur die SMTP-Identität des gewählten Postfachs.

HTML: **WebView2** unter Windows, **WebKitGTK** unter Linux, **WKWebView** unter macOS. Fehlt die Engine, erscheint der Textkörper.

Linux-Pakete für die HTML-Ansicht:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora: `gtk3 webkit2gtk4.1 libsoup3`.

## Archiv, Suche, Kennzeichen

„Nachrichten abrufen“ indiziert den **gesamten** IMAP-Ordner in SQLite (`mail.db`) plus `.eml` auf der Platte. POP3 füllt nur den Posteingang. Die Indizierung läuft im Hintergrund; die Statusleiste zeigt den Fortschritt.

Die Suche (aktueller Ordner) umfasst Betreff, Text, Anlagentext (PDF sowie text/XML/HTML/CSV/JSON) und Mandatskennzeichen. Kennzeichen existieren nur auf diesem PC.

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
| Thunderbird importieren | Mbox-Stores des Thunderbird-Profils |
| Outlook PST/OST importieren | Mail ohne Outlook-Installation |
| Drucken | Lesbares HTML der (ggf. ausgepackten) Nachricht |
| PDF speichern | Derselbe Inhalt als PDF |
| Anlagen-ZIP | Alle Anlagen der geöffneten Nachricht |
| Ausgewählte EML exportieren (Fascicolo) | Die originalen `.eml` — was Notar oder Behörde als Nachricht aufbewahren können |

Geöffnete Nachrichten liegen zusätzlich als `.eml` im Datenordner. Die `.eml` sind die Originale; PDF und Druck sind eine Lesekopie.

## Oberfläche

Ansicht → Sprache: **English** oder **Italiano**. Serverseitige Ordnernamen (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) werden Posteingang / Gesendet / … in der UI-Sprache zugeordnet.

## Downloads

Wenn ein GitHub-Release existiert: portables Windows-Zip und Setup (`postclient-{version}.exe`, eigenständig — kein .NET-SDK auf dem PC), Linux-Flatpak, macOS-DMG (`osx-arm64` und `osx-x64`). macOS-Builds sind unsigniert: beim ersten Start **Öffnen** im Kontextmenü.

### Linux (Flatpak)

```bash
flatpak install --user ./postclient-{version}.flatpak
flatpak run eu.postclient.desktop
```

Build, Tests und Release: [englische README](README.md).

## Lizenz

Apache License 2.0. Copyright 2026 Maksym Sadovnychyy (MAKS-IT).
