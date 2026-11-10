# AI usage meters

Buttons 11, 12, and 13 show ChatGPT, Claude, and Gemini meters on every page. The first ten buttons remain editable. Existing bottom-row bindings are preserved in settings, but this version displays and launches usage meters in their place.

Bars and numbers show the **percentage used**. Missing data appears as `—`, distinct from 0%. Values at 75% turn orange; values at 90% turn red. Every device image reserves the bottom 5%.

## Data sources

| Meter | Source | Scope |
| --- | --- | --- |
| ChatGPT | `token_count.rate_limits` in rollout logs under `CODEX_HOME` or `~/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl` | **Codex CLI** five-hour and weekly limits, rather than general ChatGPT chat usage |
| Claude | Existing login in `CLAUDE_CONFIG_DIR` or `~/.claude/.credentials.json`, querying `api.anthropic.com/api/oauth/usage` | Account five-hour and weekly usage |
| Gemini | Existing `~/.gemini/oauth_creds.json`, querying Google Code Assist `loadCodeAssist` and `retrieveUserQuota` | Highest usage among **Gemini CLI** Pro and Flash model families, which may differ from web app subscription limits |

Every ten seconds, Codex checks the tails of the 12 most recently modified log files from the last 14 days. It makes no OpenAI API calls and reads no OpenAI login credentials. Values update when the CLI records fresh limits.

Claude and Gemini are queried over HTTPS at startup and every two minutes. Requests have timeouts and retry on the next cycle. No browser extension, manual percentage entry, or paid API key is required.

The app does not modify CLI login files or settings. Expired Claude or Gemini access tokens must be renewed through the corresponding CLI. Lite does not use refresh tokens or embed OAuth client credentials. It does not create accounts, change plans, or purchase quota.

These integrations depend on CLI implementation details. Changes to authentication, responses, or access can replace readings with a status message. A subscription alone may not provide readings without the relevant CLI and a valid login.

## Cache and errors

The bottom of each meter shows its observation time. After 15 minutes, bars turn gray and show `Cached`. Numbers for a window whose supplied reset time has passed are hidden; they are not reset to 0% without confirmation. Readings are not persisted to a separate file when the app exits.

| Status | Meaning |
| --- | --- |
| `CLI sign-in required` | No login file was found. |
| `Sign in to CLI again` | Authentication expired or was rejected. |
| `CLI access denied` | The quota query returned 403; web subscription access may differ from CLI access. |
| `Rate limited · Retrying` | The service limited requests. |
| `Check network` | A connection failed or timed out. |

Tokens and conversation contents are never written to app settings, logs, or diagnostic output. Outbound usage requests are limited to the relevant provider's authentication and quota services.

## Open usage pages

Press a device meter or click it in the settings window to open your default browser:

- ChatGPT: [Codex usage dashboard](https://chatgpt.com/codex/settings/usage).
- Claude: [Settings → Usage](https://claude.ai/settings/usage).
- Gemini: [Gemini](https://gemini.google.com/). Use the web app's usage controls if available; web and CLI readings can have different scopes.

## Diagnostics

```powershell
Start-Process .\UlanziLite.exe -ArgumentList '--usage-test' -Wait
```

`usage-test-results.txt` contains connection states, available percentages, and reset times. This does not connect to the device. It uses real accounts and network access, so it is excluded from default self-tests and CI.

## Reference material

- [Claude usage guidance](https://support.claude.com/en/articles/9797557-usage-limit-best-practices)
- [Claude Code status line data](https://code.claude.com/docs/en/statusline)
- [Gemini CLI quota implementation](https://github.com/google-gemini/gemini-cli/blob/main/packages/core/src/code_assist/server.ts)
- [Gemini web app limits](https://support.google.com/gemini/answer/16275805?hl=en)
