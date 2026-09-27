# Postclient — client PEC, REM e IMAP da scrivania

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-49.7%25-yellowgreen)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-47%25-yellowgreen)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-52.6%25-yellowgreen)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Lingua:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

L’interfaccia del programma è in inglese, italiano, francese, tedesco o spagnolo. Questi documenti descrivono lo stesso **client di posta elettronica da scrivania** open source per chi invia e conserva **posta elettronica certificata (PEC)**, **registered electronic mail (REM) eIDAS** e posta **IMAP / POP3 / SMTP** ordinaria su Windows, Linux e macOS.

Postclient è un **client locale**, non una webmail. Si collega con IMAP, POP3 e SMTP alle caselle che già possiedi: Gmail, Microsoft 365, **PEC** italiana presso un gestore qualificato (Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero, e IMAP su dominio personalizzato), e **REM** europea quando l’operatore espone IMAP/SMTP. Le ricevute restano su questo PC come `.eml` più un archivio SQLite ricercabile.

**Non** è un prestatore di servizi fiduciari qualificati (QTSP), **non** è un gestore PEC, **non** è una marca temporale e **non** è una casella in cloud. Buste e ricevute le emette il gestore / QTSP. Questo programma le apre, mostra l’evidenza di consegna e tiene gli originali sulla macchina.

| | |
|--|--|
| Tipo | Client di posta da scrivania per PEC, REM e IMAP |
| Sistemi | Windows, Linux, macOS (Apple Silicon e Intel) |
| Stack | C#, .NET 10, Avalonia, MailKit |
| Licenza | [Apache 2.0](LICENSE.md) |
| Non è | Un QTSP, un nuovo indirizzo PEC/REM, posta ospitata, un’app scanner |

Modifiche: [CHANGELOG.md](CHANGELOG.md). Sviluppo: [CONTRIBUTING.md](CONTRIBUTING.md) e [README in inglese](README.md) (compilazione, test, release).

## Pensato per la posta certificata sul desktop

La posta da scrivania e la webmail trattano di solito una **busta** PEC o una parte di evidenza ETSI REM come un allegato qualunque. Postclient è scritto intorno a quella evidenza:

