# Postclient

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-0%25-red)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-0%25-red)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-0%25-red)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Lingua:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

L’interfaccia del programma è in inglese o in italiano. Questo testo descrive il client per chi usa la **posta elettronica certificata** in Italia (PEC) e la **REM** eIDAS in Europa, come privato o come studio / agenzia.

Postclient è un client di posta da scrivania per Windows, Linux e macOS (Apple Silicon e Intel). Si collega con **IMAP**, **POP3** e **SMTP** alle caselle che già possiedi: posta ordinaria, **PEC** italiana presso un gestore qualificato, e **REM** europea quando l’operatore espone IMAP/SMTP.

**Non** è un prestatore di servizi fiduciari qualificati (QTSP), **non** è un gestore PEC, **non** è una marca temporale e **non** è una casella in cloud. Buste e ricevute le emette il gestore. Questo programma le apre, mostra l’evidenza dell’operatore e ne tiene una copia sul computer.

| | |
|--|--|
| Tipo | Client di posta da scrivania |
| Sistemi | Windows, Linux, macOS |
| Stack | C#, .NET 10, Avalonia, MailKit |
| Licenza | [Apache 2.0](LICENSE.md) |
| Non è | Un QTSP, un nuovo indirizzo PEC/REM, posta ospitata, un’app scanner |

Modifiche: [CHANGELOG.md](CHANGELOG.md). Sviluppo: [CONTRIBUTING.md](CONTRIBUTING.md) e [README in inglese](README.md) (compilazione, test, release).

## A chi serve

### Privati

Client quotidiano per uno o più indirizzi: Gmail o Microsoft 365 accanto alla PEC personale (o a una casella REM IMAP). L’archivio sta su **questo PC**: si può cercare un avviso di condominio, un file IMU o una comunicazione della PA anni dopo, senza lasciare gli originali su un server di terzi.

Uso tipico: PEC e posta ordinaria nella stessa finestra; vedere se il gestore ha accettato e depositato il messaggio; stampare o salvare un PDF; esportare l’`.eml` originale se lo chiede il Comune, la banca o il giudice.

### Studi e agenzie

Serve quando le identità certificate non devono mescolarsi: PEC personale, PEC dello studio, posta ordinaria. **Da** è un elenco delle caselle collegate, non un indirizzo digitato a mano. Un commercialista, un avvocato, un notaio, un CAF, un amministratore di condominio o uno studio UE che deposita in PA può:

- tenere ogni casella del gestore distinta, con la quota IMAP nella barra di stato;
- etichettare i messaggi **su questo PC** per pratica (condominio, fascicolo cliente) — non è una tabella di studio condivisa in rete;
- tenere insieme l’invio e le ricevute del gestore che lo richiamano;
- esportare gli originali selezionati come **fascicolo** di file `.eml` per il notaio, il giudice o lo sportello PA;
- aprire gli allegati **FatturaPA** (XML) in una vista leggibile (parser XML, non intelligenza artificiale);
- importare un profilo Thunderbird o un **PST/OST** Outlook senza installare Outlook.

Il programma non sostituisce il gestore, non emette una ricevuta qualificata e non condivide le etichette di pratica tra i PC dell’ufficio.

## Account e protocolli

In ingresso: **IMAP** (albero cartelle) o **POP3** (solo Posta in arrivo). In uscita: **SMTP**. Cifratura: automatica, SSL/TLS, STARTTLS, STARTTLS se disponibile, oppure nessuna. MailKit usa i metodi SASL che il server offre.

Si possono salvare più caselle. I profili compilano host e porte:

| Profilo | Ruolo |
|--|--|
| Gmail | IMAP/SMTP; accesso Google (OAuth2 / XOAUTH2) o password per le app |
| Outlook / Microsoft 365 | IMAP/SMTP; accesso Microsoft. Quasi tutti questi account rifiutano la password della casella |
| IT PEC — Aruba | `imaps.pec.aruba.it` / `smtps.pec.aruba.it` (POP3: `pop3s.pec.aruba.it`) |
| IT PEC — InfoCert Legalmail | `mbox.cert.legalmail.it` / `sendm.cert.legalmail.it`. Spesso lo User ID InfoCert, non l’indirizzo |
| IT/EU PEC — Namirial | `imaps.sicurezzapostale.it` / `smtps.sicurezzapostale.it` |
| IT PEC — Poste Italiane | `mail.postecert.it` |
| IT PEC — Register.it | `imap.pec-email.com` / `smtp.pec-email.com` |
| IT PEC — Libero | `mail.postacert.it.net` |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Qualunque host indicato dall’operatore |

