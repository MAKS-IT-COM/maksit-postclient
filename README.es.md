# Postclient — cliente de escritorio PEC, REM e IMAP

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-49.7%25-yellowgreen)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-47%25-yellowgreen)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-52.6%25-yellowgreen)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/License-Apache%202.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6)

**Idioma:** [English](README.md) · [Italiano](README.it.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md)

La interfaz de la aplicación está en inglés, italiano, francés, alemán o español. Estos documentos describen el mismo **cliente de correo de escritorio** de código abierto para quien envía y conserva **posta elettronica certificata (PEC)**, **correo electrónico certificado eIDAS (REM)** y correo **IMAP / POP3 / SMTP** ordinario en Windows, Linux y macOS.

Postclient es un **cliente local**, no un webmail. Habla IMAP, POP3 y SMTP con cuentas que ya tienes: Gmail, Microsoft 365, **PEC** italiana en un gestor cualificado (Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero, e IMAP con dominio propio), y **REM** europea cuando el operador publica IMAP/SMTP. Los acuses quedan en este PC como `.eml` más un archivo SQLite consultable.

**No** es un prestador cualificado de servicios de confianza (QTSP), **no** es un operador PEC, **no** es una autoridad de sellado de tiempo y **no** es un buzón en la nube. Los sobres y acuses los emite el gestor / QTSP. Este programa los abre, muestra la evidencia de entrega y guarda los originales en el equipo.

| | |
|--|--|
| Tipo | Cliente de correo de escritorio para PEC, REM e IMAP |
| SO | Windows, Linux, macOS (Apple Silicon e Intel) |
| Stack | C#, .NET 10, Avalonia, MailKit |
| Licencia | [Apache 2.0](LICENSE.md) |
| No es | Un QTSP, una nueva dirección PEC/REM, correo alojado, una app de escáner |

Cambios: [CHANGELOG.md](CHANGELOG.md). Desarrollo: [CONTRIBUTING.md](CONTRIBUTING.md) y el [README en inglés](README.md) (compilación, pruebas, publicación).

## Hecho para el correo certificado en el escritorio

El correo de escritorio y el webmail tratan de normalmente un **sobre** PEC o una evidencia ETSI REM como un adjunto cualquiera. Postclient está escrito alrededor de esa evidencia:

- **Abrir sobres PEC / REM** — leer el `postacert.eml` interior (el mensaje destinado al destinatario) en lugar del envoltorio del gestor; columna Tipo **PEC**, **RIC**, **REM** o **SIG** (firmado digitalmente).
- **Columna de entrega desde la evidencia del operador** — accettazione, avvenuta-consegna, ETSI `SubmissionAcceptance` / `Delivery` / `Retrieval`, avis LRE y AR24, Zustellung y Abholbestätigung De-Mail, acuse de recibo. Tras SMTP, **Enviados** se concilia con acuses de Bandeja de entrada/Ricevute por Message-Id, `daticert.xml` y prefijos de asunto. La PEC italiana **no tiene acuse de lectura**: avvenuta consegna es el depósito en el buzón certificado.
- **Vista previa FatturaPA** — XML de factura electrónica italiana en una vista legible (analizador XML, no IA generativa).
- **Fascículo de `.eml` originales** — exportar los mensajes seleccionados como los archivos que un notario, una ventanilla, un banco o un juzgado pueden conservar.
- **De es un selector de buzón** — no se escribe un remitente libre; el envío usa la identidad SMTP de la cuenta conectada.
- **Etiquetas de expediente en este PC** — comunidad, expediente de cliente, aviso fiscal. Las etiquetas son locales, no una base de despacho en red.
- **Archivo local de toda la carpeta** — Obtener mensajes guarda **toda la carpeta IMAP** en `mail.db` más `.eml` en disco (no una ventana de los últimos N). FTS de asunto, cuerpo, texto de adjuntos PDF/XML/HTML/CSV/JSON y etiquetas.
- **Búsqueda por significado en el dispositivo (RAG)** — EmbeddingGemma 300M opcional en **Ajustes → Índices…**. Los vectores quedan en `mail.db`. El correo no sale de la máquina. Reconstruir o reparar índices de palabras y de significado.
- **Reglas entre buzones** — mover, eliminar, marcar, etiquetar; la carpeta de destino puede estar en otra cuenta, incluido un archivo de datos `.pst` / `.ost` adjunto. Importar JSON o un archivo `.rwz` heredado; la exportación es JSON.
- **Abrir `.pst` / `.ost` como buzón** — el almacén Unicode es escribible (carpetas, marcas, mover, eliminar). Un almacén sin conexión se copia a un `.pst` Unicode en la primera escritura. No hace falta otro programa de correo para leer el archivo.
- **Paquetes por país** — **Ajustes → Funciones** activa o desactiva Italia (PEC, FatturaPA, fascículo), Europa (REM eIDAS), Francia, Alemania, España y Suiza. El IMAP ordinario sigue disponible.

