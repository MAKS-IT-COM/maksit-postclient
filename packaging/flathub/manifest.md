# Flathub manifest

Values for `eu.postclient.desktop.yml` (or `.json`) at the top of the Flathub checkout.

| Item | Value |
|------|--------|
| Application id | `eu.postclient.desktop` |
| Domain | `postclient.eu` |
| Homepage | [https://maks-it.com/](https://maks-it.com/) |
| Project | `src/MaksIT.PostClient.UI/MaksIT.PostClient.UI.csproj` |
| Target | `net10.0` |
| Source | Release tag on `https://github.com/MAKS-IT-COM/maksit-postclient` (currently `v0.4.2`) |
| Runtime | `org.freedesktop.Platform` |
| SDK | `org.freedesktop.Sdk` |
| SDK extension | `org.freedesktop.Sdk.Extension.dotnet10` |
| Runtimes | `linux-x64`, self-contained. `linux-arm64` is not published yet |
| Command | `postclient` |
| Apphost | `MaksIT.PostClient.UI` |
| Project license | `Apache-2.0` |
| Metadata license | `CC0-1.0` |

Publish the UI project. It references Client and Shared. Tests stay out of the package. The Flatpak command matches the desktop `Exec`. The published apphost name is `MaksIT.PostClient.UI`.

Use the newest Freedesktop branch that `org.freedesktop.Sdk.Extension.dotnet10` publishes, and the branch Flathub requires on the day of submission. Extension usage is in the [extension README](https://github.com/flathub/org.freedesktop.Sdk.Extension.dotnet10). Avalonia native libraries follow the runtime identifier.

`dotnet publish` uses `--source ./nuget-sources` before `--source /usr/lib/sdk/dotnet10/nuget/packages`.

The app id `eu.postclient.desktop` is the one already shipped in [`data/`](../../data/). Verification uses `postclient.eu`, which is a different host from the homepage. See [verification.md](verification.md).

Only `x86_64` is in scope until a `linux-arm64` publish exists. Say so in `flathub.json`.

The pull request steps are in [submission.md](submission.md). Source files are in [sources.md](sources.md). Finish args are in [permissions.md](permissions.md). Verification is in [verification.md](verification.md).
