# P00 — Unity 작업환경 및 프리팹 UI 기반

## §0 Context

Unity 프로젝트 자체는 `6000.0.76f1`, URP, Input System, uGUI, Test Framework 구성을 갖췄지만 저장소 운영 스킬과 Unity 에셋 제작 절차가 연결되지 않았다. 이 계획은 Git LFS 제거, 스킬 경로 정리, Git Bash 호출 고정, Editor 시점의 프리팹 생성·검증 기반을 한 변경으로 정착시킨다.

- 선행: 없음 — 신규 Unity 프로젝트의 최초 구현 계획
- 기준 코드: `AGENTS.md`, `.agents/skills/`, `Civic/Assets/`, `Civic/PROJECT_SETUP.md`
- 관련 CPR: 없음

> **검증 기록 (2026-06-22)**: CLI 프리팹 생성·검증, EditMode 2/2, PlayMode 1/1이 통과했다. 사용자가 Unity Editor에서 `SampleScene`을 직접 열어 dummy idle UI가 정상 동작함을 추가 확인했다.
>
> **열린 Editor 재검증 (2026-06-23)**: 사용자가 Generate 재실행, Validate 대화상자, Test Runner 및 클릭 보상 업그레이드의 수동 동작을 정상 확인했다. 재실행 전후 Base·Variant·Scene SHA-256이 동일하고 생성 계층의 WorkButton·UpgradeButton·Canvas·EventSystem이 각각 1개임을 확인했다.

## §1 목표 (Goals)

1. **재현 가능한 저장소 운영** — Git LFS 없이 Unity 텍스트·바이너리 에셋을 일반 Git으로 관리하고 저장소 스킬 경로를 실제 구조와 일치시킨다.
2. **결정론적 Git Bash 실행** — PowerShell의 WSL `bash.exe` 오선택을 피하고 설치된 Git for Windows Bash를 명시적으로 사용한다.
3. **프리팹 우선 UI** — 런타임 계층 생성 대신 Editor 생성기가 Base Prefab과 사용자 편집용 Variant를 만든다.
4. **자동 검증 가능성** — EditMode·PlayMode 테스트와 Editor 검증기를 통해 프리팹 계약을 확인한다.

## §2 비목표 (Non-goals)

- 게임 콘셉트와 idle/incremental 시스템 설계
- 선택 Unity 패키지 제거
- Unity Editor 브리지 또는 MCP 플러그인 도입
- 플레이어 빌드 대상과 배포 파이프라인 확정

## §3 핵심 설계

- AI가 갱신하는 Base Prefab과 사용자가 수정하는 Prefab Variant를 분리한다.
- 생성기는 기존 사용자 Variant를 덮어쓰지 않는다.
- 런타임 UI 코드는 저장된 프리팹 인스턴스화와 바인딩만 담당한다.
- Unity 버전과 패키지 버전의 정본은 각각 `ProjectVersion.txt`, `Packages/manifest.json`이다.

## §4 단계 (Steps)

### Step 1 — 저장소 운영 정비
- [x] `.gitattributes`에서 LFS 필터를 제거하고 바이너리 속성만 유지한다.
- [x] `check-and-verify`가 PR·Issue·로컬 계획 파일의 구현 항목을 검증 후 갱신하도록 유지·확장한다.
- [x] legacy Codex 스킬 경로 오참조를 `.agents/skills`로 교정한다.
- [x] Git Bash 명시 경로와 Unity 명령을 문서화한다.

### Step 2 — Unity 프리팹 기반 구현
- [x] Runtime·Editor·Tests assembly를 분리한다.
- [x] `UiPrefabGenerator.GenerateAll()`과 `UiPrefabValidator.ValidateAll()`을 구현한다.
- [x] uGUI Base Prefab과 사용자 Variant 생성 규칙을 구현한다.
- [x] `SampleScene`에 초당 자원 증가와 클릭 보상이 동작하는 dummy idle UI를 설치한다.

### Step 3 — 검증
- [x] LFS, Git Bash, 경로 정합성을 검사한다.
- [x] exact Unity 버전에서 EditMode·PlayMode 테스트를 실행한다.
- [x] 생성기 재실행 후 사용자 Variant override 보존을 확인한다.

### Step 4 — 열린 Editor 작업 절차 재검증
- [x] 열린 Editor 상태에서 런타임·생성기·테스트 소스만 외부 수정하고 Unity 직렬화 에셋은 직접 수정하지 않는다.
- [x] 클릭 보상 업그레이드 기능이 열린 Editor의 자동 import와 compile을 통과한다.
- [x] Editor 메뉴로 Base Prefab을 재생성·검증하고 사용자 Variant 보존을 확인한다.
- [x] Test Runner와 `SampleScene` 수동 실행으로 업그레이드 구매·비용 차감·클릭 보상 증가를 확인한다.
- [x] `Generate`를 2회 이상 실행해 UI·컴포넌트·Scene 인스턴스가 중복되지 않고 Variant override가 보존되는지 확인한다.
- [x] 메뉴 기반 Generate·Validate가 성공·실패 결과를 대화상자로 표시하는지 확인한다.

## §검증 (Verification)

- [x] `git lfs ls-files` 결과가 비어 있다.
- [x] `git check-attr filter -- Civic/Assets/TutorialInfo/Icons/URP.png` 결과가 `unspecified`다.
- [x] Git Bash 명시 경로에서 base/worktree 탐지 스크립트가 성공한다.
- [x] 저장소에서 legacy Codex 스킬 경로 오참조가 0건이고 `check-and-verify`가 로컬 계획 파일을 처리한다.
- [x] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action GenerateUI`가 성공한다.
- [x] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action TestEditMode`가 성공한다.
- [x] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action TestPlayMode`가 성공한다.
- [x] Prefab Validator가 Missing Script·필수 참조·Variant 관계 오류를 검출한다.
- [x] 열린 Editor에서 `Tools > Civic > UI > Generate`와 `Validate`가 새 업그레이드 UI를 반영하고 오류 없이 완료된다.
- [x] 열린 Editor의 EditMode·PlayMode 테스트가 각각 전부 통과한다.

## §리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| Unity Editor가 열린 상태에서 batchmode가 프로젝트 잠금과 충돌 | 중 | 원본 Editor 메뉴·Test Runner를 우선 사용하고, 자동화가 필요하면 Editor 종료 또는 격리된 임시 복사본에서 사전검증 |
| 외부 코드 수정 직후 compile 중 메뉴를 실행 | 중 | Console 컴파일 완료를 확인한 뒤 Generate·Validate·Test Runner 순서로 실행 |
| 핵심 생성기를 일회성 도구로 오인해 삭제 | 고 | 핵심 도구는 유지하고 `Editor/OneShot/`의 독립 도구만 사용자 확인·참조 검색·재검증 후 삭제 |
| Base 구조 변경으로 Variant override가 무효화 | 중 | 안정된 이름·계층을 유지하고 재생성 보존 테스트 수행 |
| Unity Licensing IPC가 샌드박스에서 차단 | 중 | 외부 실행 승인을 받아 exact Editor로 재검증 |
| Git Bash 설치 경로 차이 | 저 | 표준 설치 경로 두 곳을 순서대로 탐색하고 명확히 실패 |

## §후속 (Follow-up)

- 게임 콘셉트 확정 후 선택 패키지와 UI 화면 목록을 별도 P## 계획으로 정리한다.
- 실제 첫 화면 구현 시 본 계획의 Base/Variant 계약을 재사용한다.
- 모든 구현·검증 항목은 완료됐으며, PR 머지 후 suffix 규칙에 따라 이 문서를 `_done`으로 이관한다.