## A quién va dirigido

### Particulares

Cliente diario para una o varias direcciones: Gmail o Microsoft 365 junto a una **PEC** personal (Italia) o un buzón **REM** IMAP (UE). Buscar un aviso de comunidad, un expediente fiscal o un escrito de la administración años después, sin dejar los originales en un servidor de terceros.

Uso típico: correo certificado y ordinario en la misma ventana; ver si el gestor aceptó y depositó el mensaje; imprimir o guardar un PDF; exportar el `.eml` original si lo pide el ayuntamiento, el banco o el juzgado.

### Despachos y agencias

Sirve cuando las identidades certificadas no deben mezclarse: PEC personal, PEC del despacho y correo ordinario. Un abogado, un gestor, un notario, un administrador de fincas o un despacho de la UE que presenta ante la administración puede mantener cada buzón del operador por separado (**CUOTA** IMAP en la barra de estado), agrupar un envío con sus acuses y exportar un fascículo sin cargar el expediente en un archivo de terceros.

El programa no sustituye al gestor, no emite un acuse cualificado y no comparte las etiquetas entre los PCs del despacho.

## Cuentas y protocolos

Entrada: **IMAP** (árbol de carpetas), **POP3** (solo bandeja de entrada) o un **`.pst` / `.ost`** adjunto. El `.pst` Unicode es escribible. Salida: **SMTP**. Cifrado: automático, SSL/TLS, STARTTLS, STARTTLS si está disponible, o ninguno. MailKit usa los métodos SASL que anuncia el servidor.

Se pueden guardar varios buzones. Los perfiles rellenan hosts y puertos:

| Perfil | Función |
|--|--|
| Gmail | IMAP/SMTP; acceso con Google (OAuth2 / XOAUTH2) o contraseña de aplicación de Gmail |
| Microsoft 365 | IMAP/SMTP; acceso con Microsoft. La mayoría de estas cuentas rechazan la contraseña del buzón |
| IT PEC — Aruba, Legalmail, Namirial, Poste Italiane, Register.it, Libero | Hosts IMAP/SMTP del gestor italiano (nombres de host en el [README en inglés](README.md)) |
| EU — Intesi Group | `imap.ig-trustmail.com` / `smtp.ig-trustmail.com` |
| IMAP / POP3 | Cualquier host que indique el operador. Ponga el **tipo de buzón** en PEC italiana o REM UE si esa cuenta es certificada (también con dominio propio) |
| Archivo de datos (`.pst` / `.ost`) | Almacén local abierto como buzón. El `.pst` Unicode es escribible; `.ost` / `.pst` ANSI se copia a un `.pst` Unicode en la primera escritura. Sin contraseña. |

Los buzones IMAP certificados usan la contraseña del gestor, o una contraseña de aplicación si hay 2FA. **Lleida.net** y otras notificaciones certificadas españolas **no** suelen ser IMAP. Tampoco **AR24** (Francia), **De-Mail** (Alemania) ni **IncaMail** (Suiza). Si el operador dio hosts IMAP/POP3, elija **IMAP / POP3**. Si no, este cliente no abre ese buzón; sí puede mostrar evidencia ETSI REM en mensajes importados como `.eml`.

OAuth: ID de cliente de Google (termina en `.apps.googleusercontent.com`) y, para clientes **Web**, el **secreto de cliente** — ambos en Ajustes de cuenta (el secreto va a `secrets.bin`). Los clientes públicos de escritorio no necesitan secreto. Variables `POSTCLIENT_GOOGLE_CLIENT_ID` / `POSTCLIENT_GOOGLE_CLIENT_SECRET`. Activar la **API de Gmail**, el alcance `https://mail.google.com/`, el bucle local `http://127.0.0.1` e **IMAP** en Gmail. Microsoft: ID de aplicación de Azure (GUID); cliente público; redirección `http://localhost`; permisos IMAP/SMTP.

