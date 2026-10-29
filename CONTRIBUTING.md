# Contributing to Postclient

C# style: repo-root [`.editorconfig`](.editorconfig). Repo hygiene: (Community desktop: RepoUtils under `utils/`).

## Development setup

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git
- PowerShell 7+ (RepoUtils under `utils/`)

### Build

```powershell
cd src
dotnet build MaksIT.PostClient.slnx
```

### Tests

```powershell
utils\Invoke-TestEngine.bat
```

Coverage shields in `README.md` are rewritten by **CoverageBadges**.

### Release

1. Update [CHANGELOG.md](CHANGELOG.md) and bump `<Version>` in [src/Directory.Build.props](src/Directory.Build.props).
2. Commit on `main`, tag `v{version}` on HEAD (`v1.2.3` or SemVer prerelease such as `v0.1.0-alpha.1`). GitHub marks hyphenated versions as prerelease.
3. Run `utils\Invoke-ReleasePackage.bat`. That run publishes the portable zip (win-x64), Windows setup exe (self-contained, no .NET SDK on the PC), and Flatpak (via WSL Debian on Windows). Publishing the GitHub Release starts [macOS release assets](.github/workflows/macos-release.yml), which attaches unsigned `osx-arm64` and `osx-x64` DMGs.

WinGet listing is not in this repo yet: it will point at the same GitHub setup exe.

## Microsoft Store (MSIX)

`MsixPack` is present and disabled. It writes `releases/postclient-{version}.msix` from the win-x64 publish once `enabled` is true and `publisher` is the Partner Center identity. It is a full-trust desktop package (`runFullTrust`), x64. The package language stays English (`en-us`): the app picks English, Italian, French, German, or Spanish at runtime. Upload that file on an **MSIX** product in Partner Center. The Store re-signs it. An EXE/MSI product listing cannot take this file. The listing will be published soon. The README badge stays a placeholder until the Store ID exists. Add a Store listing for each UI language. The paste text is under [packaging/microsoft-store](packaging/microsoft-store/description.md).

The `.msix` is not a GitHub release asset.

Partner Center package identity for this product. The publisher and Store ID are not reserved yet:

| Field | Value |
|-------|--------|
| Package/Identity/Name | `MAKS-IT.Postclient` |
| Package/Identity/Publisher | not reserved. `CN=PartnerCenter` stops `MsixPack` |
| PublisherDisplayName | `MaksIT` |
| Store ID | not reserved |

Those name, publisher, and publisher display strings are `packageName`, `publisher`, and `publisherDisplayName` in `utils/engines/release/scriptSettings.json`. Replace `CN=PartnerCenter`, then set `enabled` to true.

Store listing copy:

| Field | Document |
|-------|----------|
| Listings for en-us, it-it, fr-fr, de-de, es-es | [packaging/microsoft-store/description.md](packaging/microsoft-store/description.md) |
| Product features (up to 20 bullets, 200 characters each) | same folder, `product-features.md` in each language |
| Keywords (up to 7, 40 characters each, 21 words total) | same folder, `keywords.md` in each language |
| Copyright, additional license terms, Developed by | same folder, `additional-info.md` in each language |
| Restricted capability `runFullTrust` | [packaging/microsoft-store/restricted-capabilities.md](packaging/microsoft-store/restricted-capabilities.md) |
| Store logos (9:16 poster, 1:1 box art, app tiles) | [packaging/microsoft-store/logos.md](packaging/microsoft-store/logos.md) |

### System requirements (Properties)

Partner Center → **Properties** → **System requirements**. A blank cell stays unset. Minimum is what the Store may warn on; Recommended does not warn.

The app is a win-x64 desktop mail client. It does not use a camera, microphone, radio, gamepad, or a specific GPU.

| Feature | Minimum | Recommended |
|---------|---------|-------------|
| Touch screen | | |
| Keyboard | Minimum | |
| Mouse | Minimum | |
| Camera | | |
| NFC HCE | | |
| NFC Proximity | | |
| Bluetooth LE | | |
| Telephony | | |
| Microphone | | |
| Xbox controller or gamepad | | |
| Windows Mixed Reality motion controllers | | |
| Windows Mixed Reality immersive headset | | |
| Memory | 2 GB | 4 GB |
| DirectX | Not specified | Not specified |
| Video memory | Not specified | Not specified |
| Processor | x64 | Not specified |
| Graphics | Not specified | Not specified |

## Flathub

`FlatpakPack` writes the GitHub Release `.flatpak` from the linux-x64 publish. A Flathub submission builds a release tag from source. Open the pull request on [flathub/flathub](https://github.com/flathub/flathub) against `new-pr`. Only `x86_64` is in scope until `linux-arm64` is published.

| Field | Document |
|-------|----------|
| Pull request and local check | [packaging/flathub/submission.md](packaging/flathub/submission.md) |
| Upstream files and NuGet sources | [packaging/flathub/sources.md](packaging/flathub/sources.md) |
| Manifest values | [packaging/flathub/manifest.md](packaging/flathub/manifest.md) |
| Finish args | [packaging/flathub/permissions.md](packaging/flathub/permissions.md) |
| Verified app id | [packaging/flathub/verification.md](packaging/flathub/verification.md) |

## GitHub setup exe

The GitHub Windows installer stays the WiX Burn `setup.exe`. Its switches are `/quiet /norestart` (install), `/repair /quiet /norestart` (repair), and `/uninstall /quiet /norestart` (uninstall). Signing that exe for an EXE/MSI Store listing needs a Trusted Root Authenticode certificate and the Burn order: payload PEs, then the MSI, then `wix burn detach` / sign the engine / `wix burn reattach` / sign `setup.exe` ([WiX signing](https://docs.firegiant.com/wix/tools/signing/)).

## Commit format

```text
(type): description
```

Types: `(feature):`, `(bugfix):`, `(refactor):`, `(perf):`, `(test):`, `(docs):`, `(build):`, `(ci):`, `(style):`, `(revert):`, `(chore):`.

Lowercase description; no trailing period.

## License

By contributing, you agree that your contributions are licensed under the terms in [LICENSE.md](LICENSE.md) (Apache 2.0).