- **Apre buste PEC / REM** — legge il `postacert.eml` interno (il messaggio destinato al destinatario) invece del wrapper del gestore; colonna Tipo **PEC**, **RIC**, **REM** o **SIG** (firmato digitalmente).
- **Colonna consegna dall’evidenza dell’operatore** — accettazione, avvenuta-consegna, ETSI `SubmissionAcceptance` / `Delivery` / `Retrieval`, LRE e avis AR24, Zustellung e Abholbestätigung De-Mail, acuse de recibo. Dopo l’invio SMTP, **Inviata** viene abbinata alle ricevute in Posta in arrivo/Ricevute tramite Message-Id, `daticert.xml` e prefissi oggetto. La PEC italiana **non ha ricevuta di lettura**: avvenuta consegna è il deposito nella casella certificata.
- **Anteprima FatturaPA** — XML della fattura elettronica italiana in una vista leggibile (parser XML, non intelligenza artificiale).
- **Fascicolo di `.eml` originali** — esporta i messaggi selezionati come i file che un notaio, uno sportello PA, una banca o un giudice può conservare.
- **Da è un selettore di casella** — non si digita un From libero; l’invio usa l’identità SMTP dell’account collegato.
- **Etichette di pratica su questo PC** — condominio, fascicolo cliente, avviso IMU. Le etichette sono locali, non un database di studio in rete.
- **Archivio locale di tutta la cartella** — Scarica messaggi memorizza **tutta** la cartella IMAP in `mail.db` più gli `.eml` su disco (non una finestra degli ultimi N). FTS su oggetto, corpo, testo degli allegati PDF/XML/HTML/CSV/JSON e etichette.
- **Ricerca per significato sul dispositivo (RAG)** — EmbeddingGemma 300M opzionale in **Impostazioni → Indici…**. I vettori restano in `mail.db`. La posta non lascia la macchina. Si possono ricostruire o riparare gli indici per parole e per significato.
- **Regole tra caselle** — sposta, elimina, flag, etichetta; la cartella di destinazione può stare su un altro account, compreso un **archivio cartella**. Importa JSON o un file `.rwz` legacy; l’export è JSON.
- **Archivi cartella** — crea, collega, sposta o sgancia una directory con `.eml` e il proprio `mail.db` (parole e significato). **Importa PST…** copia la posta in un archivio; il `.pst` non diventa una casella. Percorso predefinito: `%LocalAppData%\Postclient\stores\{nome}` (portatile: `{installazione}/data/stores/{nome}`).
- **Easy Migration** — **File → Easy Migration → Crea bundle…** chiede la passphrase due volte nella stessa finestra, poi Salva. Il `.postbundle` contiene posta, account, segreti e la cartella modelli condivisa. **Apri bundle…** lo ripristina nelle cartelle Postclient di questo PC. La sincronizzazione in background resta spenta.
- **Sincronizzazione in background** — **Impostazioni → Sincronizzazione in background per questo account…**: un servizio all’avvio (servizio Windows, unità systemd di sistema su Linux). Sincronizza ogni casella salvata senza che nessuno abbia effettuato l’accesso.
- **Spam** — **Segna come spam** ricorda il messaggio su questo PC e insegna a ogni casella, anche dopo che il messaggio o la casella non ci sono più. La posta simile è solo un indizio. **Non spam** cancella quella memoria e riporta il messaggio in Posta in arrivo. Spostare in Indesiderata non insegna il filtro.
- **Pacchetti per Paese** — **Impostazioni → Funzionalità** accende o spegne Italia (PEC, FatturaPA, fascicolo), Europa (REM eIDAS), Francia, Germania, Spagna e Svizzera. L’IMAP ordinario resta disponibile.

## A chi serve

### Privati

Client quotidiano per uno o più indirizzi: Gmail o Microsoft 365 accanto a una **PEC** personale (Italia) o a una casella **REM** IMAP (UE). Cerca un avviso di condominio, un file IMU o una comunicazione della PA anni dopo, senza lasciare gli originali sul server di terzi.

Uso tipico: posta certificata e ordinaria nella stessa finestra; vedere se il gestore ha accettato e depositato la PEC; stampare o salvare un PDF; esportare l’`.eml` originale se lo chiede il Comune, la banca o il giudice.

### Studi e agenzie

Serve quando le identità certificate non devono mescolarsi: PEC personale, PEC dello studio, posta ordinaria. Un commercialista, un avvocato, un notaio, un CAF, un amministratore di condominio o uno studio UE che deposita in PA può tenere ogni casella del gestore distinta (quota IMAP **QUOTA** in barra di stato), raggruppare un invio con le sue ricevute ed esportare un fascicolo senza caricare la pratica su un archivio di terzi.

Il programma non sostituisce il gestore, non emette una ricevuta qualificata e non condivide le etichette tra i PC dell’ufficio.

## Account e protocolli

In ingresso: **IMAP** (albero cartelle), **POP3** (solo Posta in arrivo), o un **archivio cartella** locale. Un `.pst` Unicode si può ancora **importare** in un archivio. In uscita: **SMTP**. Cifratura: automatica, SSL/TLS, STARTTLS, STARTTLS se disponibile, oppure nessuna. MailKit usa i metodi SASL che il server offre.

Si possono usare più caselle insieme. I profili compilano host e porte:

