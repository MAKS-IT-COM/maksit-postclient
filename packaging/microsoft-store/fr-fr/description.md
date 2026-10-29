# Microsoft Store description (fr-fr)

Paste into Partner Center → Store listings → **French (France)**. Plain text only. URLs in these fields are not clickable; put the privacy policy, support site, and license in their own submission fields.

Leave **What's new in this version** empty on the first submission. Use it only for a later update (1,500 characters).

The other languages are listed in [../description.md](../description.md). Hardware checkboxes stay in [CONTRIBUTING.md](../../../CONTRIBUTING.md) under **System requirements (Properties)**.

## Short description

Shown at the top of the listing. The field allows 1,000 characters. Keep this under 270 so every Store view shows the full sentence.

```text
Client de messagerie pour les boîtes que vous avez déjà. Il lit et envoie en IMAP, POP3 et SMTP, y compris la PEC italienne et le REM européen. Les avis restent sur ce PC. La recherche par le sens, si vous l’activez, reste sur l’appareil et n’envoie pas le courrier.
```

## Description

Required. Up to 10,000 characters. This text is different from the short description so the page does not repeat the same paragraph.

```text
Gardez le courrier ordinaire et le courrier certifié dans la même fenêtre sous Windows. Postclient se connecte au prestataire que vous utilisez déjà. Il ne délivre pas d’adresse PEC, d’accusé qualifié ni d’horodatage.

Connectez-vous en IMAP, POP3 ou SMTP. Les profils remplissent les hôtes et les ports pour Gmail, Microsoft 365, PEC Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero et Intesi Group. Tout autre hôte est un compte personnalisé. Réglez le type de boîte sur PEC italienne ou REM européenne lorsqu’un domaine propre est certifié. Un magasin de dossiers est un répertoire sur ce PC : pas de mot de passe et pas de SMTP. Plusieurs boîtes restent ouvertes.

L’arborescence suit le serveur, y compris les dossiers imbriqués et les libellés Gmail. Les dossiers standard que le serveur range sous INBOX sont à côté de la boîte de réception. Vous pouvez créer un dossier, le vider, tout marquer lu ou non lu, et déposer des messages sur un dossier. La liste montre non lu, drapeau, pièces jointes, livraison, type, de, objet, date, taille et une étiquette de dossier. Ouvrez le HTML, le texte brut, la source ou une facture électronique italienne. Grouper les conversations indente les réponses. Répondre, répondre à tous, transférer, marquer, marquer comme spam et supprimer. Supprimer envoie le courrier à la corbeille. Supprimer depuis la corbeille est définitif. La rédaction est en texte brut. À, Cc et Cci sont des adresses. L’envoi utilise seulement la boîte sélectionnée. De est cette boîte. On ne saisit pas un expéditeur libre.

Le courrier certifié est montré comme une preuve, pas comme une pièce jointe quelconque. Ouvrez le message de transport PEC et lisez le postacert.eml interne. La colonne Type marque PEC, un accusé, REM ou une signature numérique. La colonne Livraison lit l’acceptation, la remise, les événements REM ETSI et les libellés d’avis français, allemands et espagnols lorsque ces packs sont activés. La PEC italienne n’a pas d’accusé de lecture : la remise signifie que le message a été déposé dans la boîte certifiée. Après l’envoi, Envoyés est rapproché des avis dans la boîte de réception et Ricevute par Message-Id, daticert.xml et le préfixe de l’objet. Ce programme montre cette preuve. Il n’émet pas d’accusé qualifié, d’enveloppe PEC ni d’horodatage.

Récupérer les messages enregistre tout le dossier IMAP sur ce PC, sous forme de mail.db plus les fichiers .eml d’origine. La recherche couvre l’objet, le corps, le texte extrait des pièces courantes et les étiquettes. La recherche par le sens est facultative. Un modèle est téléchargé une fois sur ce PC, les vecteurs restent dans mail.db et le courrier n’est pas envoyé à un service d’IA dans le cloud. Les règles s’exécutent à la synchro et à l’import. Le dossier cible peut vivre sur un autre compte. Importez un fichier de règles ou exportez du JSON.

Les actions sur fichiers couvrent l’import EML et mbox, un PST Outlook copié dans un magasin de dossiers, la rétention par dossier, un bundle de migration verrouillé par une passphrase et l’export des fichiers .eml d’origine. Marquer comme spam retient ce message sur cet ordinateur et peut signaler un courrier semblable dans chaque boîte. Les packs pays masquent les extras de courrier certifié que vous n’utilisez pas. L’IMAP ordinaire reste disponible. L’interface est en anglais, italien, français, allemand ou espagnol.

Les mots de passe et les jetons de connexion restent à côté des réglages, pas dans settings.json. Gmail et Microsoft 365 passent par Identity Hub. La disposition et les comptes sont enregistrés sur cet ordinateur.

Il vous faut Windows 10 ou Windows 11 64 bits et une boîte que vous avez déjà, ou un magasin de dossiers. Installez WebView2 si vous voulez le courrier HTML. Sinon le texte est affiché. Le modèle de recherche par le sens est un téléchargement à part, seulement si vous activez cet index.
```

Product features (up to 20 bullets) are in [product-features.md](product-features.md). Keywords are in [keywords.md](keywords.md). Copyright, additional license terms, and Developed by are in [additional-info.md](additional-info.md).

## Additional system requirements

Optional text lines, separate from the Properties hardware table. Up to 11 minimum and 11 recommended. Each line is at most 200 characters.

Minimum:

```text
Windows 10 ou Windows 11 64 bits
Une boîte IMAP, POP3 ou SMTP que vous avez déjà, ou un dossier local de messages
Accès réseau à ce serveur lorsque le compte n’est pas un magasin de dossiers
```

Recommended:

```text
WebView2, pour afficher le HTML en page. Sinon le corps texte est montré
De l’espace disque pour l’archive locale. Le modèle de recherche par le sens, s’il est activé, pèse environ 300 Mo
```

Category is set once on the product, not per language: Productivity.
