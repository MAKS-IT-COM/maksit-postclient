# Postclient

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-0%25-red)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-0%25-red)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-0%25-red)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Idioma:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

La interfaz de la aplicación está en inglés o italiano. Este texto describe el cliente para quienes usan **correo electrónico certificado** (PEC italiana y REM eIDAS), como particulares o como despacho / agencia.

Postclient es un cliente de correo de escritorio para Windows, Linux y macOS (Apple Silicon e Intel). Se conecta por **IMAP**, **POP3** y **SMTP** a buzones que ya tienes: correo ordinario, **PEC** italiana en un gestor cualificado, y **REM** europea cuando el operador publica IMAP/SMTP.

**No** es un prestador cualificado de servicios de confianza (QTSP), **no** es un operador PEC, **no** es una autoridad de sellado de tiempo y **no** es un buzón en la nube. Los sobres y acuses los emite el gestor / QTSP. Este programa los abre, muestra la evidencia del operador y guarda una copia en el equipo.

| | |
|--|--|
| Tipo | Cliente de correo de escritorio |
| SO | Windows, Linux, macOS |
| Stack | C#, .NET 10, Avalonia, MailKit |
| Licencia | [Apache 2.0](LICENSE.md) |
| No es | Un QTSP, una nueva dirección PEC/REM, correo alojado, una app de escáner |

Cambios: [CHANGELOG.md](CHANGELOG.md). Desarrollo: [CONTRIBUTING.md](CONTRIBUTING.md) y el [README en inglés](README.md) (compilación, pruebas, publicación).

## A quién va dirigido

### Particulares

Cliente diario para una o varias direcciones: Gmail o Microsoft 365 junto a una PEC personal (Italia) o un buzón REM IMAP. El archivo está **en este PC**: se puede buscar un aviso de comunidad, un expediente fiscal o un escrito de la administración años después, sin dejar los originales en un servidor de terceros.

Uso típico: correo certificado y ordinario en la misma ventana; ver si el gestor aceptó y depositó el mensaje; imprimir o guardar un PDF; exportar el `.eml` original si lo pide el ayuntamiento, el banco o el juzgado.

### Despachos y agencias

Sirve cuando las identidades certificadas no deben mezclarse: PEC personal, PEC del despacho y correo ordinario. **De** es un selector de buzones conectados, no una dirección escrita a mano. Un abogado, un gestor, un notario, un administrador de fincas o un despacho de la UE que presenta ante la administración puede:

- mantener cada buzón del operador por separado, con la cuota IMAP en la barra de estado;
- etiquetar mensajes **en este PC** por expediente (comunidad, cliente) — no es una tabla de despacho compartida;
- agrupar el envío con los acuses del gestor que lo referencian;
- exportar los originales seleccionados como **fascículo** de archivos `.eml` para el notario, el juzgado o la ventanilla;
- abrir adjuntos **FatturaPA** (XML de factura electrónica italiana) en una vista legible (analizador XML, no IA);
- importar un perfil Thunderbird o un **PST/OST** de Outlook sin instalar Outlook.

El programa no sustituye al gestor, no emite un acuse cualificado y no comparte las etiquetas de expediente entre los PCs del despacho.

## Cuentas y protocolos

Entrada: **IMAP** (árbol de carpetas) o **POP3** (solo bandeja de entrada). Salida: **SMTP**. Cifrado: automático, SSL/TLS, STARTTLS, STARTTLS si está disponible, o ninguno. MailKit usa los métodos SASL que anuncia el servidor.

Se pueden guardar varios buzones. Los perfiles rellenan hosts y puertos:

| Perfil | Función |
|--|--|
| Gmail | IMAP/SMTP; acceso con Google (OAuth2 / XOAUTH2) o contraseña de aplicación |
| Outlook / Microsoft 365 | IMAP/SMTP; acceso con Microsoft. La mayoría de estas cuentas rechazan la contraseña del buzón |
| IT PEC — Aruba, Legalmail, Namirial, Poste Italiane, Register.it, Libero | Hosts IMAP/SMTP del gestor italiano (nombres de host en el [README en inglés](README.md)) |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Cualquier host que indique el operador |

