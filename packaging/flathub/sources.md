# Flathub sources

Upstream files stay in this repository. The Flathub build installs them from the release tag.

| Path | Role |
|------|------|
| [data/eu.postclient.desktop.metainfo.xml](../../data/eu.postclient.desktop.metainfo.xml) | AppStream. Id `eu.postclient.desktop`. |
| [data/eu.postclient.desktop.desktop](../../data/eu.postclient.desktop.desktop) | Launcher. `Exec=postclient`. `Terminal=false`. |
| [src/MaksIT.PostClient.UI/Assets/icon.svg](../../src/MaksIT.PostClient.UI/Assets/icon.svg) | Icon, installed as `share/icons/hicolor/scalable/apps/eu.postclient.desktop.svg`. |
| [LICENSE.md](../../LICENSE.md) | Apache-2.0. Install to `$FLATPAK_DEST/share/licenses/eu.postclient.desktop/` because the filename is `LICENSE.md`. |

Screenshot URLs in the metainfo use branch `main`, where `assets/screenshots/` is stored. The `0.4.2` release entry is the current packaged version. On a newer release, point `<image>` at the tag that contains the screenshots and `<release>` at that version.

## Pull request

| File | Where |
|------|--------|
| `eu.postclient.desktop.yml` or `.json` | Top of the Flathub checkout. Values are in [manifest.md](manifest.md). |
| NuGet sources | Generated, then committed in the pull request. The build has no network. |
| `flathub.json` | Limit architectures to `x86_64` until `linux-arm64` is published. |

```json
{
  "only-arches": ["x86_64"]
}
```

Generate the NuGet list with [flatpak-builder-tools/dotnet](https://github.com/flatpak/flatpak-builder-tools/tree/master/dotnet) or [Nickvision.FlatpakGenerator](https://github.com/nickvisionapps/flatpakgenerator).

The pull request steps are in [submission.md](submission.md). Finish args are in [permissions.md](permissions.md). Verification is in [verification.md](verification.md).