Las contraseñas y los tokens OAuth quedan junto a la configuración (`DPAPI` en Windows, modo `600` en Linux/macOS), nunca en `settings.json`.

## PEC italiana (posta elettronica certificata)

La PEC es un **sobre** del gestor:

- mensaje de transporte con `X-Trasporto: posta-certificata`, `daticert.xml` y el original en `postacert.eml`;
- acuses (`X-Ricevuta`) en **Bandeja de entrada** y **Ricevute**: accettazione, presa-in-carico, avvenuta-consegna, non-accettazione, mancata-consegna, virus, preaviso de error.

Ver → **Unwrap PEC/REM envelope** muestra el `postacert.eml` interior. La columna Tipo marca **PEC** (transporte) o **RIC** (acuse).

La PEC italiana **no tiene acuse de lectura**. **Avvenuta consegna** significa depósito en el buzón certificado del destinatario, no que una persona haya abierto el mensaje.

## REM europea (correo electrónico certificado eIDAS)

Si el mensaje lleva evidencia ETSI REM (`REMEvidence` / `urn:etsi:rem`, o un nombre de parte `remevidence` / `rem-md`), el cliente lo trata como **REM**. Los eventos alimentan la misma columna de entrega que la PEC:

- `SubmissionAcceptance` → el QTSP ha aceptado;
- `Delivery` / avis de réception LRE o AR24 / Zustellung De-Mail / **acuse de recibo** → depositado;
- `ContentConsignment` / `Retrieval` / `Abholbestätigung` De-Mail → el destinatario retiró el contenido (equivalente europeo más cercano a «leído»);
- rechazo, virus, no entrega, `DeliveryExpiration` → fallido.

S/MIME (`smime.p7s` / `smime.p7m`) se muestra como firmado digitalmente; no es PEC ni REM.

## Columna de entrega tras el envío

Tras el SMTP, Postclient guarda el **Message-Id** de salida y, al abrir **Enviados**, concilia los acuses desde **Bandeja de entrada** y **Ricevute**. La espera solo aplica a cuentas PEC o REM (perfil, o IMAP/POP3 con ese tipo de buzón). Gmail, Microsoft 365 e IMAP ordinario se marcan solo como enviados. Empareja, en este orden: `<msgid>` de `daticert.xml`, `X-Riferimento-Message-ID`, `In-Reply-To`, `identificativo` PEC, luego prefijos de asunto (`ACCETTAZIONE:`, `CONSEGNA:`, `AVIS DE RECEPTION:`, `ACUSE DE RECIBO:`, `ZUSTELLBESTAETIGUNG:`, `ABHOLBESTAETIGUNG:`, …).

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
- Vista: HTML (motor web del sistema), texto, fuente o **FatturaPA**. Disposición: lista encima de la lectura, o tres columnas (carpetas, lista, lectura).
- **Agrupar conversaciones** sangra las respuestas en la lista (Message-ID / In-Reply-To / References, profundidad máxima 8). Los acuses PEC del mismo original quedan juntos. No hay un panel de conversación aparte.
- Acciones: responder, responder a todos, reenviar, abrir en ventana nueva, leído/no leído, marca, prioridad, eliminar.
- La redacción es **texto sin formato**. Para, Cc, Cco son chips de dirección. **Enviar adjuntos como ZIP** empaqueta los archivos soltados (contraseña opcional). El envío usa solo la identidad SMTP del buzón elegido.

HTML: **WebView2** en Windows, **WebKitGTK** en Linux, **WKWebView** en macOS. Si falta el motor, se muestra el cuerpo de texto.

Paquetes Linux para la vista HTML:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Fedora: `gtk3 webkit2gtk4.1 libsoup3`.

## Archivo local, búsqueda por palabras e índice de significado

«Obtener mensajes» indexa **toda** la carpeta IMAP en SQLite (`mail.db`) más los `.eml` en disco. POP3 solo llena la bandeja de entrada. La indexación corre en segundo plano; la barra de estado muestra el avance.

