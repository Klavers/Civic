---
name: unity-workflow
description: Unity Editor 상태를 확인하고 Civic 프로젝트의 UI 생성, 데이터/UI 검증, EditMode/PlayMode 테스트, MSBuild 보조 검증을 안전하게 수행하는 스킬. Unity 작업절차, Invoke-Unity.ps1, GenerateUI, ValidateData, ValidateUI, TestEditMode, TestPlayMode, UnityLockfile, stale lockfile, Unity licensing/headless 오류, 열린 Editor 대체 절차를 다룰 때 사용한다.
---

# unity-workflow

Civic Unity 프로젝트에서 코드·CSV·프리팹 생성기·UI 변경 후 검증 절차를 안전하게 수행한다. 핵심 원칙은 “Editor 상태를 먼저 확인하고, 열린 Editor와 CLI batchmode를 섞지 않는 것”이다.

## 전제

- 저장소 루트에서 실행한다: `Z:\Civic`
- Unity 프로젝트 루트는 `Civic/`이다.
- Unity 버전 정본은 `Civic/ProjectSettings/ProjectVersion.txt`이다.
- CLI는 기존 래퍼만 사용한다.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action <Action>
```

지원 action:

- `Compile`
- `GenerateUI`
- `ValidateData`
- `ValidateUI`
- `TestEditMode`
- `TestPlayMode`

## 1. Editor 상태 확인

CLI 실행 전 항상 다음을 확인한다.

```powershell
Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,StartTime,Path
Test-Path -LiteralPath 'Civic\Temp\UnityLockfile'
```

판정:

- Unity 프로세스가 있으면 원본 프로젝트에 `Invoke-Unity.ps1`을 실행하지 않는다.
- `Civic/Temp/UnityLockfile`이 있으면 원본 프로젝트에 `Invoke-Unity.ps1`을 실행하지 않는다.
- Unity 프로세스가 없고 lockfile도 없을 때만 원본 프로젝트 CLI 검증을 진행한다.

## 2. Editor가 열려 있을 때

Editor가 열려 있으면 다음 절차를 사용자에게 안내하고, CLI를 실행하지 않는다.

- 열린 Editor에서 자동 import/compile을 기다린다.
- UI 생성: `Tools > Civic > UI > Generate`
- 데이터 검증: `Tools > Civic > Data > Validate`
- UI 검증: `Tools > Civic > UI > Validate`
- 테스트: `Window > General > Test Runner`에서 EditMode/PlayMode 실행

Editor 실행 중에는 `.unity`, `.prefab`, `.asset` 직렬화 파일을 외부에서 직접 패치하지 않는다. 씬·프리팹 변경은 Editor 메뉴 또는 생성기를 통해 수행한다.

## 3. stale UnityLockfile 처리

`UnityLockfile`은 다음 조건을 모두 만족할 때만 stale로 본다.

- `Get-Process Unity` 결과가 비어 있다.
- 사용자가 Editor를 정상 종료했거나, 잔류 Unity CLI 프로세스 종료를 승인했다.
- lockfile 경로가 `Civic/Temp/UnityLockfile`로 확인된다.

stale로 확인되면 경로를 다시 확인한 뒤 제거한다.

```powershell
Resolve-Path -LiteralPath 'Civic\Temp\UnityLockfile'
Remove-Item -LiteralPath 'Civic\Temp\UnityLockfile' -Force
```

Unity 프로세스가 남아 있으면 lockfile을 바로 삭제하지 않는다. 먼저 프로세스 시작 시간, 최신 `Civic/Logs/Codex/*.log`, licensing/headless 오류 여부를 확인한다. 잔류 CLI 프로세스 종료가 필요하면 사용자 승인 또는 명시 지시를 받은 뒤 `Stop-Process`를 사용한다.

## 4. 표준 CLI 검증 순서

Editor가 닫혀 있고 lockfile이 없으면 다음 순서로 실행한다.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action GenerateUI
powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action ValidateData
powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action ValidateUI
powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action TestEditMode
powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action TestPlayMode
dotnet msbuild Civic\Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true
```

일반적으로 `MSBuild`는 Unity import/package registration이 정상화된 뒤 보조 검증으로 실행한다. Unity 패키지 어셈블리 생성 전에는 `UnityEngine.UI` 참조가 깨져 실패할 수 있다.

## 5. 성공 판정

각 로그는 `Civic/Logs/Codex/` 아래에 생성된다. 성공 여부는 exit code와 로그 마커를 함께 본다.

| Action | 성공 마커 |
|-|-|
| `GenerateUI` | `UNITY_ACTION_OK=GenerateUI`, `CIVIC_UI_GENERATION_OK` |
| `ValidateData` | `UNITY_ACTION_OK=ValidateData`, `CIVIC_DATA_VALIDATION_OK` |
| `ValidateUI` | `UNITY_ACTION_OK=ValidateUI`, `CIVIC_UI_VALIDATION_OK` |
| `TestEditMode` | `UNITY_ACTION_OK=TestEditMode`, XML `failed="0"` |
| `TestPlayMode` | `UNITY_ACTION_OK=TestPlayMode`, XML `failed="0"` |
| `dotnet msbuild` | exit code 0 |

테스트 action은 XML 결과 파일도 확인한다.

```powershell
[xml]$results = Get-Content -Raw -LiteralPath '<EditMode-or-PlayMode-result>.xml'
$results.'test-run'.failed
```

## 6. 실패 시 중단 기준

다음 오류가 나오면 반복 재시도하지 말고 작업을 중단해 사용자에게 보고한다.

- `The connection with the Unity Licensing Client has been lost`
- `Timed-out waiting for channel: "LicenseClient-..."`
- `Error: 'com.unity.editor.headless' was not found`
- `The following packages were not registered because your license doesn't allow it`
- `UnityEngine.UI` 또는 `UnityEditor.UI` 참조 누락이 MSBuild에서 발생

보고할 때는 다음을 구분한다.

- 관찰된 현상: 어떤 command/action이 어떤 로그에서 실패했는가
- 원인 후보: licensing/headless, package registration, lockfile, 열린 Editor 충돌 중 무엇인가
- 현재 상태: Unity 프로세스 존재 여부, `UnityLockfile` 존재 여부
- 안전한 다음 선택지: 열린 Editor 메뉴 검증, Editor 정상 재시작/종료 후 재시도, 잔류 프로세스 종료 승인 등

## 7. 결과 보고 형식

최종 보고에는 최소한 다음을 포함한다.

- 실행한 action 목록과 PASS/FAIL
- 로그 경로
- EditMode/PlayMode total/passed/failed
- Unity 프로세스 및 `UnityLockfile` 최종 상태
- 생성기 실행으로 변경된 주요 에셋 예: `Civic/Assets/_Project/Prefabs/UI/Generated/CivicHud_Base.prefab`

체크박스 갱신이 이어지면 `check-and-verify`를 별도로 사용하고, 검증 완료 항목만 체크한다.

