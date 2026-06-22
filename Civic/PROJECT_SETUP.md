# Civic 프로젝트 환경 설정

## 개발 환경

- Unity Editor: **6000.0.76f1**
- Render Pipeline: **Universal Render Pipeline (URP) 17.0.4**
- 입력 시스템: **Input System 1.19.0**
- 기본 IDE: Visual Studio (`Microsoft.VisualStudio.Workload.ManagedGame` 권장)

Unity Hub에서 위 Unity Editor 버전을 설치한 뒤, 이 문서가 있는 `Civic` 폴더를 프로젝트로 등록합니다. 다른 Unity 버전으로 열면 에셋 또는 프로젝트 설정이 자동으로 변경될 수 있으므로 가급적 정확히 같은 버전을 사용합니다.

## 프로젝트 실행

1. 저장소를 클론합니다.
2. Unity Hub에서 **Add project from disk**를 선택합니다.
3. 저장소 안의 `Civic` 폴더를 지정합니다.
4. Unity가 패키지를 복원하고 `Library` 폴더를 생성할 때까지 기다립니다.
5. `Assets/Scenes/SampleScene.unity` 씬을 엽니다.

현재 빌드 설정에는 `SampleScene`이 활성화되어 있습니다.

## AI 및 명령줄 작업

저장소 루트의 PowerShell 래퍼가 `ProjectVersion.txt`에 기록된 정확한 Unity Editor를 탐색합니다.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action Compile
powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action GenerateUI
powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action TestEditMode
powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action TestPlayMode
```

열린 Unity Editor에서는 `Tools > Civic > UI > Generate`와 `Tools > Civic > UI > Validate`를 사용합니다. 동일 프로젝트를 Editor에서 열어 둔 상태로 batchmode를 실행하지 않습니다.

- **Generate**: 생성기 코드로 Base Prefab을 갱신하고 기존 사용자 Variant를 보존한 뒤 자동으로 Validate까지 수행합니다. 여러 번 실행해도 같은 이름의 UI·컴포넌트·Scene `UIRoot`를 중복 생성하지 않도록 설계되어 있습니다.
- **Validate**: Base/Variant 존재·타입·상속 관계, Missing Script, 필수 직렬화 참조, Canvas와 입력 EventSystem 구성을 검사합니다. 메뉴 실행 시 성공 또는 실패 대화상자가 표시됩니다.

생성기를 다시 실행할 때는 사용자 Variant의 override는 유지되지만 `Generated/` 아래 Base를 직접 수정한 내용은 현재 생성기 코드로 덮어씁니다. 따라서 사용자 디자인 수정은 Variant에서 수행합니다. 일회성 Editor 도구의 생성·검증·삭제 절차와 핵심 생성기 유지 기준은 저장소 루트 `AGENTS.md`의 "UI 생성·검증 도구 수명주기"를 따릅니다.

## UI 제작 원칙

- 런타임 UI는 uGUI 프리팹으로 제작합니다.
- `Assets/_Project/Prefabs/UI/Generated/`는 AI가 갱신하는 Base Prefab입니다.
- `Assets/_Project/Prefabs/UI/`의 Variant는 사용자가 Prefab Mode에서 편집하며 생성기가 덮어쓰지 않습니다.
- 런타임 코드는 저장된 프리팹을 인스턴스화하고 데이터·이벤트만 바인딩합니다.

## 주요 패키지

정확한 패키지 버전은 `Packages/manifest.json`과 `Packages/packages-lock.json`을 기준으로 합니다.

- AI Navigation: 2.0.12
- Input System: 1.19.0
- Multiplayer Center: 1.0.0
- Universal Render Pipeline: 17.0.4
- Test Framework: 1.6.0
- Timeline: 1.8.12
- Visual Scripting: 1.9.11

패키지는 Unity Package Manager가 자동으로 복원합니다. 특별한 이유가 없다면 패키지 버전을 임의로 변경하지 않습니다.

## Git 관리 기준

다음 항목은 Unity 또는 IDE가 로컬에서 자동 생성하므로 Git에 추가하지 않습니다.

- `Library/`, `Temp/`, `Logs/`, `UserSettings/`
- `.vs/`, `*.csproj`, `*.sln`
- 로컬 빌드 결과물과 캐시

`Assets`, `Packages`, `ProjectSettings`와 모든 Unity `.meta` 파일은 함께 커밋합니다. 자세한 제외 규칙은 같은 폴더의 `.gitignore`를 따릅니다.

이 프로젝트는 Git LFS를 사용하지 않습니다. 이미지·오디오·모델을 포함한 바이너리 에셋도 일반 Git 객체로 추적합니다.