| Profilo | Ruolo |
|--|--|
| Gmail | IMAP/SMTP; accesso Google (OAuth2 / XOAUTH2) o password per le app Gmail |
| Microsoft 365 | IMAP/SMTP; accesso Microsoft. Quasi tutti questi account rifiutano la password della casella |
| IT PEC — Aruba | `imaps.pec.aruba.it` / `smtps.pec.aruba.it` (POP3: `pop3s.pec.aruba.it`) |
| IT PEC — InfoCert Legalmail | `mbox.cert.legalmail.it` / `sendm.cert.legalmail.it`. Spesso lo User ID InfoCert, non l’indirizzo |
| IT/EU PEC — Namirial | `imaps.sicurezzapostale.it` / `smtps.sicurezzapostale.it` |
| IT PEC — Poste Italiane | `mail.postecert.it` |
| IT PEC — Register.it | `imap.pec-email.com` / `smtp.pec-email.com` |
| IT PEC — Libero | `mail.postacert.it.net` |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Qualunque host indicato dall’operatore. Imposta il **tipo di casella** su PEC italiana o REM UE se quella casella è certificata (anche con dominio personalizzato) |
| Archivio locale | Cartella su disco (`postclient.store.json` + `mail.db` + `.eml`). Senza password e senza SMTP. Crea, collega, sposta o sgancia dal menu File. |

Le caselle IMAP certificate usano la password del gestore, o una password per le app se c’è il 2FA. **AR24** (Francia), **De-Mail** (Germania), **IncaMail** (Svizzera) e **Lleida** (Spagna) di solito **non** sono IMAP. Se l’operatore ha dato host IMAP/POP3, scegli **IMAP / POP3**. Altrimenti questa applicazione non apre quella casella; può comunque mostrare l’evidenza ETSI REM su messaggi importati come `.eml`.

Gmail e Microsoft 365 accedono tramite **Identity Hub** (`https://identity.maks-it.com`). Impostazioni account apre `/desktop-login?provider=Google` o `Microsoft`. Il JWT Hub e il refresh token restano in `secrets.bin`; IMAP usa un mailbox-token a breve scadenza (XOAUTH2). All’avvio il client rinnova la sessione Hub da quel refresh token, così Gmail e Outlook restano collegati dopo la chiusura. Se una build precedente ha salvato solo il JWT, accedi di nuovo da Impostazioni account. Override: `POSTCLIENT_IDENTITY_HUB`. La password resta valida per PEC e IMAP generico.

Password e token OAuth stanno accanto alle impostazioni (`DPAPI` su Windows, mode `600` su Linux/macOS), mai in `settings.json`.

## PEC italiana (posta elettronica certificata)

La PEC è una **busta** del gestore:

- messaggio di trasporto con `X-Trasporto: posta-certificata`, `daticert.xml` e l’originale in `postacert.eml`;
- ricevute (`X-Ricevuta`) in **Posta in arrivo** e **Ricevute**: accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, rilevazione-virus, preavviso-errore-consegna.

Visualizza → **Apri busta PEC/REM** mostra il `postacert.eml` interno. La colonna Tipo indica **PEC** (trasporto) o **RIC** (ricevuta).

La PEC italiana **non ha ricevuta di lettura**. **Avvenuta consegna** significa deposito nella casella certificata del destinatario, non che una persona abbia aperto il messaggio.

## REM europea (registered electronic mail eIDAS)

Se il messaggio porta evidenza ETSI REM (`REMEvidence` / `urn:etsi:rem`, o un nome parte `remevidence` / `rem-md`), il client lo tratta come **REM**. Gli eventi finiscono nella stessa colonna Consegna della PEC:

- `SubmissionAcceptance` → il QTSP ha preso in carico;
- `Delivery` / avis de réception LRE o AR24 / Zustellung De-Mail / acuse de recibo → depositato;
- `ContentConsignment` / `Retrieval` / `Abholbestätigung` De-Mail → il destinatario ha ritirato il contenuto (equivalente europeo più vicino alla “lettura”);
- rifiuto, virus, mancata consegna, `DeliveryExpiration` → fallito.

S/MIME (`smime.p7s` / `smime.p7m`) è mostrato come firmato digitalmente; non è PEC né REM.

## Colonna consegna dopo l’invio

