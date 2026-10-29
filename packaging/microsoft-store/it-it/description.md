# Microsoft Store description (it-it)

Paste into Partner Center → Store listings → **Italian (Italy)**. Plain text only. URLs in these fields are not clickable; put the privacy policy, support site, and license in their own submission fields.

Leave **What's new in this version** empty on the first submission. Use it only for a later update (1,500 characters).

The other languages are listed in [../description.md](../description.md). Hardware checkboxes stay in [CONTRIBUTING.md](../../../CONTRIBUTING.md) under **System requirements (Properties)**.

## Short description

Shown at the top of the listing. The field allows 1,000 characters. Keep this under 270 so every Store view shows the full sentence.

```text
Client di posta per le caselle che hai già. Legge e invia con IMAP, POP3 e SMTP, compresa la PEC italiana e la REM europea. Le ricevute restano su questo PC. La ricerca per significato, se la attivi, resta sul dispositivo e non carica la posta.
```

## Description

Required. Up to 10,000 characters. This text is different from the short description so the page does not repeat the same paragraph.

```text
Tieni la posta ordinaria e la posta certificata nella stessa finestra su Windows. Postclient si collega al gestore che usi già. Non rilascia un indirizzo PEC, una ricevuta qualificata o una marca temporale.

Collegati con IMAP, POP3 o SMTP. I profili compilano host e porte per Gmail, Microsoft 365, PEC Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero e Intesi Group. Qualsiasi altro host è un account personalizzato. Imposta il tipo di casella su PEC italiana o REM europea quando un dominio proprio è certificato. Un archivio cartella è una directory su questo PC: niente password e niente SMTP. Più caselle restano aperte.

L’albero delle cartelle segue il server, incluse le cartelle nidificate e le etichette di Gmail. Le cartelle standard che il server tiene sotto INBOX stanno accanto a Posta in arrivo. Puoi creare una cartella, svuotarla, segnare tutto come letto o non letto e trascinare i messaggi su una cartella. L’elenco mostra non letto, stella, allegati, consegna, tipo, da, oggetto, data, dimensione e un’etichetta di pratica. Apri HTML, testo semplice, la sorgente o una fattura elettronica italiana. Raggruppa conversazioni indenta le risposte. Rispondi, rispondi a tutti, inoltra, segna, segna come spam ed elimina. Elimina sposta la posta nel Cestino. Eliminare dal Cestino è definitivo. La composizione è testo semplice. A, Cc e Ccn sono indirizzi. L’invio usa solo la casella selezionata. Da è quella casella. Non si digita un mittente libero.

La posta certificata è mostrata come evidenza, non come un allegato qualunque. Apri il messaggio di trasporto PEC e leggi il postacert.eml interno. La colonna Tipo segna PEC, una ricevuta, REM o una firma digitale. La colonna Consegna legge accettazione, avvenuta consegna, gli eventi REM ETSI e le etichette di ricevuta francesi, tedesche e spagnole quando quei pacchetti sono accesi. La PEC italiana non ha ricevuta di lettura: avvenuta consegna significa che il messaggio è stato depositato nella casella certificata. Dopo l’invio, Inviata si abbina alle ricevute in Posta in arrivo e Ricevute tramite Message-Id, daticert.xml e il prefisso dell’oggetto. Questo programma mostra quell’evidenza. Non emette una ricevuta qualificata, una busta PEC o una marca temporale.

Scarica messaggi memorizza l’intera cartella IMAP su questo PC, come mail.db più i file .eml originali. La ricerca copre oggetto, corpo, il testo estratto dagli allegati comuni e le etichette di pratica. La ricerca per significato è facoltativa. Un modello si scarica una volta su questo PC, i vettori restano in mail.db e la posta non viene inviata a un servizio di intelligenza artificiale nel cloud. Le regole partono in sincronizzazione e in importazione. La cartella di destinazione può stare su un altro account. Importa un file di regole o esporta JSON.

Le azioni su file coprono l’import di EML e mbox, un PST di Outlook copiato in un archivio cartella, la conservazione per cartella, un bundle di migrazione bloccato da passphrase e l’esportazione dei file .eml originali. Segna come spam ricorda quel messaggio su questo computer e può suggerire posta simile in ogni casella. I pacchetti paese nascondono gli extra di posta certificata che non usi. L’IMAP ordinario resta disponibile. L’interfaccia è in inglese, italiano, francese, tedesco o spagnolo.

Password e token di accesso restano accanto alle impostazioni, non dentro settings.json. Gmail e Microsoft 365 usano Identity Hub. Layout e account sono memorizzati su questo computer.

Ti serve Windows 10 o Windows 11 a 64 bit e una casella che hai già, oppure un archivio cartella. Installa WebView2 se vuoi la posta HTML. Altrimenti viene mostrato il testo. Il modello di ricerca per significato è un download a parte, solo se attivi quell’indice.
```

Product features (up to 20 bullets) are in [product-features.md](product-features.md). Keywords are in [keywords.md](keywords.md). Copyright, additional license terms, and Developed by are in [additional-info.md](additional-info.md).

## Additional system requirements

Optional text lines, separate from the Properties hardware table. Up to 11 minimum and 11 recommended. Each line is at most 200 characters.

Minimum:

```text
Windows 10 o Windows 11 a 64 bit
Una casella IMAP, POP3 o SMTP che hai già, oppure una cartella locale di messaggi
Accesso di rete a quel server quando l’account non è un archivio cartella
```

Recommended:

```text
WebView2, così la posta HTML usa la vista pagina. Se manca, viene mostrato il testo
Spazio su disco per l’archivio locale. Il modello di ricerca per significato, se lo attivi, pesa circa 300 MB
```

Category is set once on the product, not per language: Productivity.
