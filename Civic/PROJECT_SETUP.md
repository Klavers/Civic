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
