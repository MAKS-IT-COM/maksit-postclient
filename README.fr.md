# Postclient — client de bureau PEC, REM et IMAP

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-49.7%25-yellowgreen)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-47%25-yellowgreen)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-52.6%25-yellowgreen)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Langue:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

L’interface de l’application est en anglais, italien, français, allemand ou espagnol. Ces documents décrivent le même **client de messagerie de bureau** open source pour qui envoie et conserve de la **posta elettronica certificata (PEC)**, du **courrier électronique recommandé eIDAS (REM)** et du courrier **IMAP / POP3 / SMTP** ordinaire sous Windows, Linux et macOS.

Postclient est un **client local**, pas une webmail. Il parle IMAP, POP3 et SMTP aux comptes que vous avez déjà : Gmail, Microsoft 365, **PEC** italienne chez un gestionnaire qualifié (Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero, et IMAP sur domaine personnalisé), et **REM** européenne lorsque l’opérateur publie IMAP/SMTP. Les avis restent sur cet ordinateur en `.eml` plus une archive SQLite interrogeable.

Ce n’est **pas** un prestataire de services de confiance qualifié (QTSP), **pas** un opérateur PEC, **pas** une autorité d’horodatage et **pas** une boîte hébergée. Les enveloppes et accusés sont émis par le gestionnaire / QTSP. Ce programme les ouvre, affiche la preuve de livraison et conserve les originaux sur la machine.

| | |
|--|--|
| Nature | Client de messagerie de bureau pour PEC, REM et IMAP |
| OS | Windows, Linux, macOS (Apple Silicon et Intel) |
| Stack | C#, .NET 10, Avalonia, MailKit |
| Licence | [Apache 2.0](LICENSE.md) |
| N’est pas | Un QTSP, une nouvelle adresse PEC/REM, du courrier cloud, une appli scanner |

Journal : [CHANGELOG.md](CHANGELOG.md). Développement : [CONTRIBUTING.md](CONTRIBUTING.md) et le [README anglais](README.md) (compilation, tests, publication).

## Conçu pour le courrier certifié sur le bureau

La messagerie de bureau et le webmail traitent en général une **enveloppe** PEC ou une preuve ETSI REM comme une pièce jointe quelconque. Postclient est écrit autour de cette preuve :

- **Ouvrir les enveloppes PEC / REM** — lire le `postacert.eml` interne (le message destiné au destinataire) plutôt que l’enveloppe du gestionnaire ; colonne Type **PEC**, **RIC**, **REM** ou **SIG** (signé numériquement).
- **Colonne livraison depuis la preuve opérateur** — accettazione, avvenuta-consegna, ETSI `SubmissionAcceptance` / `Delivery` / `Retrieval`, avis LRE et AR24, Zustellung et Abholbestätigung De-Mail, acuse de recibo. Après SMTP, **Envoyés** est rapproché des avis Boîte de réception/Ricevute par Message-Id, `daticert.xml` et préfixes d’objet. La PEC italienne **n’a pas d’accusé de lecture** : avvenuta consegna est le dépôt dans la boîte certifiée.
- **Aperçu FatturaPA** — XML de facturation électronique italienne en vue lisible (analyseur XML, pas d’IA générative).
- **Fascicule d’`.eml` originaux** — exporter les messages sélectionnés comme les fichiers qu’un notaire, un guichet, une banque ou un juge peut conserver.
- **De est un sélecteur de boîte** — pas d’expéditeur saisi librement ; l’envoi utilise l’identité SMTP du compte connecté.
- **Étiquettes de dossier sur cet ordinateur** — copropriété, dossier client, avis fiscal. Les étiquettes sont locales, pas une base de cabinet en réseau.
- **Archive locale du dossier entier** — Get Messages stocke **tout le dossier IMAP** dans `mail.db` plus les `.eml` sur disque (pas une fenêtre des N derniers). FTS sur objet, corps, texte des pièces PDF/XML/HTML/CSV/JSON et étiquettes.
- **Recherche de sens sur l’appareil (RAG)** — EmbeddingGemma 300M optionnel dans **Paramètres → Indices…**. Les vecteurs restent dans `mail.db`. Le courrier ne quitte pas la machine. Reconstruire ou réparer les indices mot-clé et sens.
- **Règles entre boîtes** — déplacer, supprimer, marquer, étiqueter ; le dossier cible peut vivre sur un autre compte, y compris un fichier `.pst` / `.ost` attaché. Import JSON ou fichier `.rwz` historique ; l’export est JSON.
- **Ouvrir un `.pst` / `.ost` comme boîte** — le magasin Unicode est inscriptible (dossiers, drapeaux, déplacement, suppression). Un magasin hors ligne est copié vers un `.pst` Unicode à la première écriture. Aucun autre programme de messagerie n’est requis pour lire le fichier.
- **Packs pays** — **Paramètres → Fonctionnalités** active ou désactive l’Italie (PEC, FatturaPA, fascicule), l’Europe (REM eIDAS), la France, l’Allemagne, l’Espagne et la Suisse. L’IMAP ordinaire reste disponible.

