# Postclient

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-0%25-red)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-0%25-red)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-0%25-red)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Langue:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

L’interface de l’application est en anglais ou en italien. Ce texte décrit le client pour les utilisateurs de **courrier électronique recommandé** (PEC italienne et REM eIDAS), particuliers ou cabinets.

Postclient est un client de messagerie de bureau pour Windows, Linux et macOS (Apple Silicon et Intel). Il se connecte en **IMAP**, **POP3** et **SMTP** aux boîtes que vous avez déjà : courrier ordinaire, **PEC** italienne chez un gestionnaire qualifié, et **REM** européenne lorsque l’opérateur publie IMAP/SMTP.

Ce n’est **pas** un prestataire de services de confiance qualifié (QTSP), **pas** un opérateur PEC, **pas** une autorité d’horodatage et **pas** une boîte hébergée. Les enveloppes et accusés sont émis par le gestionnaire / QTSP. Ce programme les ouvre, affiche la preuve de l’opérateur et en conserve une copie sur la machine.

| | |
|--|--|
| Nature | Client de messagerie de bureau |
| OS | Windows, Linux, macOS |
| Stack | C#, .NET 10, Avalonia, MailKit |
| Licence | [Apache 2.0](LICENSE.md) |
| N’est pas | Un QTSP, une nouvelle adresse PEC/REM, du courrier cloud, une appli scanner |

Journal : [CHANGELOG.md](CHANGELOG.md). Développement : [CONTRIBUTING.md](CONTRIBUTING.md) et le [README anglais](README.md) (compilation, tests, publication).

## Public

### Particuliers

Client quotidien pour une ou plusieurs adresses : Gmail ou Microsoft 365 à côté d’une PEC personnelle (Italie) ou d’une boîte REM IMAP. L’archive est **sur cet ordinateur** : on peut retrouver un avis de copropriété, un dossier fiscal ou un courrier d’administration des années plus tard, sans laisser les originaux chez un tiers.

Usage typique : courrier certifié et courrier ordinaire dans la même fenêtre ; voir si le gestionnaire a accepté et déposé le message ; imprimer ou enregistrer un PDF ; exporter l’`.eml` original si la mairie, la banque ou le juge le demande.

### Cabinets et agences

Utile lorsque les identités certifiées ne doivent pas se mélanger : PEC personnelle, PEC du cabinet, courrier ordinaire. **De** est une liste des boîtes connectées, pas une adresse saisie librement. Un avocat, un expert-comptable, un notaire, un syndic ou un cabinet UE qui dépose auprès de l’administration peut :

- garder chaque boîte d’opérateur séparée, avec le quota IMAP dans la barre d’état ;
- étiqueter les messages **sur ce PC** par dossier (copropriété, client) — ce n’est pas un tableau de cabinet partagé ;
- regrouper l’envoi et les avis du gestionnaire qui s’y rapportent ;
- exporter les originaux sélectionnés en **fascicule** de fichiers `.eml` pour le notaire, le juge ou le guichet ;
- ouvrir les pièces **FatturaPA** (XML de facturation électronique italienne) en vue lisible (analyseur XML, pas d’IA) ;
- importer un profil Thunderbird ou un **PST/OST** Outlook sans installer Outlook.

Le programme ne remplace pas le gestionnaire, n’émet pas d’accusé qualifié et ne partage pas les étiquettes de dossier entre les postes du cabinet.

## Comptes et protocoles

Entrée : **IMAP** (arborescence) ou **POP3** (boîte de réception seulement). Sortie : **SMTP**. Chiffrement : auto, SSL/TLS, STARTTLS, STARTTLS si disponible, ou aucun. MailKit utilise les méthodes SASL annoncées par le serveur.

Plusieurs boîtes peuvent être enregistrées. Les profils remplissent hôtes et ports :

| Profil | Rôle |
|--|--|
| Gmail | IMAP/SMTP ; connexion Google (OAuth2 / XOAUTH2) ou mot de passe d’application |
| Outlook / Microsoft 365 | IMAP/SMTP ; connexion Microsoft. La plupart de ces comptes refusent le mot de passe de la boîte |
| IT PEC — Aruba, Legalmail, Namirial, Poste Italiane, Register.it, Libero | Hôtes IMAP/SMTP du gestionnaire italien (voir le [README anglais](README.md) pour les noms d’hôte) |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Tout hôte fourni par l’opérateur |

