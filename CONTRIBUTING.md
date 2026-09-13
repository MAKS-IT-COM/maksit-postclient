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

## Commit format

```text
(type): description
```

Types: `(feature):`, `(bugfix):`, `(refactor):`, `(perf):`, `(test):`, `(docs):`, `(build):`, `(ci):`, `(style):`, `(revert):`, `(chore):`.

Lowercase description; no trailing period.

## License

By contributing, you agree that your contributions are licensed under the terms in [LICENSE.md](LICENSE.md) (Apache 2.0).