Le caselle PEC IMAP usano la password del gestore, o una password per le app se c’è il 2FA. **AR24** (Francia), **De-Mail** (Germania), **IncaMail** (Svizzera) e **Lleida** (Spagna) di solito **non** sono IMAP. Se l’operatore ha dato host IMAP/POP3, scegli **IMAP / POP3** e inseriscili. Altrimenti questa applicazione non apre quella casella; può comunque mostrare l’evidenza ETSI REM su messaggi importati come `.eml`.

OAuth: client ID Google (termina con `.apps.googleusercontent.com`) e, per client **Web**, il **client secret** — entrambi in Impostazioni account (il secret sta in `secrets.bin`). I client pubblici Desktop non richiedono secret. Variabili `POSTCLIENT_GOOGLE_CLIENT_ID` / `POSTCLIENT_GOOGLE_CLIENT_SECRET`. Abilitare le **API Gmail**, lo scope `https://mail.google.com/`, il loopback `http://127.0.0.1` e **IMAP** in Gmail. Microsoft: ID applicazione Azure (GUID); client pubblico; redirect `http://localhost`; permessi IMAP/SMTP.

Password e token OAuth stanno accanto alle impostazioni (`DPAPI` su Windows, mode `600` su Linux/macOS), mai in `settings.json`.

## PEC italiana

La PEC è una **busta** del gestore:

- messaggio di trasporto con `X-Trasporto: posta-certificata`, `daticert.xml` e l’originale in `postacert.eml`;
- ricevute (`X-Ricevuta`) in **Posta in arrivo** e **Ricevute**: accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, rilevazione-virus, preavviso-errore-consegna.

Visualizza → **Apri busta PEC/REM** mostra il `postacert.eml` interno (il testo destinato al destinatario) invece del wrapper del gestore. La colonna Tipo indica **PEC** (trasporto) o **RIC** (ricevuta).

La PEC italiana **non ha ricevuta di lettura**. **Avvenuta consegna** significa deposito nella casella certificata del destinatario, non che una persona abbia aperto il messaggio.

## REM europea (eIDAS)

Se il messaggio porta evidenza ETSI REM (`REMEvidence` / `urn:etsi:rem`, o un nome parte `remevidence` / `rem-md`), il client lo tratta come **REM** (regione Europa). Gli eventi finiscono nella stessa colonna Consegna della PEC:

- `SubmissionAcceptance` → il QTSP ha preso in carico;
- `Delivery` / avis de réception LRE o AR24 / Zustellung De-Mail / acuse de recibo → depositato;
- `ContentConsignment` / `Retrieval` / `Abholbestätigung` De-Mail → il destinatario ha ritirato il contenuto (equivalente europeo più vicino alla “lettura”);
- rifiuto, virus, mancata consegna, `DeliveryExpiration` → fallito.

S/MIME (`smime.p7s` / `smime.p7m`) è mostrato come firmato digitalmente; non è PEC né REM.

## Colonna consegna dopo l’invio