Les boîtes IMAP certifiées utilisent le mot de passe du gestionnaire, ou un mot de passe d’application si la 2FA est active. **AR24**, **La Poste LRE** et d’autres LRE françaises ne sont en général **pas** de l’IMAP. **De-Mail** (Allemagne), **IncaMail** (Suisse) et **Lleida** (Espagne) non plus. Si l’opérateur a fourni des hôtes IMAP/POP3, choisissez **IMAP / POP3** et saisissez-les. Sinon ce client n’ouvre pas cette boîte ; il peut encore afficher une preuve ETSI REM sur des messages importés en `.eml`.

OAuth : identifiant client Google (se termine par `.apps.googleusercontent.com`) et, pour les clients **Web**, le **secret client** — les deux dans les paramètres de compte (le secret va dans `secrets.bin`). Les clients publics Desktop n’ont pas besoin de secret. Variables `POSTCLIENT_GOOGLE_CLIENT_ID` / `POSTCLIENT_GOOGLE_CLIENT_SECRET`. Activer l’**API Gmail**, le scope `https://mail.google.com/`, la boucle locale `http://127.0.0.1` et **IMAP** dans Gmail. Microsoft : ID d’application Azure (GUID) ; client public ; redirection `http://localhost` ; droits IMAP/SMTP.

Mots de passe et jetons OAuth restent à côté des réglages (`DPAPI` sous Windows, mode `600` sous Linux/macOS), jamais dans `settings.json`.

## PEC italienne

La PEC est une **enveloppe** du gestionnaire :

- message de transport `X-Trasporto: posta-certificata`, `daticert.xml`, original dans `postacert.eml` ;
- avis (`X-Ricevuta`) dans **Boîte de réception** et **Receipts / Ricevute** : accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, virus, préavis d’erreur.

Affichage → **Unwrap PEC/REM envelope** montre le `postacert.eml` interne (le texte destiné au destinataire) plutôt que l’enveloppe. La colonne Type indique **PEC** (transport) ou **RIC** (avis).

La PEC italienne **n’a pas d’accusé de lecture**. **Avvenuta consegna** signifie un dépôt dans la boîte certifiée du destinataire, pas qu’une personne a ouvert le message.

## REM européenne (eIDAS)

Si le message porte une preuve ETSI REM (`REMEvidence` / `urn:etsi:rem`, ou une pièce `remevidence` / `rem-md`), le client le traite comme **REM** (région Europe). Les événements alimentent la même colonne Livraison que la PEC :

- `SubmissionAcceptance` → prise en charge par le QTSP ;
- `Delivery` / avis de réception LRE ou AR24 / Zustellung De-Mail / acuse de recibo → déposé ;
- `ContentConsignment` / `Retrieval` / `Abholbestätigung` De-Mail → le destinataire a retiré le contenu (équivalent européen le plus proche de la « lecture ») ;
- rejet, virus, non-remise, `DeliveryExpiration` → échec.

S/MIME (`smime.p7s` / `smime.p7m`) est affiché comme signé numériquement ; ce n’est ni PEC ni REM.

## Colonne livraison après envoi