## Public

### Particuliers

Client quotidien pour une ou plusieurs adresses : Gmail ou Microsoft 365 à côté d’une **PEC** personnelle (Italie) ou d’une boîte **REM** IMAP (UE). Retrouver un avis de copropriété, un dossier fiscal ou un courrier d’administration des années plus tard, sans laisser les originaux chez un tiers.

Usage typique : courrier certifié et courrier ordinaire dans la même fenêtre ; voir si le gestionnaire a accepté et déposé le message ; imprimer ou enregistrer un PDF ; exporter l’`.eml` original si la mairie, la banque ou le juge le demande.

### Cabinets et agences

Utile lorsque les identités certifiées ne doivent pas se mélanger : PEC personnelle, PEC du cabinet, courrier ordinaire. Un avocat, un expert-comptable, un notaire, un syndic ou un cabinet UE qui dépose auprès de l’administration peut garder chaque boîte d’opérateur séparée (**QUOTA** IMAP dans la barre d’état), regrouper un envoi avec ses avis et exporter un fascicule sans charger le dossier chez un tiers.

Le programme ne remplace pas le gestionnaire, n’émet pas d’accusé qualifié et ne partage pas les étiquettes entre les postes du cabinet.

## Comptes et protocoles

Entrée : **IMAP** (arborescence), **POP3** (boîte de réception seulement) ou un **`.pst` / `.ost`** attaché. Le `.pst` Unicode est inscriptible. Sortie : **SMTP**. Chiffrement : auto, SSL/TLS, STARTTLS, STARTTLS si disponible, ou aucun. MailKit utilise les méthodes SASL annoncées par le serveur.

Plusieurs boîtes peuvent être enregistrées. Les profils remplissent hôtes et ports :

| Profil | Rôle |
|--|--|
| Gmail | IMAP/SMTP ; connexion Google (OAuth2 / XOAUTH2) ou mot de passe d’application Gmail |
| Microsoft 365 | IMAP/SMTP ; connexion Microsoft. La plupart de ces comptes refusent le mot de passe de la boîte |
| IT PEC — Aruba, Legalmail, Namirial, Poste Italiane, Register.it, Libero | Hôtes IMAP/SMTP du gestionnaire italien (voir le [README anglais](README.md) pour les noms d’hôte) |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Tout hôte fourni par l’opérateur. Indiquez le **type de boîte** PEC italienne ou REM UE si cette boîte est certifiée (y compris avec un domaine personnalisé) |
| Magasin local | Dossier sur disque (`postclient.store.json` + `mail.db` + `.eml`). Sans mot de passe ni SMTP. Créer, attacher, déplacer ou détacher depuis le menu Fichier. |

Les boîtes IMAP certifiées utilisent le mot de passe du gestionnaire, ou un mot de passe d’application si la 2FA est active. **AR24**, **La Poste LRE** et d’autres LRE françaises ne sont en général **pas** de l’IMAP. **De-Mail** (Allemagne), **IncaMail** (Suisse) et **Lleida** (Espagne) non plus. Si l’opérateur a fourni des hôtes IMAP/POP3, choisissez **IMAP / POP3**. Sinon ce client n’ouvre pas cette boîte ; il peut encore afficher une preuve ETSI REM sur des messages importés en `.eml`.

