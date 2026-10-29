# Flathub permissions

Finish args for the manifest. The GitHub bundle uses this set. Keep the Flathub set to what the app uses.

```text
--share=ipc
--share=network
--socket=wayland
--socket=x11
--filesystem=xdg-config
--filesystem=xdg-data
--filesystem=xdg-cache
```

Network reaches the mail server and Identity Hub sign-in (`https://identity.maks-it.com`). Settings stay in the sandbox config directory (`~/.config/postclient`). The archive, folder stores created in the default location, and secrets stay in the data directory (`~/.local/share/postclient`). Cache covers the web engine profile when one is created inside the sandbox. X11 and Wayland stay together until Avalonia's native Wayland path no longer hangs on GNOME. Linux runs through X11/XWayland. The app has no audio, so the sandbox does not take pulseaudio.

A mailbox you already have, or a local folder store, is required after install. `--share=network` uses this computer's network, including localhost. A file outside the config, data, and cache directories is not visible to the sandbox. Choosing a file in a portal dialog grants that file only. HTML mail shows the text body: WebKitGTK is a host library and is not in the Freedesktop runtime. Desktop notices that call `notify-send` on the host do not run inside the sandbox. Background sync and a mailbox shared with other accounts on the PC need a host install. The sandbox cannot install a system service or write `/var/lib/maksit/postclient`. The metainfo says the archive stays on this computer. The submission is from the project that maintains this repository.

The pull request steps are in [submission.md](submission.md). Manifest values are in [manifest.md](manifest.md). Verification is in [verification.md](verification.md).
