# Testing and distribution

## Automated checks

```powershell
.\build.ps1
.\test.ps1
```

Builds treat warnings as errors. `test.ps1` waits for the GUI executable and checks exit codes. Results appear beside the executable.

| Command | Output | Coverage |
| --- | --- | --- |
| `--self-test` | `test-results.txt`, `audio-preview.png`, `clock-preview.png`, `layout-*.png` | HID framing and parsing, URL/settings validation, ZIP manifests and packet boundaries, image sizes, overlay transitions, atomic saves and backups, editor drafts/previews/save/cancel/errors/reset, and control overlaps/clipping at default, 1120, and 1500 widths |
| `--render` | `preview.png` | Default UI rendering without a device |
| `--layout-test` | `layout-test-results.txt`, `layout-*.png` | Editor actions and layout checks at default, 1120/1500 widths, and compact 800×560, 900×640, 1024×700, 1200×700 client sizes; verifies both independent tab selections, no window scrollbars, and visible controls without overlap or clipping; no device access or settings writes |
| `--display-test` | `display-test-results.txt` | Focused display regression checks: complete image retention, update opcode, unchanged usage suppression, and combined clock/usage updates; no device or settings writes |
| `--audio-test` | `audio-test-results.txt` | Default input/output reads; volume and mute control/restoration for the test process's silent session |
| `--usage-test` | `usage-test-results.txt` | Optional diagnostics with real CLI logins and network access |
| `--diagnose` | `diagnostics.txt` | Execution desktop, HID interfaces, default audio devices, and foreground app state |

Self-tests and rendering do not connect to USB or save user settings. File replacement tests use temporary directories and clean up afterward. Usage tests use synthetic responses to check Codex, Claude, and Gemini parsing, used-percentage direction, missing values, resets, and caching without credentials or network access.

Startup tests use a temporary registry key outside Windows' Run key, then remove it. They check quoted executable paths, existing script registrations, enable/disable, preservation of other entries, and rollback after a settings-save failure. Preview mode disables startup changes. The startup dialog is rendered to `startup-preview.png`.

Display refresh tests alternate clock and usage updates around one- and two-minute boundaries. They verify that each ZIP includes all 14 display images and preserves other buttons and the latest clock image. They also check unchanged usage suppression and coalescing usage changes into wide-display uploads. Rendered application examples depend on the machine's Start menu. Run audio tests separately on a PC with an output device; CI does not assume one exists.

Website icon tests use synthetic responses for redirects, relative icons, HTML base URLs, shortcut path/query exclusion, ICO fallback, memory/disk caching, custom-image precedence, invalid/offline fallback, and retry intervals.

Wait for directly invoked GUI diagnostics to finish before inspecting their result:

```powershell
$process = Start-Process .\UlanziLite.exe -ArgumentList '--audio-test' -Wait -PassThru
$process.ExitCode
```

## Hardware checks

Run from File Explorer with Studio fully closed:

- Connect, confirm the current page, then unplug/reconnect and check page restoration.
- Launch websites, apps, files, and folders; switch pages with side buttons.
- Check automatic website icons in the editor and on the device after saving, custom-image precedence, restart persistence, and offline cache behavior.
- Turn each knob both ways and press it. Confirm rotation preserves mute.
- Control a foreground app playing audio; retain it when opening settings; check waiting states without an audio session.
- Check overlay expiration, extension after repeated input, clock restoration, and updates during page changes.
- Leave the app running for at least five minutes and two usage queries. Verify all meters remain visible and check for flicker or input delays.
- Switch buttons/pages while editing; save/cancel; hide, restore, and exit through the tray.
- Apply startup and tray preferences, restart, confirm the editor stays hidden and device control works, then reopen via the tray. Sign out/in to check Windows startup; disable it afterward and verify it no longer launches. Check Studio is closed.
- Check clipping, scrolling, and keyboard focus at 100%, 150%, and 200% scaling.

Automated tests cannot prove physical display appearance, audible results, or compatibility with all firmware. Record manual results with OS, scaling, and firmware versions.

## Portable distribution

```powershell
.\package.ps1
```

This builds and verifies a fresh executable in `dist/`, then creates a versioned ZIP and SHA-256 checksum. The package includes the executable, README, contribution guide, and docs, plus `LICENSE` if present. Personal settings, backups, logs, diagnostics, and test output are excluded. Existing personal settings and the working root executable are not replaced by packaging.

The Windows GitHub Actions workflow runs the same command and uploads reports and packages as artifacts. It does not publish releases or create tags. The repository owner determines the distribution license.

To refresh the website screenshot, run `--render` and copy `preview.png` to `docs/screenshot.png`. The separate [Pages workflow](../.github/workflows/pages.yml) publishes `docs/` after a push to `main`; select **GitHub Actions** in repository Pages settings first.
