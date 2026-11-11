# Ulanzi Lite for D200X

**Your shortcuts. Your sound. One lightweight controller.**

A portable Windows controller for the Ulanzi D200X: launch apps and websites, check AI usage, control audio with three knobs, and keep a clock on your desk.

[Explore the website](https://geniuskey.github.io/ulanzi-d200x/) · [Download the Windows app](https://github.com/geniuskey/ulanzi-d200x/raw/refs/heads/main/UlanziLite.exe) · [Contribute](CONTRIBUTING.md)

Windows Forms · .NET Framework · Win32 HID · Windows Core Audio. No additional runtime, web server, or driver installation. **0.3.0 preview**. An independent, unofficial project, not affiliated with Ulanzi.

![Ulanzi Lite settings window](docs/screenshot.png)

## Get started

Requires Windows 10/11 x64 and .NET Framework 4.5 or later.

1. Place `UlanziLite.exe` in a **writable folder**. Extract a distribution ZIP before running it.
2. Quit Ulanzi Studio, including its tray process, and connect your D200X over USB.
3. Run `UlanziLite.exe` from File Explorer. Administrator privileges are not required.
4. Select a button, edit its action, and choose **Save and apply to device**.

You can edit and save without a connected device. The saved current page is applied automatically when it connects. The app does not install itself or register for startup.

## Make the buttons yours

The layout provides **10 editable buttons across three pages**, plus three fixed AI usage meters. Switch pages with the device's side buttons or the page selector.

| Action | Input |
| --- | --- |
| Open website | An `https://` or `http://` URL |
| Open app / file / folder | Full path to an executable, Start menu shortcut, file, or folder |
| Empty button | Leave the URL or path blank |

Labels support up to 40 characters. The installed-app picker shows app icons beside their names, including the selected app. Choose a PNG, JPG, or BMP icon; the app references the original image file, so keep it in place. Default shortcuts are editable examples. Existing personal labels and paths are preserved; new defaults and the interface are English.

Website shortcuts fetch their icons automatically. Custom images take priority; unavailable icons fall back to the first character of the label. Downloads run in the background and are cached in `website-icons/` beside the executable. Only the site's home page and icon URLs are requested; the shortcut's path and query are excluded.

- **Test action** runs the current editor input; **Run saved** runs the saved action.
- Edits update the button artwork and preview immediately. Save to apply them to the device.
- Invalid inputs show an explanation and move focus to the affected field.
- Drafts survive button and page changes. **● Unsaved** means changes have not been applied.
- **Ctrl+S** saves the selected button. **Esc** or **Cancel edit** discards its draft.
- **Reset button** creates a draft that you can save or cancel.
- When quitting, choose whether to save, discard, or cancel if drafts remain. Drafts live in memory and cannot be recovered after a crash.

Volume step, brightness, overlay duration, and current page save immediately. The window expands its button and audio areas when resized; smaller windows scroll.

## AI usage at a glance

The bottom three buttons show **ChatGPT / Claude / Gemini** usage using existing CLI sign-ins. Click a meter or press its device button to open the related usage page.

| Meter | What it measures |
| --- | --- |
| ChatGPT | Codex five-hour and weekly usage from local `.codex` session logs |
| Claude | Account five-hour and weekly usage using the existing `.claude` login |
| Gemini | CLI Pro and Flash quotas using the existing `.gemini` login; these can differ from web subscription limits |

Bars show the **percentage used**. Unknown values appear as `—`, with separate connection, permission, and stale-data states. Existing bindings for the bottom three buttons remain in your settings, but meters take priority.

See [usage sources, privacy, and diagnostics](docs/USAGE.md).

## Three knobs. Your audio, within reach.

From left to right: **microphone / active app / master output**. Turn clockwise to increase volume, counterclockwise to decrease, and press to toggle mute. Turning a knob preserves mute. The default step is 2%, adjustable from 1–10%.

| Knob | Target |
| --- | --- |
| Microphone | Windows default **communications input** device |
| Active app | Audio sessions sharing the foreground app's executable name; multiple devices, instances, or browser tabs may be adjusted together |
| Master output | Windows **default output** device |

Opening the settings window retains the last selected external app. If it has no audio session, the app shows a waiting state without changing volume.

The wide display shows your PC's local time and date, with English weekday names. Knob input switches it to three volume levels, mute states, and the active knob. It returns to the clock three seconds after the last input by default; choose 1–30 seconds. Device control continues in the system tray.

The clock uses large hours and minutes with smaller seconds. All device images reserve the bottom 5% for the viewing angle. The interface uses Segoe UI with Arial fallback; numbers use Bahnschrift with Segoe UI fallback. No font installation is required. Custom Unicode labels remain supported through Windows font fallback.

Closing the window hides it to the tray. Double-click the tray icon to reopen it; choose **Exit** in the tray menu to quit.

## Settings and troubleshooting

Choose **Data folder** to open the data location. All app data stays beside the executable.

| File | Purpose |
| --- | --- |
| `settings.json` | Saved buttons and device settings |
| `settings.json.bak` | Settings immediately before the last save |
| `ulanzi-lite.log` / `.log.old` | Current and previous logs, rotating at about 512 KiB |

To recover damaged settings, quit the app, keep a separate copy of `settings.json`, then copy `settings.json.bak` to `settings.json`. To start fresh, rename the settings file and relaunch. The app does not silently overwrite damaged settings.

| Symptom | Check |
| --- | --- |
| Waiting for connection | Fully quit Studio; check the USB data cable and port. Reconnect is attempted every two seconds. |
| Saved, but device update failed | Check the connection. Reconnection reapplies the saved current page. |
| Active app volume is waiting | Play audio in that app and select its window. |
| Wrong microphone or output changes | Check the Windows default communications input and default output devices. |
| Save fails | Check folder write permissions and settings/image paths. |
| App launching or foreground detection is limited | Run directly from File Explorer instead of an isolated test desktop. |

To restore your Studio display and brightness, exit Lite and reapply your original profile in Studio. Lite does not modify device firmware or Studio configuration files.

Claude and Gemini usage checks contact their service providers using existing CLI credentials. Codex reads local logs only. Tokens and conversation contents are not stored in app settings or logs. There is no telemetry, voice recording, or keyboard input collection. Website shortcuts open your default browser. Logs and diagnostics may contain app names and local paths; review them before sharing.

## Build and verify

Build with the Windows .NET Framework compiler, without installing packages:

```powershell
.\build.ps1
.\test.ps1
.\package.ps1
```

Automated checks cover HID framing, configuration and backups, image bundles, overlay transitions, and the editor UI without connecting to hardware. Actual USB input, displays, and audio require separate hardware checks. Compatibility with every firmware and environment is not guaranteed.

[Testing and packaging](docs/TESTING.md) · [Contributing](CONTRIBUTING.md) · [Protocol notes](docs/PROTOCOL.md)

## GitHub Pages

The English promotional website lives in `docs/index.html`, with local CSS and images and no build dependencies. `.github/workflows/pages.yml` deploys `docs/` on pushes to `main`, or through **Actions → Deploy GitHub Pages → Run workflow**.

In **Settings → Pages → Build and deployment**, select **GitHub Actions** as the source. After pushing these files and a successful deployment, the site is available at <https://geniuskey.github.io/ulanzi-d200x/>. Markdown manuals remain linked through GitHub for readable rendering.

## Acknowledgments

USB compatibility work references publicly documented protocol information from [edubox/opendeck-ulanzi-d200x](https://github.com/edubox/opendeck-ulanzi-d200x). The host code is independently implemented and depends on neither that plugin nor Studio. Audio uses Windows Core Audio.