Los buzones IMAP certificados usan la contraseña del gestor, o una contraseña de aplicación si hay 2FA. **Lleida.net** y otras notificaciones certificadas españolas **no** suelen ser IMAP. Tampoco **AR24** (Francia), **De-Mail** (Alemania) ni **IncaMail** (Suiza). Si el operador dio hosts IMAP/POP3, elija **IMAP / POP3** e introdúzcalos. Si no, este cliente no abre ese buzón; sí puede mostrar evidencia ETSI REM en mensajes importados como `.eml`.

OAuth: ID de cliente de Google (termina en `.apps.googleusercontent.com`) y, para clientes **Web**, el **secreto de cliente** — ambos en Ajustes de cuenta (el secreto va a `secrets.bin`). Los clientes públicos de escritorio no necesitan secreto. Variables `POSTCLIENT_GOOGLE_CLIENT_ID` / `POSTCLIENT_GOOGLE_CLIENT_SECRET`. Activar la **API de Gmail**, el alcance `https://mail.google.com/`, el bucle local `http://127.0.0.1` e **IMAP** en Gmail. Microsoft: ID de aplicación de Azure (GUID); cliente público; redirección `http://localhost`; permisos IMAP/SMTP.

Las contraseñas y los tokens OAuth quedan junto a la configuración (`DPAPI` en Windows, modo `600` en Linux/macOS), nunca en `settings.json`.

## PEC italiana

La PEC es un **sobre** del gestor:

- mensaje de transporte con `X-Trasporto: posta-certificata`, `daticert.xml` y el original en `postacert.eml`;
- acuses (`X-Ricevuta`) en **Bandeja de entrada** y **Ricevute**: accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, virus, preaviso de error.

Ver → **Unwrap PEC/REM envelope** muestra el `postacert.eml` interior (el texto destinado al destinatario) en lugar del envoltorio. La columna Tipo marca **PEC** (transporte) o **RIC** (acuse).

La PEC italiana **no tiene acuse de lectura**. **Avvenuta consegna** significa depósito en el buzón certificado del destinatario, no que una persona haya abierto el mensaje.

## REM europea (eIDAS)

Si el mensaje lleva evidencia ETSI REM (`REMEvidence` / `urn:etsi:rem`, o un nombre de parte `remevidence` / `rem-md`), el cliente lo trata como **REM** (región Europa). Los eventos alimentan la misma columna de entrega que la PEC:

- `SubmissionAcceptance` → el QTSP ha aceptado;
- `Delivery` / avis de réception LRE o AR24 / Zustellung De-Mail / **acuse de recibo** → depositado;
- `ContentConsignment` / `Retrieval` / `Abholbestätigung` De-Mail → el destinatario retiró el contenido (equivalente europeo más cercano a «leído»);
- rechazo, virus, no entrega, `DeliveryExpiration` → fallido.

S/MIME (`smime.p7s` / `smime.p7m`) se muestra como firmado digitalmente; no es PEC ni REM.

## Columna de entrega tras el envío