Gmail et Microsoft 365 passent par **Identity Hub** (`https://identity.maks-it.com`). Les paramètres de compte ouvrent `/desktop-login?provider=Google` ou `Microsoft`. Le JWT Hub et le jeton de rafraîchissement restent dans `secrets.bin` ; IMAP utilise un jeton mailbox de courte durée (XOAUTH2). À la connexion, le client renouvelle la session Hub à partir de ce jeton, pour que Gmail et Outlook restent connectés après la fermeture. Si une version antérieure n’a enregistré que le JWT, reconnectez-vous une fois dans les paramètres de compte. Surcharge : `POSTCLIENT_IDENTITY_HUB`. Le mot de passe reste valable pour PEC et IMAP générique.

Mots de passe et jetons OAuth restent à côté des réglages (`DPAPI` sous Windows, mode `600` sous Linux/macOS), jamais dans `settings.json`.

## PEC italienne (posta elettronica certificata)

La PEC est une **enveloppe** du gestionnaire :

- message de transport `X-Trasporto: posta-certificata`, `daticert.xml`, original dans `postacert.eml` ;
- avis (`X-Ricevuta`) dans **Boîte de réception** et **Receipts / Ricevute** : accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, virus, préavis d’erreur.

Affichage → **Unwrap PEC/REM envelope** montre le `postacert.eml` interne. La colonne Type indique **PEC** (transport) ou **RIC** (avis).

La PEC italienne **n’a pas d’accusé de lecture**. **Avvenuta consegna** signifie un dépôt dans la boîte certifiée du destinataire, pas qu’une personne a ouvert le message.

## REM européenne (courrier électronique recommandé eIDAS)

Si le message porte une preuve ETSI REM (`REMEvidence` / `urn:etsi:rem`, ou une pièce `remevidence` / `rem-md`), le client le traite comme **REM**. Les événements alimentent la même colonne Livraison que la PEC :

- `SubmissionAcceptance` → prise en charge par le QTSP ;
- `Delivery` / avis de réception LRE ou AR24 / Zustellung De-Mail / acuse de recibo → déposé ;
- `ContentConsignment` / `Retrieval` / `Abholbestätigung` De-Mail → le destinataire a retiré le contenu (équivalent européen le plus proche de la « lecture ») ;
- rejet, virus, non-remise, `DeliveryExpiration` → échec.

S/MIME (`smime.p7s` / `smime.p7m`) est affiché comme signé numériquement ; ce n’est ni PEC ni REM.

## Colonne livraison après envoi