- **Ajustes → Índices…**: índice de palabras (FTS) y, si se desea, EmbeddingGemma 300M (~300 MB) para el significado. El paquete ONNX se descarga de Hugging Face (`onnx-community/embeddinggemma-300m-ONNX`). Los vectores quedan en `mail.db` en este PC. Dispositivo: Auto / CPU / GPU (DirectML en Windows). Reconstruir o reparar si la búsqueda falla. Los pesos siguen los términos Google Gemma.

La búsqueda (carpeta actual) cubre asunto, cuerpo, texto de adjuntos (PDF y text/XML/HTML/CSV/JSON), etiquetas de expediente y el significado cuando el modelo está listo. Las etiquetas existen solo en este PC.

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
| Importar mbox | Almacenes mbox locales de un perfil de correo de escritorio |
| Importar `.pst` / `.ost` | Copia el correo del archivo de datos a la cuenta IMAP/POP3 **seleccionada**. Los almacenes anidados en el archivo también se importan. Cierre cualquier programa que tenga el archivo abierto. |
| Adjuntar archivo de datos | Abre un `.pst` / `.ost` como buzón. El `.pst` Unicode es escribible. El `.ost` no se escribe in situ: el primer cambio lo copia a un `.pst` Unicode junto al original. Menú Archivo o tipo de cuenta *archivo de datos*. |
| Nuevo archivo de datos | Menú Archivo: crea un `.pst` Unicode vacío (Bandeja de entrada, Borradores, Enviados, Eliminados) y lo adjunta como buzón. |
| Reglas | **Ajustes → Reglas**: **Importar reglas…** lee la exportación JSON de esta app o un archivo `.rwz` heredado. **Exportar reglas…** escribe JSON. Cada regla está ligada a una **cuenta**; la carpeta de destino puede estar en otro buzón (incluido un archivo de datos adjunto). Se ejecutan al obtener mensajes, al importar y desde **Ejecutar todas las reglas**. |
| Imprimir | HTML legible del mensaje (opcionalmente desempaquetado) |
| Guardar PDF | El mismo contenido en PDF |
| ZIP de adjuntos | Todos los adjuntos del mensaje abierto |
| Exportar EML seleccionados (fascículo) | Los `.eml` originales — lo que un notario o la administración pueden conservar como mensaje |

Los mensajes abiertos también se copian como `.eml` en la carpeta de datos. Los `.eml` son los originales; PDF e impresión son una copia de lectura.

## Idioma de la interfaz

Ver → Idioma: **English**, **Italiano**, **Français**, **Deutsch** o **Español**. Los nombres de carpeta del servidor (Posta in arrivo, Gesendet, Messages envoyés, Enviados, …) se asignan a Bandeja de entrada / Enviados / … en el idioma de la UI.

## Paquetes por país

**Ajustes → Funciones** es una matriz de **solo correo certificado**. IMAP/POP3 ordinario, búsqueda, etiquetas de expediente, importación mbox/`.pst` y archivos de datos adjuntos siguen disponibles. Las filas son funciones certificadas; las columnas son **Italia**, **Europa**, **Francia**, **Alemania**, **España**, **Suiza**. La casilla de cabecera enciende o apaga toda la columna.

| Paquete | Qué desbloquea | Predeterminado |
|--|--|--|
| Italia | Sobre PEC / acuses / entrega, perfiles PEC, vista previa FatturaPA, fascículo | Activado |
| Europa | Evidencia REM eIDAS, perfil Intesi | Activado |
| Francia / Alemania / España / Suiza | Etiquetas de evidencia (AR24/LRE, De-Mail, Lleida, IncaMail). Esas redes no suelen ser IMAP. | Desactivado |

Apagar un paquete oculta menús, columnas y perfiles. Los buzones ya configurados se quedan; solo desaparece la interfaz extra.

## Descargas

Cuando existe una GitHub Release: zip portátil e instalador Windows (`postclient-{version}.exe`, autónomo — sin SDK .NET en el PC; ruta predeterminada `C:\Program Files\MaksIT\Postclient`), Flatpak Linux, DMG macOS (`osx-arm64` y `osx-x64`). Las compilaciones macOS no están firmadas: en el primer arranque, **Abrir** desde el menú contextual.

### Linux (Flatpak)

```bash
flatpak install --user ./postclient-{version}.flatpak
flatpak run eu.postclient.desktop
```

Compilación, pruebas y publicación: [README en inglés](README.md).

## Licencia

Apache License 2.0. Copyright 2026 Maksym Sadovnychyy (MAKS-IT).
