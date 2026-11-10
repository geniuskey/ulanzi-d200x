# Contributing

Use Windows 10/11 x64 and the built-in .NET Framework 4.x compiler. No package installation is required.

```powershell
.\build.ps1
.\test.ps1
```

Write code, comments, documentation, issue reports, and pull requests in English. Keep Unicode support for user-provided button labels and paths.

For bug reports, include reproduction steps, expected and actual behavior, Windows version, device firmware if known, and whether Studio was running. Include display scaling for UI issues. Review logs and diagnostics for app names and local paths before sharing; you do not need to upload your entire personal `settings.json`.

Keep changes focused. When changing settings persistence, HID framing, audio device selection, or asynchronous display uploads, extend relevant self-tests and state which hardware checks you performed in your PR. Self-tests alone do not establish device compatibility.

Source lives in `src/`. `Config.cs` handles settings and button artwork; `MainForm.cs` coordinates editing, tray behavior, and display uploads; `NativeHid.cs` handles USB; `Audio.cs` wraps Core Audio; `WideScreen.cs` draws the wide display. Update app versions in `Branding.cs` and `assets/app.manifest` together.

The promotional website is buildless HTML/CSS in `docs/`. Keep links relative for GitHub Pages project hosting, use English copy, and check desktop and mobile layouts. After UI changes, regenerate the English screenshot with `--render` and copy `preview.png` to `docs/screenshot.png`.

See [testing and distribution](docs/TESTING.md).