Après l’envoi SMTP, Postclient mémorise le **Message-Id** sortant et, à l’ouverture de **Envoyés**, rapproche les avis depuis **Boîte de réception** et **Ricevute**. L’attente ne vaut que pour les comptes PEC ou REM (profil, ou IMAP/POP3 avec ce type de boîte). Gmail, Microsoft 365 et IMAP ordinaires sont simplement marqués envoyés. Appariement, dans l’ordre : `<msgid>` de `daticert.xml`, `X-Riferimento-Message-ID`, `In-Reply-To`, `identificativo` PEC, puis les préfixes d’objet (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

| Preuve | État | Signification |
|--|--|--|
| PEC `accettazione` / `presa-in-carico`, ETSI `SubmissionAcceptance` | Accepté | Le gestionnaire a pris le message |
| PEC `avvenuta-consegna`, ETSI `Delivery`, avis LRE/AR24, Zustellung | Livré | Déposé dans la **boîte certifiée** — la PEC ne certifie pas la lecture |
| ETSI `ContentConsignment` / `Retrieval`, `Abholbestätigung` | Retiré | Le destinataire a retiré le contenu |
| `non-accettazione`, `mancata-consegna`, virus, rejet ETSI | Échec | Le gestionnaire signale un refus ou une non-remise |

Le client **affiche** la preuve de l’opérateur. Il n’émet pas d’accusé qualifié, d’enveloppe PEC ni d’horodatage.

Les notifications système signalent les nouvelles PEC, avis et REM (toast Windows, notification macOS, `notify-send` sous Linux).

## Lecture, dossiers, rédaction

- L’arbre suit le serveur (Boîte de réception, Ricevute, Brouillons, Envoyés, Archive, Indésirable, Corbeille, plus dossiers personnalisés). Les chemins IMAP imbriqués apparaissent comme des dossiers imbriqués, y compris les libellés Gmail `[Gmail]/…` sous `[Gmail]`. Brouillons, Envoyés et Corbeille nommés `INBOX.Drafts` / `INBOX.Sent` / `INBOX.Trash` restent à côté de la boîte de réception, pas en dessous. Gmail utilise `/` comme séparateur : un libellé tel que `P.IVA` reste un seul dossier. Les dossiers système ne se suppriment pas. On peut créer un dossier, le vider (vers la Corbeille, ou définitivement dans la Corbeille), tout marquer lu/non lu, supprimer un dossier personnalisé, glisser des messages sur un dossier. L’état ouvert ou fermé des comptes et des dossiers imbriqués est conservé dans `settings.json`.
- Colonnes : non lu, drapeau, pièces jointes, livraison, type (PEC / RIC / REM / SIG), de, objet, date (locale `yyyy-MM-dd HH:mm`), étiquette de dossier.
- Vue : HTML (moteur web du système), texte, source, ou **FatturaPA**. Disposition : liste au-dessus de la lecture, ou trois colonnes (dossiers, liste, lecture).
- **Grouper les conversations** indente les réponses dans la liste (Message-ID / In-Reply-To / References, profondeur max. 8). Les avis PEC du même original restent ensemble. Il n’y a pas de volet conversation séparé.
- Actions : répondre, répondre à tous, transférer, ouvrir dans une nouvelle fenêtre, lu/non lu, drapeau, priorité, supprimer.
- La rédaction est en **texte brut**. À, Cc, Cci sont des pastilles d’adresse. **Envoyer les pièces en ZIP** archive les fichiers déposés (mot de passe optionnel). L’envoi n’utilise que l’identité SMTP de la boîte choisie.

HTML : **WebView2** sous Windows, **WebKitGTK** sous Linux, **WKWebView** sous macOS. Sans moteur, le corps texte s’affiche.

Paquets Linux pour la vue HTML :

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora : `gtk3 webkit2gtk4.1 libsoup3`.

## Archive locale, recherche par mots et index de sens

« Get Messages » indexe **tout** le dossier IMAP dans SQLite (`mail.db`) plus les `.eml` sur disque. POP3 ne remplit que la boîte de réception. L’indexation tourne en arrière-plan ; la barre d’état indique l’avancement.

- **Paramètres → Indices…** : index mot-clé (FTS) et, en option, EmbeddingGemma 300M (~300 Mo) pour le sens. Le paquet ONNX se télécharge depuis Hugging Face (`onnx-community/embeddinggemma-300m-ONNX`). Les vecteurs restent dans `mail.db` sur cet ordinateur. Périphérique : Auto / CPU / GPU (DirectML sous Windows). Reconstruire ou réparer si la recherche est fausse. Les poids suivent les conditions Google Gemma.

La recherche (dossier courant) couvre objet, corps, texte des pièces (PDF et text/XML/HTML/CSV/JSON), étiquettes et le sens lorsque le modèle est prêt. Les étiquettes n’existent que sur ce PC.

L’archive est **uniquement sur cet ordinateur**. Fichier → Ouvrir le dossier d’archive / Exporter l’archive copie `mail.db` et les `.eml`. Emplacements :

| OS | Configuration | Données |
|--|--|--|
| Windows | `%AppData%\Postclient\` | `%LocalAppData%\Postclient\` |
| Linux | `~/.config/postclient/` | `~/.local/share/postclient/` |
| macOS | `~/Library/Application Support/Postclient/` | identique |

Surcharge : `POSTCLIENT_CONFIG` / `POSTCLIENT_DATA_DIR`. Le zip portable Windows avec `installType: portable` dans `settings.json` garde la config à côté de l’exécutable.

Le **QUOTA** IMAP du gestionnaire (si le serveur le publie) apparaît dans la barre d’état. C’est l’espace chez l’opérateur, pas un stockage cloud de cette application.

## Import, export, impression

| Action | Résultat |
|--|--|
| Importer EML | Originaux dans le dossier courant |
| Importer mbox | Magasins mbox locaux issus d’un profil de messagerie de bureau |
| Importer `.pst` / `.ost` | Copie le courrier du fichier de données dans le compte IMAP/POP3 **sélectionné**. Les magasins imbriqués dans le fichier sont importés aussi. Fermez tout programme qui a le fichier ouvert. |
| Attacher un fichier de données | Ouvre un `.pst` / `.ost` comme boîte. Le `.pst` Unicode est inscriptible. L’`.ost` n’est pas écrit sur place — le premier changement le copie vers un `.pst` Unicode à côté de l’original. Menu Fichier ou type de compte *fichier de données*. |
| Nouveau fichier de données | Menu Fichier : crée un `.pst` Unicode vide (Boîte de réception, Brouillons, Envoyés, Supprimés) et l’attache comme boîte. |
| Règles | **Paramètres → Règles** : **Importer des règles…** lit l’export JSON de cette appli ou un fichier `.rwz` historique. **Exporter des règles…** écrit du JSON. Chaque règle est liée à un **compte** ; le dossier cible peut vivre sur une autre boîte (y compris un fichier de données attaché). Elles s’exécutent à la récupération, à l’import et via **Exécuter toutes les règles**. |
| Imprimer | HTML lisible du message (éventuellement désenveloppé) |
| Enregistrer PDF | Le même contenu en PDF |
| ZIP des pièces | Toutes les pièces du message ouvert |
| Exporter les EML sélectionnés (fascicule) | Les `.eml` originaux — ce qu’un notaire ou une administration peut conserver comme message |

Les messages ouverts sont aussi copiés en `.eml` sous le dossier de données. Les `.eml` sont les originaux ; PDF et impression sont une copie de lecture.

## Langue de l’interface

Affichage → Langue : **English**, **Italiano**, **Français**, **Deutsch** ou **Español**. Les noms de dossiers du serveur (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) sont rattachés à Boîte de réception / Envoyés / … dans la langue de l’UI.

## Packs pays

**Paramètres → Fonctionnalités** est une matrice de **courrier certifié uniquement**. IMAP/POP3 ordinaire, recherche, étiquettes, import mbox/`.pst` et fichiers de données attachés restent disponibles. Les lignes sont des fonctions certifiées ; les colonnes sont **Italie**, **Europe**, **France**, **Allemagne**, **Espagne**, **Suisse**. La case d’en-tête allume ou éteint toute la colonne.

| Pack | Ce qu’il débloque | Défaut |
|--|--|--|
| Italie | Enveloppe PEC / avis / livraison, profils PEC, aperçu FatturaPA, fascicule | Activé |
| Europe | Preuve REM eIDAS, profil Intesi | Activé |
| France / Allemagne / Espagne / Suisse | Libellés de preuve (AR24/LRE, De-Mail, Lleida, IncaMail). Ces réseaux ne sont en général pas de l’IMAP. | Désactivé |

Désactiver un pack masque menus, colonnes et profils. Les boîtes déjà configurées restent ; seule l’interface extra disparaît.

## Téléchargements

Lorsqu’une GitHub Release existe : zip portable et installeur Windows (`postclient-{version}.exe`, autonome — pas de SDK .NET sur le PC ; chemin par défaut `C:\Program Files\MaksIT\Postclient`), Flatpak Linux, DMG macOS (`osx-arm64` et `osx-x64`). Les builds macOS ne sont pas signées : au premier lancement, **Ouvrir** depuis le menu contextuel.

### Linux (Flatpak)

```bash
flatpak install --user ./postclient-{version}.flatpak
flatpak run eu.postclient.desktop
```

Compilation, tests et publication : [README anglais](README.md).

## Licence

Apache License 2.0. Copyright 2026 Maksym Sadovnychyy (MAKS-IT).