Tras el SMTP, Postclient guarda el **Message-Id** de salida y, al abrir **Enviados**, concilia los acuses desde **Bandeja de entrada** y **Ricevute**. Empareja, en este orden: `<msgid>` de `daticert.xml`, `X-Riferimento-Message-ID`, `In-Reply-To`, `identificativo` PEC, luego prefijos de asunto (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

| Evidencia | Estado | Significado |
|--|--|--|
| PEC `accettazione` / `presa-in-carico`, ETSI `SubmissionAcceptance` | Aceptado | El gestor tomó el mensaje |
| PEC `avvenuta-consegna`, ETSI `Delivery`, avis LRE/AR24, Zustellung | Entregado | Depositado en el **buzón certificado** — la PEC no certifica la lectura |
| ETSI `ContentConsignment` / `Retrieval`, `Abholbestätigung` | Retirado | El destinatario retiró el contenido |
| `non-accettazione`, `mancata-consegna`, virus, rechazo ETSI | Fallido | El gestor informó de no aceptación o no entrega |

El cliente **muestra** la evidencia del operador. No emite acuse cualificado, sobre PEC ni sello de tiempo.

Las notificaciones del sistema avisan de PEC, acuses y REM nuevos (toast de Windows, notificación de macOS, `notify-send` en Linux).

## Lectura, carpetas, redacción

- El árbol sigue al servidor (Bandeja de entrada, Ricevute, Borradores, Enviados, Archivo, No deseado, Papelera, más carpetas propias). Las de sistema no se pueden borrar. Se puede crear una carpeta, vaciarla (a Papelera, o de forma definitiva en Papelera), marcar todo leído/no leído, eliminar una carpeta propia y arrastrar mensajes a una carpeta.
- Columnas: no leído, marca, adjuntos, entrega, tipo (PEC / RIC / REM / SIG), de, asunto, fecha (local `yyyy-MM-dd HH:mm`), etiqueta de expediente.
- Vista: HTML (motor web del sistema), texto, fuente o **FatturaPA**. Disposición: lista encima de la lectura, o tres columnas.
- **Agrupar conversaciones** sangra las respuestas en la lista (Message-ID / In-Reply-To / References, profundidad máxima 8). Los acuses PEC del mismo original quedan juntos. No hay un panel de conversación aparte.
- Acciones: responder, responder a todos, reenviar, abrir en ventana nueva, leído/no leído, marca, prioridad, eliminar.
- La redacción es **texto sin formato**. Para, Cc, Cco son chips de dirección. El envío usa solo la identidad SMTP del buzón elegido.

HTML: **WebView2** en Windows, **WebKitGTK** en Linux, **WKWebView** en macOS. Si falta el motor, se muestra el cuerpo de texto.

Paquetes Linux para la vista HTML:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora: `gtk3 webkit2gtk4.1 libsoup3`.

## Archivo, búsqueda, etiquetas

«Obtener mensajes» indexa **toda** la carpeta IMAP en SQLite (`mail.db`) más los `.eml` en disco. POP3 solo llena la bandeja de entrada. La indexación corre en segundo plano; la barra de estado muestra el avance.

La búsqueda (carpeta actual) cubre asunto, cuerpo, texto de adjuntos (PDF y text/XML/HTML/CSV/JSON) y etiquetas de expediente. Las etiquetas existen solo en este PC.

El archivo está **solo en este PC**. Archivo → Abrir carpeta de archivo / Exportar archivo copia `mail.db` y los `.eml`. Rutas:

| SO | Configuración | Datos |
|--|--|--|
| Windows | `%AppData%\Postclient\` | `%LocalAppData%\Postclient\` |
| Linux | `~/.config/postclient/` | `~/.local/share/postclient/` |
| macOS | `~/Library/Application Support/Postclient/` | la misma |

Sustitución: `POSTCLIENT_CONFIG` / `POSTCLIENT_DATA_DIR`. El zip portátil de Windows con `installType: portable` en `settings.json` deja la configuración junto al ejecutable.

La **CUOTA** IMAP del gestor (si el servidor la publica) aparece en la barra de estado. Es el espacio en el operador, no un almacenamiento en la nube de esta aplicación.

## Importar, exportar, imprimir

| Acción | Resultado |
|--|--|
| Importar EML | Originales en la carpeta actual |
| Importar Thunderbird | Almacenes mbox del perfil Thunderbird |
| Importar Outlook PST/OST | Correo sin instalar Outlook |
| Imprimir | HTML legible del mensaje (opcionalmente desempaquetado) |
| Guardar PDF | El mismo contenido en PDF |
| ZIP de adjuntos | Todos los adjuntos del mensaje abierto |
| Exportar EML seleccionados (fascículo) | Los `.eml` originales — lo que un notario o la administración pueden conservar como mensaje |

Los mensajes abiertos también se copian como `.eml` en la carpeta de datos. Los `.eml` son los originales; PDF e impresión son una copia de lectura.

## Idioma de la interfaz

Ver → Idioma: **English** o **Italiano**. Los nombres de carpeta del servidor (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) se asignan a Bandeja de entrada / Enviados / … en el idioma de la UI.

## Descargas

Cuando existe una GitHub Release: zip portátil e instalador Windows (`postclient-{version}.exe`, autónomo — sin SDK .NET en el PC), Flatpak Linux, DMG macOS (`osx-arm64` y `osx-x64`). Las compilaciones macOS no están firmadas: en el primer arranque, **Abrir** desde el menú contextual.

### Linux (Flatpak)

```bash
flatpak install --user ./postclient-{version}.flatpak
flatpak run eu.postclient.desktop
```

Compilación, pruebas y publicación: [README en inglés](README.md).

## Licencia

Apache License 2.0. Copyright 2026 Maksym Sadovnychyy (MAKS-IT).
