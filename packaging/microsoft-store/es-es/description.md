# Microsoft Store description (es-es)

Paste into Partner Center → Store listings → **Spanish (Spain)**. Plain text only. URLs in these fields are not clickable; put the privacy policy, support site, and license in their own submission fields.

Leave **What's new in this version** empty on the first submission. Use it only for a later update (1,500 characters).

The other languages are listed in [../description.md](../description.md). Hardware checkboxes stay in [CONTRIBUTING.md](../../../CONTRIBUTING.md) under **System requirements (Properties)**.

## Short description

Shown at the top of the listing. The field allows 1,000 characters. Keep this under 270 so every Store view shows the full sentence.

```text
Cliente de correo para buzones que ya tiene. Lee y envía con IMAP, POP3 y SMTP, incluida la PEC italiana y la REM europea. Los acuses quedan en este PC. La búsqueda por significado, si la activa, se queda en el equipo y no sube el correo.
```

## Description

Required. Up to 10,000 characters. This text is different from the short description so the page does not repeat the same paragraph.

```text
Tenga el correo ordinario y el correo certificado en la misma ventana de Windows. Postclient se conecta al operador que ya usa. No emite una dirección PEC, un acuse cualificado ni un sello de tiempo.

Conéctese con IMAP, POP3 o SMTP. Los perfiles rellenan hosts y puertos para Gmail, Microsoft 365, PEC Aruba, InfoCert Legalmail, Namirial, Poste Italiane, Register.it, Libero e Intesi Group. Cualquier otro host es una cuenta personalizada. Ponga el tipo de buzón en PEC italiana o REM europea cuando un dominio propio esté certificado. Un almacén de carpetas es un directorio en este PC: sin contraseña y sin SMTP. Varios buzones siguen abiertos.

El árbol de carpetas sigue al servidor, incluidas las carpetas anidadas y las etiquetas de Gmail. Las carpetas estándar que el servidor guarda bajo INBOX quedan junto a la bandeja de entrada. Puede crear una carpeta, vaciarla, marcar todo como leído o no leído y arrastrar mensajes a una carpeta. La lista muestra no leído, marca, adjuntos, entrega, tipo, de, asunto, fecha, tamaño y una etiqueta de expediente. Abra HTML, texto plano, el origen o una factura electrónica italiana. Agrupar conversaciones sangra las respuestas. Responder, responder a todos, reenviar, marcar, marcar como spam y eliminar. Eliminar mueve el correo a la papelera. Eliminar desde la papelera es definitivo. Redactar es texto plano. Para, Cc y Cco son direcciones. El envío usa solo el buzón seleccionado. De es ese buzón. No se escribe un remitente libre.

El correo certificado se muestra como evidencia, no como un adjunto cualquiera. Abra el mensaje de transporte PEC y lea el postacert.eml interno. La columna Tipo marca PEC, un acuse, REM o una firma digital. La columna Entrega lee aceptación, entrega efectuada, eventos REM de ETSI y las etiquetas de acuse francesas, alemanas y españolas cuando esos paquetes están activos. La PEC italiana no tiene acuse de lectura: la entrega efectuada significa que el mensaje se depositó en el buzón certificado. Después de enviar, Enviados se casa con los acuses de la bandeja de entrada y Ricevute por Message-Id, daticert.xml y el prefijo del asunto. Este programa muestra esa evidencia. No emite un acuse cualificado, un sobre PEC ni un sello de tiempo.

Obtener mensajes guarda toda la carpeta IMAP en este PC, como mail.db más los archivos .eml originales. La búsqueda cubre asunto, cuerpo, el texto extraído de adjuntos habituales y las etiquetas de expediente. La búsqueda por significado es opcional. Un modelo se descarga una vez en este PC, los vectores quedan en mail.db y el correo no se envía a un servicio de IA en la nube. Las reglas se ejecutan al sincronizar y al importar. La carpeta de destino puede estar en otra cuenta. Importe un archivo de reglas o exporte JSON.

Las acciones de archivo cubren la importación de EML y mbox, un PST de Outlook copiado a un almacén de carpetas, la retención por carpeta, un paquete de migración cerrado con contraseña y la exportación de los .eml originales. Marcar como spam recuerda ese mensaje en este equipo y puede indicar correo parecido en cada buzón. Los paquetes de país ocultan los extras de correo certificado que no usa. El IMAP ordinario sigue disponible. La interfaz está en inglés, italiano, francés, alemán o español.

Las contraseñas y los tokens de acceso quedan junto a los ajustes, no dentro de settings.json. Gmail y Microsoft 365 usan Identity Hub. La disposición y las cuentas se guardan en este equipo.

Necesita Windows 10 o Windows 11 de 64 bits y un buzón que ya tenga, o un almacén de carpetas. Instale WebView2 si quiere correo HTML. Si no, se muestra el texto. El modelo de búsqueda por significado es una descarga aparte, solo si activa ese índice.
```

Product features (up to 20 bullets) are in [product-features.md](product-features.md). Keywords are in [keywords.md](keywords.md). Copyright, additional license terms, and Developed by are in [additional-info.md](additional-info.md).

## Additional system requirements

Optional text lines, separate from the Properties hardware table. Up to 11 minimum and 11 recommended. Each line is at most 200 characters.

Minimum:

```text
Windows 10 o Windows 11 de 64 bits
Un buzón IMAP, POP3 o SMTP que ya tenga, o una carpeta local de mensajes
Acceso de red a ese servidor cuando la cuenta no es un almacén de carpetas
```

Recommended:

```text
WebView2, para que el correo HTML use la vista de página. Si falta, se muestra el texto
Espacio en disco para el archivo local. El modelo de búsqueda por significado, si lo activa, ocupa unos 300 MB
```

Category is set once on the product, not per language: Productivity.