Dopo l’SMTP, Postclient memorizza il **Message-Id** in uscita e, aprendo **Inviata**, riconcilia le ricevute da **Posta in arrivo** e **Ricevute**. Abbina, in ordine: `<msgid>` in `daticert.xml`, `X-Riferimento-Message-ID`, `In-Reply-To`, `identificativo` PEC, poi i prefissi oggetto (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

| Evidenza | Stato | Significato |
|--|--|--|
| PEC `accettazione` / `presa-in-carico`, ETSI `SubmissionAcceptance` | Accettato | Il gestore ha preso il messaggio |
| PEC `avvenuta-consegna`, ETSI `Delivery`, avis LRE/AR24, Zustellung | Consegnato | Depositato nella **casella certificata** — la PEC non certifica la lettura |
| ETSI `ContentConsignment` / `Retrieval`, `Abholbestätigung` | Ritirato | Il destinatario ha ritirato il contenuto |
| `non-accettazione`, `mancata-consegna`, virus, rifiuto ETSI | Fallito | Il gestore ha segnalato mancata accettazione o consegna |

Il client **mostra** l’evidenza dell’operatore. Non emette ricevuta qualificata, busta PEC o marca temporale.

Le notifiche del sistema segnalano nuove PEC, ricevute e REM (toast Windows, notifica macOS, `notify-send` su Linux).

## Lettura, cartelle, composizione

- L’albero segue il server (Posta in arrivo, Ricevute, Bozze, Inviata, Archivio, Indesiderata, Cestino, più cartelle personalizzate). Le cartelle di sistema non si eliminano. Si può creare una cartella, svuotarla (nel Cestino, o in modo definitivo nel Cestino), segnare tutto letto/non letto, eliminare una cartella personalizzata, trascinare i messaggi su una cartella.
- Colonne elenco: non letto, stella, allegati, consegna, tipo (PEC / RIC / REM / SIG), da, oggetto, data (locale `yyyy-MM-dd HH:mm`), etichetta di pratica.
- Vista: HTML (motore web del sistema), testo, sorgente, o **FatturaPA**. Disposizione: elenco sopra la lettura, oppure tre colonne.
- **Raggruppa conversazioni** indenta le risposte nell’elenco (Message-ID / In-Reply-To / References, profondità massima 8). Le ricevute PEC dello stesso originale stanno insieme. Non c’è un riquadro conversazioni separato.
- Azioni: rispondi, rispondi a tutti, inoltra, apri in nuova finestra, letto/non letto, stella, priorità, elimina.
- La composizione è **testo semplice**. A, Cc, Ccn sono chip di indirizzo. L’invio usa solo l’identità SMTP della casella scelta.

HTML: **WebView2** su Windows, **WebKitGTK** su Linux, **WKWebView** su macOS. Se manca il motore, si vede il corpo di testo.

Pacchetti Linux per la vista HTML:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora: `gtk3 webkit2gtk4.1 libsoup3`.

## Archivio, ricerca, etichette

Scarica messaggi indica **tutta** la cartella IMAP in SQLite (`mail.db`) più gli `.eml` su disco. POP3 riempie solo la Posta in arrivo. L’indicizzazione gira in background; la barra di stato mostra l’avanzamento.

La ricerca (cartella corrente) copre oggetto, corpo, testo degli allegati (PDF e text/XML/HTML/CSV/JSON) e etichette di pratica. Le etichette esistono solo su questo PC.

L’archivio è **solo su questo PC**. File → Apri cartella archivio / Esporta archivio copia `mail.db` e gli `.eml`. Percorsi:

| OS | Configurazione | Dati |
|--|--|--|
| Windows | `%AppData%\Postclient\` | `%LocalAppData%\Postclient\` |
| Linux | `~/.config/postclient/` | `~/.local/share/postclient/` |
| macOS | `~/Library/Application Support/Postclient/` | stesso |

Override: `POSTCLIENT_CONFIG` / `POSTCLIENT_DATA_DIR`. Lo zip portatile Windows con `installType: portable` in `settings.json` tiene la config accanto all’eseguibile.

La **QUOTA** IMAP del gestore (se il server la espone) è in barra di stato. È lo spazio della casella presso l’operatore, non uno storage cloud di questa applicazione.

## Importazione, esportazione, stampa

| Azione | Risultato |
|--|--|
| Importa EML | Originali nella cartella corrente |
| Importa Thunderbird | Store mbox del profilo Thunderbird |
| Importa Outlook PST/OST | Posta senza installare Outlook |
| Stampa | HTML leggibile del messaggio (eventualmente sbustato) |
| Salva PDF | Lo stesso contenuto in PDF |
| Salva allegati ZIP | Tutti gli allegati del messaggio aperto |
| Esporta EML selezionati (fascicolo) | Gli `.eml` originali — ciò che notaio o PA possono conservare come messaggio |

I messaggi aperti sono anche copiati come `.eml` nella cartella dati. Gli `.eml` sono gli originali; PDF e stampa sono una copia di lettura.

## Lingua dell’interfaccia

Visualizza → Lingua: **English** o **Italiano**. I nomi cartella del server (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) sono mappati su Posta in arrivo / Inviata / … nella lingua dell’UI.

## Download

Quando esiste una GitHub Release: zip portatile e setup Windows (`postclient-{version}.exe`, self-contained — niente SDK .NET sul PC), Flatpak Linux, DMG macOS (`osx-arm64` e `osx-x64`). Le build macOS non sono firmate: al primo avvio **Apri** dal menu contestuale.

### Linux (Flatpak)

```bash
flatpak install --user ./postclient-{version}.flatpak
flatpak run eu.postclient.desktop
```

Compilazione da sorgente, test e procedura di release: [README in inglese](README.md).

## Licenza

Apache License 2.0. Copyright 2026 Maksym Sadovnychyy (MAKS-IT).