Après l’envoi SMTP, Postclient mémorise le **Message-Id** sortant et, à l’ouverture de **Envoyés**, rapproche les avis depuis **Boîte de réception** et **Ricevute**. Appariement, dans l’ordre : `<msgid>` de `daticert.xml`, `X-Riferimento-Message-ID`, `In-Reply-To`, `identificativo` PEC, puis les préfixes d’objet (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACCUSE DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

| Preuve | État | Signification |
|--|--|--|
| PEC `accettazione` / `presa-in-carico`, ETSI `SubmissionAcceptance` | Accepté | Le gestionnaire a pris le message |
| PEC `avvenuta-consegna`, ETSI `Delivery`, avis LRE/AR24, Zustellung | Livré | Déposé dans la **boîte certifiée** — la PEC ne certifie pas la lecture |
| ETSI `ContentConsignment` / `Retrieval`, `Abholbestätigung` | Retiré | Le destinataire a retiré le contenu |
| `non-accettazione`, `mancata-consegna`, virus, rejet ETSI | Échec | Le gestionnaire signale un refus ou une non-remise |

Le client **affiche** la preuve de l’opérateur. Il n’émet pas d’accusé qualifié, d’enveloppe PEC ni d’horodatage.

Les notifications système signalent les nouvelles PEC, avis et REM (toast Windows, notification macOS, `notify-send` sous Linux).

## Lecture, dossiers, rédaction

- L’arbre suit le serveur (Boîte de réception, Ricevute, Brouillons, Envoyés, Archive, Indésirable, Corbeille, plus dossiers personnalisés). Les dossiers système ne se suppriment pas. On peut créer un dossier, le vider (vers la Corbeille, ou définitivement dans la Corbeille), tout marquer lu/non lu, supprimer un dossier personnalisé, glisser des messages sur un dossier.
- Colonnes : non lu, drapeau, pièces jointes, livraison, type (PEC / RIC / REM / SIG), de, objet, date (locale `yyyy-MM-dd HH:mm`), étiquette de dossier.
- Vue : HTML (moteur web du système), texte, source, ou **FatturaPA**. Disposition : liste au-dessus de la lecture, ou trois colonnes.
- **Grouper les conversations** indente les réponses dans la liste (Message-ID / In-Reply-To / References, profondeur max. 8). Les avis PEC du même original restent ensemble. Il n’y a pas de volet conversation séparé.
- Actions : répondre, répondre à tous, transférer, ouvrir dans une nouvelle fenêtre, lu/non lu, drapeau, priorité, supprimer.
- La rédaction est en **texte brut**. À, Cc, Cci sont des pastilles d’adresse. L’envoi n’utilise que l’identité SMTP de la boîte choisie.

HTML : **WebView2** sous Windows, **WebKitGTK** sous Linux, **WKWebView** sous macOS. Sans moteur, le corps texte s’affiche.

Paquets Linux pour la vue HTML :

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora : `gtk3 webkit2gtk4.1 libsoup3`.

## Archive, recherche, étiquettes

« Get Messages » indexe **tout** le dossier IMAP dans SQLite (`mail.db`) plus les `.eml` sur disque. POP3 ne remplit que la boîte de réception. L’indexation tourne en arrière-plan ; la barre d’état indique l’avancement.

La recherche (dossier courant) couvre objet, corps, texte des pièces (PDF et text/XML/HTML/CSV/JSON) et étiquettes. Les étiquettes n’existent que sur ce PC.

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
| Importer Thunderbird | Stores mbox du profil Thunderbird |
| Importer Outlook PST/OST | Courrier sans installer Outlook |
| Imprimer | HTML lisible du message (éventuellement désenveloppé) |
| Enregistrer PDF | Le même contenu en PDF |
| ZIP des pièces | Toutes les pièces du message ouvert |
| Exporter les EML sélectionnés (fascicule) | Les `.eml` originaux — ce qu’un notaire ou une administration peut conserver comme message |

Les messages ouverts sont aussi copiés en `.eml` sous le dossier de données. Les `.eml` sont les originaux ; PDF et impression sont une copie de lecture.

## Langue de l’interface

Affichage → Langue : **English** ou **Italiano**. Les noms de dossiers du serveur (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) sont rattachés à Boîte de réception / Envoyés / … dans la langue de l’UI.

## Téléchargements

Lorsqu’une GitHub Release existe : zip portable et installeur Windows (`postclient-{version}.exe`, autonome — pas de SDK .NET sur le PC), Flatpak Linux, DMG macOS (`osx-arm64` et `osx-x64`). Les builds macOS ne sont pas signées : au premier lancement, **Ouvrir** depuis le menu contextuel.

### Linux (Flatpak)

```bash
flatpak install --user ./postclient-{version}.flatpak
flatpak run eu.postclient.desktop
```

Compilation, tests et publication : [README anglais](README.md).

## Licence

Apache License 2.0. Copyright 2026 Maksym Sadovnychyy (MAKS-IT).
