# Microsoft Store restricted capabilities

Paste into Partner Center → Submission options. This product declares only `runFullTrust`.

The field holds 500 characters. [App capability declarations](https://learn.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations#special-and-restricted-capabilities) says a package with a full-trust app must declare `runFullTrust` or it cannot be installed. A full-trust app uses entry point `Windows.FullTrustApplication` and runs at medium integrity, outside an AppContainer.

## runFullTrust

Why do you need the `runFullTrust` capability, and how will it be used in your product?

```text
Postclient is a full-trust Win32 app (.NET, Avalonia) packaged as MSIX. Windows.FullTrustApplication runs at medium integrity as the signed-in user, outside an AppContainer. runFullTrust is required for this package to install.

Used for mail settings and the archive under %AppData% and %LocalAppData%, passwords in the user secret store, files the user opens or saves, and IMAP, POP3, and SMTP. No administrator rights or device capabilities.
```

This justification is once per submission, not once per listing language. Store listing text for each UI language is indexed in [description.md](description.md).
