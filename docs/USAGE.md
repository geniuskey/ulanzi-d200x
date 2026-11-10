# AI 구독 사용량 미터

하단 버튼 11·12·13은 모든 페이지에서 ChatGPT·Claude·Gemini 미터로 고정됩니다. 위 10개 버튼은 기존처럼 편집합니다. 기존 설정의 하단 세 버튼 데이터는 삭제하지 않지만 이 버전에서는 사용량 미터를 우선 표시·실행합니다.

미터의 막대와 숫자는 **사용한 비율**입니다. 빈 데이터는 `—`로 표시하며, 0%와 구분합니다. 75% 이상은 주황, 90% 이상은 빨강입니다. 모든 이미지의 하단 5% 여백은 유지합니다.

## 연동 방식

| 미터 | 데이터 원본 | 표시 범위 |
| --- | --- | --- |
| ChatGPT | `CODEX_HOME` 또는 `~/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl`의 `token_count.rate_limits` | **Codex CLI**의 5시간·주간 한도. 일반 ChatGPT 채팅 사용률을 의미하지 않습니다. |
| Claude | `CLAUDE_CONFIG_DIR` 또는 `~/.claude/.credentials.json`의 기존 로그인으로 `api.anthropic.com/api/oauth/usage` 조회 | 계정의 5시간·주간 사용률 |
| Gemini | `~/.gemini/oauth_creds.json`의 기존 로그인으로 Google Code Assist의 `loadCodeAssist`·`retrieveUserQuota` 조회 | **Gemini CLI** Pro·Flash 계열에서 각각 가장 많이 사용한 모델의 할당량. Gemini 웹 앱 구독 사용률과 다를 수 있습니다. |

Codex 로그는 10초 간격으로 최근 14일 중 마지막으로 수정된 12개 파일의 끝부분을 확인합니다. 별도 OpenAI API 호출이나 로그인 정보 읽기는 하지 않습니다. CLI가 새 한도 정보를 기록할 때 갱신됩니다.

Claude와 Gemini는 시작 시와 이후 2분 간격으로 HTTPS 조회합니다. 요청마다 시간 제한이 있고, 실패하면 다음 주기에 다시 시도합니다. 별도 브라우저 확장 프로그램·수동 사용률 입력·유료 API 키는 필요하지 않습니다.

CLI 로그인 파일과 설정은 변경하지 않습니다. Claude 토큰이 만료되면 CLI에서 다시 로그인해야 합니다. Gemini도 CLI가 갱신한 유효한 access token만 읽습니다. 토큰이 만료되면 Gemini CLI에서 인증을 갱신해야 합니다. 앱이 refresh token으로 별도 갱신하거나 OAuth 클라이언트 정보를 내장하지 않습니다. 계정 자동 가입·플랜 변경·할당량 구매는 수행하지 않습니다.

이 조회 경로들은 CLI 클라이언트의 구현에 의존합니다. 서비스가 인증 방식·응답을 변경하거나 접근을 거절하면 수치 대신 상태를 표시합니다. CLI를 사용하지 않거나 로그인하지 않았다면 구독 중이어도 자동 조회되지 않을 수 있습니다.

## 캐시와 오류

확인 시각을 미터 하단에 표시합니다. 마지막 확인 후 15분이 지나면 막대를 회색으로 바꾸고 `캐시`로 표시합니다. 제공된 초기화 시각이 지난 창의 숫자는 숨기며, 서버 확인 없이 0%로 초기화하지 않습니다. 앱 종료 시 측정값은 별도 파일에 저장하지 않습니다.

- `CLI 로그인 필요`: 해당 CLI의 로그인 파일을 찾지 못했습니다.
- `CLI 재로그인 필요`: 로그인 인증이 만료되거나 거절되었습니다.
- `CLI 조회 권한 없음`: 해당 계정의 할당량 조회가 403으로 거절되었습니다. 웹 구독이 있어도 CLI 접근 권한과 다를 수 있습니다.
- `조회 제한 · 재시도`: 서비스가 조회를 제한했습니다.
- `네트워크 확인`: 연결에 실패하거나 시간 제한을 넘었습니다.

읽은 토큰·대화 내용은 앱 설정·로그·진단 결과에 기록하지 않습니다. 외부 전송은 해당 서비스의 인증·한도 조회에 한정됩니다.

## 버튼으로 사용량 확인

장치 버튼 또는 설정 창의 해당 미터를 누르면 기본 브라우저로 엽니다.

- ChatGPT: [Codex 사용량 대시보드](https://chatgpt.com/codex/settings/usage)
- Claude: [Settings → Usage](https://claude.ai/settings/usage)
- Gemini: [Gemini](https://gemini.google.com/)를 연 뒤 **설정 → 사용량 한도**를 선택합니다. 공식 안내에서 이 경로를 제공하며, 사용량 대화상자의 직접 URL은 확인되지 않았습니다. 웹 화면과 CLI 미터의 집계 범위는 다를 수 있습니다.

## 진단

```powershell
Start-Process .\UlanziLite.exe -ArgumentList '--usage-test' -Wait
```

`usage-test-results.txt`에 서비스별 연결 상태와 확인 가능한 사용률·초기화 시각만 기록합니다. 장치에는 연결하지 않습니다. 실제 계정과 네트워크를 사용하므로 기본 자체 테스트와 CI에는 포함하지 않습니다.

## 참고한 원본

- [OpenAI 공식 사용량 대시보드 안내](https://learn.chatgpt.com/docs/pricing)
- [Claude 계정 사용량 안내](https://support.claude.com/en/articles/9797557-usage-limit-best-practices)
- [Claude Code 한도 데이터 스키마](https://code.claude.com/docs/en/statusline)
- [Gemini CLI 할당량 조회 구현](https://github.com/google-gemini/gemini-cli/blob/main/packages/core/src/code_assist/server.ts)
- [Gemini 웹 앱의 한도 확인 경로](https://support.google.com/gemini/answer/16275805?hl=ko)