Dopo l’SMTP, Postclient memorizza il **Message-Id** in uscita e, aprendo **Inviata**, riconcilia le ricevute da **Posta in arrivo** e **Ricevute**. L’attesa vale solo per le caselle di tipo PEC o REM (un preset, oppure IMAP/POP3 con quel tipo). Gmail, Microsoft 365 e IMAP ordinario risultano solo inviati. Abbina, in ordine: `<msgid>` in `daticert.xml`, `X-Riferimento-Message-ID`, `In-Reply-To`, `identificativo` PEC, poi i prefissi oggetto (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

| Evidenza | Stato | Significato |
|--|--|--|
| PEC `accettazione` / `presa-in-carico`, ETSI `SubmissionAcceptance` | Accettato | Il gestore ha preso il messaggio |
| PEC `avvenuta-consegna`, ETSI `Delivery`, avis LRE/AR24, Zustellung | Consegnato | Depositato nella **casella certificata** — la PEC non certifica la lettura |
| ETSI `ContentConsignment` / `Retrieval`, `Abholbestätigung` | Ritirato | Il destinatario ha ritirato il contenuto |
| `non-accettazione`, `mancata-consegna`, virus, rifiuto ETSI | Fallito | Il gestore ha segnalato mancata accettazione o consegna |

Il client **mostra** l’evidenza dell’operatore. Non emette ricevuta qualificata, busta PEC o marca temporale.

Le notifiche del sistema segnalano nuove PEC, ricevute e REM (toast Windows, notifica macOS, `notify-send` su Linux).

## Lettura, cartelle, composizione

- L’albero segue il server (Posta in arrivo, Ricevute, Bozze, Inviata, Archivio, Indesiderata, Cestino, più cartelle personalizzate). I percorsi IMAP nidificati sono cartelle nidificate, comprese le etichette Gmail `[Gmail]/…` sotto `[Gmail]`. Bozze, Inviata e Cestino che il server chiama `INBOX.Drafts` / `INBOX.Sent` / `INBOX.Trash` stanno accanto a Posta in arrivo, non sotto. Gmail usa `/` come separatore: un’etichetta come `P.IVA` resta una sola cartella. Le cartelle di sistema non si eliminano. Si può creare una cartella, svuotarla (nel Cestino, o in modo definitivo nel Cestino), segnare tutto letto/non letto, eliminare una cartella personalizzata, trascinare i messaggi su una cartella. Quali account e cartelle nidificate sono aperti o chiusi resta in `settings.json`.
- Colonne elenco: non letto, stella, allegati, consegna, tipo (PEC / RIC / REM / SIG), da, oggetto, data (locale `yyyy-MM-dd HH:mm`), dimensione, etichetta di pratica.
- Vista: HTML (motore web del sistema), testo, sorgente, o **FatturaPA**. Disposizione: elenco sopra la lettura, oppure tre colonne (cartelle, elenco, lettura).
- **Raggruppa conversazioni** indenta le risposte nell’elenco (Message-ID / In-Reply-To / References, profondità massima 8). Le ricevute PEC dello stesso originale stanno insieme. Non c’è un riquadro conversazioni separato.
- Azioni: rispondi, rispondi a tutti, inoltra, apri in nuova finestra, letto/non letto, stella, priorità, segna come spam, non spam, elimina. **Maiusc+clic** e **Ctrl+clic** selezionano più messaggi o cartelle. Elimina (barra, **Canc**, svuota cartella) sposta la selezione nel Cestino. Dal Cestino l’eliminazione è definitiva.
- La composizione è **testo semplice**. A, Cc, Ccn sono chip di indirizzo. Gli allegati sono elencati nel compositore. **Invia allegati come ZIP** impacca i file trascinati (password opzionale). L’invio usa solo l’identità SMTP della casella scelta.

HTML: **WebView2** su Windows (profilo sotto la cartella dati), **WebKitGTK** su Linux, **WKWebView** su macOS. Se manca il motore, si vede il corpo di testo.

Pacchetti Linux per la vista HTML:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora: `gtk3 webkit2gtk4.1 libsoup3`.

## Archivio locale, ricerca per parole e indice di significato

Scarica messaggi indica **tutta** la cartella IMAP in SQLite (`mail.db`) più gli `.eml` su disco. POP3 riempie solo la Posta in arrivo. L’indicizzazione gira in background; la barra di stato mostra l’avanzamento.

- **Impostazioni → Indici…**: indice per parole (FTS) e, se vuoi, EmbeddingGemma 300M (~300 MB) per il significato. Il pacchetto ONNX si scarica una volta nella cartella della macchina (`ProgramData\MaksIT\Postclient\models`, o `/var/lib/maksit/postclient/models` su Linux) ed è condiviso da ogni account e dal servizio di sincronizzazione. I vettori restano in `mail.db` su questo PC. Dispositivo: Automatico / CPU / GPU (DirectML su Windows). Ricrea o ripara gli indici se la ricerca è storta. I pesi usano i termini Google Gemma. **Segni spam…** elenca ciò che il filtro ha imparato.

La ricerca (cartella corrente) copre oggetto, corpo, testo degli allegati (PDF e text/XML/HTML/CSV/JSON), etichette di pratica e il significato quando il modello è pronto. La casella di ricerca ha una × che cancella il testo. Le etichette esistono solo su questo PC.

L’archivio è **solo su questo PC**. File → Apri cartella archivio / Esporta archivio copia `mail.db` e gli `.eml`. Percorsi:

| OS | Configurazione | Dati |
|--|--|--|
| Windows | `%AppData%\Postclient\` | `%LocalAppData%\Postclient\` |
| Linux | `~/.config/postclient/` | `~/.local/share/postclient/` |
| macOS | `~/Library/Application Support/Postclient/` | stesso |

Override: `POSTCLIENT_CONFIG` / `POSTCLIENT_DATA_DIR`. Lo zip portatile Windows con `installType: portable` in `settings.json` tiene la config accanto all’eseguibile.

La **QUOTA** IMAP del gestore (se il server la supporta) è una barra sottile sul nodo di quell’account nell’albero. POP3 mostra lo spazio usato da STAT quando il server non ha un limite. Gli archivi locali non hanno quota. È lo spazio della casella presso l’operatore, non uno storage cloud di questa applicazione.

## Importazione, esportazione, stampa

| Azione | Risultato |
|--|--|
| Importa EML | Originali nella cartella corrente |
| Importa mbox | Store mbox locali da un profilo di posta da scrivania |
| Importa `.pst` / `.ost` | Copia la posta Outlook **in un archivio cartella** (percorso proposto sotto `stores/` nei dati app). Un secondo import nello stesso archivio salta i duplicati per Message-ID. |
| Nuovo archivio | Menu File: cartella vuota con `postclient.store.json` e `mail.db`. |
| Collega archivio | Menu File: cartella che ha già `postclient.store.json`. |
| Sposta / Sgancia archivio | Copia l’intera directory, oppure la toglie dall’elenco. La cartella resta sul disco quando si sgancia. |
| Conservazione | **Impostazioni → Conservazione…**: giorni per cartella (`0` = per sempre). Alla scadenza la posta della cartella va nel Cestino dopo Scarica messaggi. Sul Cestino, `0` lo tiene per sempre e un numero di giorni elimina in modo permanente il Cestino più vecchio. |
| Sincronizzazione in background | **Impostazioni → Sincronizzazione in background per questo account…**: servizio all’avvio (servizio Windows, unità systemd di sistema su Linux). Sincronizza ogni casella salvata senza accesso. **Condividi questo account** fa solo sì che le altre persone su questo PC aprano quella casella. |
| Account condiviso | **Impostazioni account → Condividi questo account con gli altri utenti di questo PC**: quella casella passa nella cartella della macchina (`ProgramData\MaksIT\Postclient`, o `Public` se ProgramData non è scrivibile; `/var/lib/maksit/postclient` su Linux; `/Users/Shared/MaksIT/Postclient` su macOS). Gli altri utenti la vedono aprendo Postclient. Le password non vengono copiate. |
| Easy Migration | **File → Easy Migration → Crea bundle…** chiede la passphrase due volte nella stessa finestra, poi Salva. Il `.postbundle` contiene posta, account, segreti e la cartella modelli condivisa. **Apri bundle…** lo ripristina su questo PC e riscrive i percorsi degli archivi. La sincronizzazione in background resta spenta. |
| Segni spam | **Segna come spam** ricorda il messaggio su questo PC e insegna a ogni casella, anche dopo che il messaggio o la casella non ci sono più. La posta simile è solo un indizio. **Non spam** cancella quella memoria e riporta il messaggio in Posta in arrivo. Spostare in Indesiderata non insegna il filtro. **Impostazioni → Indici… → Segni spam…** elenca ciò che è stato imparato. |
| Aggiornamenti e registro | **Aiuto → Controlla aggiornamenti…** chiede a GitHub l’ultima release e può scaricare il pacchetto per questo sistema. **Aiuto → Registro** apre una finestra copiabile sulla cartella dei log. **Aiuto → Informazioni su Postclient** elenca Info, Privacy, Security e Support su maks-it.com. |
| Regole | **Impostazioni → Regole**: **Importa regole…** legge l’export JSON di questa app o un file `.rwz` legacy. **Esporta regole…** scrive JSON. Ogni regola è legata a una **casella**; la cartella di destinazione può stare su un’altra casella (anche un archivio). Partono su Scarica messaggi, in import e da **Esegui tutte le regole**. |
| Stampa | HTML leggibile del messaggio (eventualmente sbustato) |
| Salva PDF | Lo stesso contenuto in PDF |
| Salva allegati ZIP | Tutti gli allegati del messaggio aperto |
| Esporta EML selezionati (fascicolo) | Gli `.eml` originali — ciò che notaio o PA possono conservare come messaggio |

I messaggi aperti sono anche copiati come `.eml` nella cartella dati. Gli `.eml` sono gli originali; PDF e stampa sono una copia di lettura.

## Lingua dell’interfaccia

Visualizza → Lingua: **English**, **Italiano**, **Français**, **Deutsch** o **Español**. I nomi cartella del server (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) sono mappati su Posta in arrivo / Inviata / … nella lingua dell’UI.

## Pacchetti per Paese

**Impostazioni → Funzionalità** è una matrice di **sola posta certificata**. IMAP/POP3 ordinario, ricerca, etichette di pratica, import mbox/`.pst` e file dati collegati restano sempre disponibili. Le righe sono funzioni certificate; le colonne sono **Italia**, **Europa**, **Francia**, **Germania**, **Spagna**, **Svizzera**. La casella in intestazione accende o spegne tutta la colonna.

| Pacchetto | Cosa sblocca | Predefinito |
|--|--|--|
| Italia | Busta PEC / ricevute / consegna, profili PEC, anteprima FatturaPA, fascicolo | Acceso |
| Europa | Evidenza REM eIDAS, profilo Intesi | Acceso |
| Francia / Germania / Spagna / Svizzera | Etichette di evidenza (AR24/LRE, De-Mail, Lleida, IncaMail). Quelle reti di solito non sono IMAP. | Spento |

Spegnere un pacchetto nasconde menu, colonne e profili. Le caselle già configurate restano; sparisce solo l’interfaccia extra.

## Download

Quando esiste una GitHub Release: zip portatile e setup Windows (`postclient-{version}.exe`, self-contained — niente SDK .NET sul PC; percorso predefinito `C:\Program Files\MaksIT\Postclient`), Flatpak Linux, DMG macOS (`osx-arm64` e `osx-x64`). Le build macOS non sono firmate: al primo avvio **Apri** dal menu contestuale.

### Linux (Flatpak)

```bash
flatpak install --user ./postclient-{version}.flatpak
flatpak run eu.postclient.desktop
```

Compilazione da sorgente, test e procedura di release: [README in inglese](README.md).

## Licenza

Apache License 2.0. Copyright 2026 Maksym Sadovnychyy (MAKS-IT).
