# 기여 안내

Windows 10/11 x64와 기본 .NET Framework 4.x 컴파일러를 사용합니다. 별도 패키지 설치는 필요하지 않습니다.

```powershell
.\build.ps1
.\test.ps1
```

버그 제보에는 재현 순서, 기대한 동작과 실제 동작, Windows 버전, 장치 펌웨어 버전(확인 가능한 경우), Studio 동시 실행 여부를 적어 주세요. UI 문제에는 화면 배율도 도움이 됩니다. 로그와 진단에는 앱 이름·로컬 경로가 포함될 수 있으므로 공유 전에 확인해 주세요. 개인 `settings.json` 전체를 올릴 필요는 없습니다.

변경은 하나의 문제에 집중해 주세요. 설정 저장, HID 프레이밍, 오디오 장치 선택, 비동기 화면 전송을 바꾸면 관련 자체 검증을 보강하고 하드웨어로 확인한 범위를 PR에 명시해 주세요. 자체 테스트만으로 실제 장치 호환성을 주장하지 않습니다.

코드는 `src/`에 있습니다. `Config.cs`는 설정·버튼 이미지, `MainForm.cs`는 편집·트레이·화면 전송 조율, `NativeHid.cs`는 USB 전송, `Audio.cs`는 Core Audio, `WideScreen.cs`는 가로 화면을 담당합니다. 앱 버전은 `Branding.cs`와 `assets/app.manifest`에서 관리합니다.

검증과 배포 절차는 [docs/TESTING.md](docs/TESTING.md)를 참고하세요.
