# P02 — 인구층 소비·정수 인구 제한·해금 기반 UI

> **Status**: implemented in branch; Unity CLI verification passed on 2026-06-25
> **Branch**: `P02/implement`
> **GitHub umbrella issue**: [#22 P02: 인구층 소비·정수 인구 제한·해금 기반 UI](https://github.com/Klavers/Civic/issues/22)
> **Parent context**: [P01 핵심 경제/자원 UI 세로 슬라이스](./done/P01_Core_Economy_And_Resource_UI_Umbrella_done.md)
> **Related issues**: [#17](https://github.com/Klavers/Civic/issues/17), [#18](https://github.com/Klavers/Civic/issues/18), [#20](https://github.com/Klavers/Civic/issues/20), [#19](https://github.com/Klavers/Civic/issues/19)

## §0 Context

P01은 기본 경제 루프, 자원/건물/기술 CSV, 프리팹 기반 `CivicHud`를 구현했다. P02는 P01 위에서 다음 문제를 함께 처리한다.

- [#17](https://github.com/Klavers/Civic/issues/17): 인구 생산 건물이 자원을 소비하지 않고 인구를 생산한다.
- [#18](https://github.com/Klavers/Civic/issues/18): 표시상 사용 인구가 인구 한도와 같은 상태에서도 인구를 소비하는 건물이 한 번 더 건설될 수 있다.
- [#20](https://github.com/Klavers/Civic/issues/20): 미연구 자원/건물이 UI에 표시된다.
- [#19](https://github.com/Klavers/Civic/issues/19): 신규 자원 목록, 식량 환산비, 인구층 소비 여부의 데이터 근거를 제공한다.

P02는 `P02/implement` 단일 브랜치에서 계획 작성, 원인분석 기록, 데이터/시뮬레이션/UI 구현, 테스트 갱신까지 수행한다.

## §1 목표 (Goals)

1. **인구층 소비 데이터 추가** — `resources.csv`에 `isPopulationConsumption`, `requiredTechnologyId`를 추가한다. 기본 인구층 소비 자원은 시작 시 해금된 `isPopulationConsumption = true` 자원으로 판정한다.
2. **해금 기반 데이터·UI 구현** — 자원과 건물은 기술 해금 상태에 따라 런타임 snapshot과 HUD 표시 후보에서 제외한다.
3. **시대 탭 기술 UI 구현** — 기술 패널에 현재 시대 이하의 시대 탭만 표시하고, 선택된 시대의 기술 목록을 보여준다.
4. **정수 인구 제한 수정** — 인구 보유량, 표시값, 건설 가능 판정을 정수 기준으로 통일하고 `현재 사용 인구 + 신규 건물 사용 인구 <= 인구`로 판정한다.
5. **1초 tick 정산 도입** — `Advance(seconds)`는 내부 누적기를 사용하고 1초 이상 누적될 때 whole tick 단위로 정산한다.
6. **인구 생산 건물의 소비 기반 보너스 구현** — 인구 생산 건물의 기본 인구는 건물 수 기반 현재값으로 계산하고, 해금된 인구층 소비 자원의 현재 수요 충족분만큼 활성 인구 보너스를 부여한다. 이 보너스는 영구 인구를 초당 누적하지 않으며 수요가 미충족되면 취소된다.

## §2 비목표 (Non-goals)

- 기술 강화/대체 효과 시스템 구현: [#23](https://github.com/Klavers/Civic/issues/23)
- 효율 +1, 산출 추가, 투입 추가, 오두막→집 대체, 인구 기반 과학 생산 +1 같은 복합 기술 효과 구현
- 전체 시대의 최종 밸런스 확정
- 아이콘 에셋 기반 투입/산출 UI 전환
- 미구현 #19 자원의 임시 더미 표시

## §3 결정 요약

| 주제 | 결정 |
|-|-|
| 데이터 원본 | `Assets/_Project/Data/Sheets/*.csv` 유지 |
| 자원 해금 | `resources.csv.requiredTechnologyId` 기준 |
| 인구층 소비 여부 | `resources.csv.isPopulationConsumption` 기준 |
| 기본 인구층 소비 자원 | 시작 시 해금되어 있고 `isPopulationConsumption = true`인 자원 |
| 기본가격 | P02에 포함되는 모든 element 자원의 `basePrice = 1` |
| 식량 환산비 | #19 기준. P02 포함 자원 중 밀 1, 고기 2 |
| 기술표 우선순위 | 최신 시대별 기술표가 #19의 필요기술 추정보다 우선 |
| P02 제외 자원 | 최신 기술표에 대응 해금 기술이 없는 #19 자원 |
| 시대 데이터 | 원시/고대/고전/중세/르네상스/산업 6개 시대 추가 |
| 기술 비용 | 원시 3, 고대 5, 고전 8 더미값 |
| 기술 효과 | 자원 해금, 건물 해금, 시대 해금, 세율 증가만 P02 구현 |
| tick | 모든 건물 투입/산출은 1초 tick 정산 |
| `/s` 표시 | 1초 tick 변화량을 `/s`로 표시 |
| 인구 타입 | 저장/표시/판정 모두 정수 기준 |
| 인구 제한 | 비-인구생산 건물만 `현재 사용 인구 + populationUse <= 정수 인구` 필요 |
| 인구 보너스 | 인구층 소비 자원 수요 충족 상태에 따른 현재 활성 효과. 초당 누적 생산 아님 |
| UI 생성 | 런타임 row 생성 금지. `UiPrefabGenerator`가 충분한 고정 row/tap 풀 생성 |

## §4 CSV 스키마 변경

### `resources.csv`

P02 스키마:

`id,displayNameKo,category,basePrice,foodConversion,isStockpile,isPopulationConsumption,requiredTechnologyId,sortOrder`

- `isPopulationConsumption`: 인구 생산 건물이 활성 인구 보너스 조건으로 요구할 수 있는 자원 여부.
- `requiredTechnologyId`: 비어 있으면 시작 해금, 값이 있으면 해당 기술 연구 후 해금.
- `food` aggregate와 `population`, `science`, `treasury`, `construction_power` 같은 numeric 핵심 자원은 UI에서 계속 표시한다.
- 미해금 element 자원은 우측 자원요약, 식량 펼침, 좌측 자원 상세에서 완전히 숨긴다.

### `buildings.csv`

기존 `unlockedByTechnologyId`를 건물 해금 조건으로 계속 사용한다. 미해금 건물은 건물 패널에서 숨긴다.

### `technologies.csv`

기존 컬럼을 유지한다.

`id,displayNameKo,eraId,cost,unlocksEraId,prerequisiteTechnologyIds,taxRateAdd,sortOrder`

P02에서 실제 작동시키는 효과는 다음 네 가지다.

- 기술 자체 연구 완료
- 연구한 기술의 시대가 현재 시대보다 높을 때 현재 시대 진보
- `taxRateAdd`
- 자원/건물의 해금 조건 충족

`unlocksEraId` 컬럼은 호환 목적으로 유지하지만 런타임 시대 진행에는 사용하지 않는다.

## §5 구현 단계 (Steps)

### Step 1 — 계획과 이슈 기록

- [x] 본 계획 문서를 작성한다.
- [x] P02 umbrella 이슈를 생성한다.
- [x] #17/#18/#20/#19에 결정사항과 P02 처리 범위를 코멘트한다.
- [x] 기술 강화/대체 효과를 별도 follow-up 이슈로 생성한다.

### Step 2 — 데이터 모델과 검증

- [x] `ResourceDefinition`에 `IsPopulationConsumption`, `RequiredTechnologyId`를 추가한다.
- [x] CSV loader가 신규 컬럼을 읽도록 수정한다.
- [x] 데이터 검증에서 `requiredTechnologyId` 참조를 확인한다.
- [x] `eras.csv`를 6개 시대까지 확장한다.
- [x] P02 포함 자원/건물/기술 데이터를 CSV에 반영한다.

### Step 3 — 시뮬레이션

- [x] `Advance(seconds)`에 1초 tick 누적기를 둔다.
- [x] 인구 보유량을 정수로 저장/갱신한다.
- [x] #18 인구 제한을 `현재 사용 인구 + populationUse <= 정수 인구`로 수정한다.
- [x] 인구생산 건물의 기본 population output을 건물 수 기반 현재값으로 계산한다.
- [x] 해금된 인구층 소비 자원별 현재 수요 충족분만큼 활성 인구 보너스를 계산하고, 영구 인구로 누적하지 않는다.
- [x] 해금 상태에 따른 snapshot 표시 후보 필터링을 구현한다.

### Step 4 — UI

- [x] 우측 자원요약, 식량 펼침, 좌측 자원 상세에서 미해금 element 자원을 숨긴다.
- [x] 건물 패널에서 미해금 건물을 숨긴다.
- [x] 기술 패널에 시대 탭 풀을 추가한다.
- [x] 현재 시대 이하의 탭만 표시하고 선택된 시대 기술만 표시한다.
- [x] 런타임 UI row/tap 생성 없이 prefab 고정 풀만 사용한다.

### Step 5 — 검증과 동기화

- [x] EditMode 테스트를 갱신·실행한다.
- [x] PlayMode 테스트를 갱신·실행한다.
- [x] 관련 이슈에 구현 내용과 검증 결과를 코멘트한다.
- [x] 구현·검증 완료 항목만 체크박스에 반영한다.

## §6 검증 계획 (Verification)

### 2026-06-25 검증 결과

이전 P01/uGUI 작업과 동일한 `scripts/Invoke-Unity.ps1` CLI 래퍼 절차로 검증했다. 실행 전 원본 프로젝트의 `Unity.exe` 프로세스와 `Civic/Temp/UnityLockfile`이 없는 상태를 확인했다.

| 항목 | 결과 | 로그 |
|-|-|-|
| `GenerateUI` | PASS | `Civic/Logs/Codex/GenerateUI-20260625-005545.log` |
| `ValidateData` | PASS | `Civic/Logs/Codex/ValidateData-20260625-005640.log` |
| `ValidateUI` | PASS | `Civic/Logs/Codex/ValidateUI-20260625-005659.log` |
| `TestEditMode` | PASS — 12 passed, 0 failed | `Civic/Logs/Codex/EditMode-20260625-005721.xml` |
| `TestPlayMode` | PASS — 1 passed, 0 failed | `Civic/Logs/Codex/PlayMode-20260625-005745.xml` |
| `dotnet msbuild Civic/Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true` | PASS | console output |

체크박스 반영은 `check-and-verify` 절차상 PATCH 전 사용자 승인이 필요하므로 별도 승인 후 수행한다.

### EditMode

- 신규 CSV 컬럼 파싱과 참조 검증
- #19 기반 인구층 소비 여부, 식량 환산비, 기본가격 1 검증
- 최신 기술표 기준 자원/건물 해금 검증
- 오두막 건설 시 기본 인구 즉시 증가 검증
- 인구층 소비 자원별 정수 수요 충족량과 현재 활성 보너스 검증
- 오두막 100개, 자원 97/112/80 시나리오에서 활성 보너스 277, 미충족 시 보너스 취소, 보충 시 재활성화 검증
- `27 / 27` 상태에서 인구 소모 건물 건설 불가 및 `TryBuild` false 검증
- 미해금 자원/건물이 snapshot/UI 후보에서 제외되는지 검증

### PlayMode

- 건물 패널에서 미해금 건물이 보이지 않는지 확인
- 기술 연구 후 건물/자원이 표시되는지 확인
- 식량 펼침에서 미해금 food child가 숨겨지는지 확인
- 시대 전환 후 현재 이하 시대 탭만 표시되는지 확인
- 1초 tick 이후 HUD 숫자와 정산 로그형 인구 상세가 갱신되는지 확인

### Commands

```powershell
./scripts/Invoke-Unity.ps1 -Action GenerateUI
./scripts/Invoke-Unity.ps1 -Action ValidateData
./scripts/Invoke-Unity.ps1 -Action ValidateUI
MSBuild Civic/Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true
powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action TestEditMode
powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action TestPlayMode
```

## §7 리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| 신규 자원/기술 데이터가 실제 설계표와 다를 수 있음 | 해금/표시 결과가 기획 의도와 어긋남 | 최신 시대별 기술표를 우선하고 #19와 충돌한 항목은 이슈 코멘트에 기록 |
| tick 누적기 도입으로 기존 PlayMode 테스트 타이밍이 바뀜 | 기존 0.25초 기반 검증 실패 | 1초 이상 경과를 기다리는 테스트로 갱신 |
| snapshot 필터링이 내부 계산까지 누락시킬 수 있음 | 미해금 자원이 계산에서 사라지는 회귀 | 계산용 `Data.Resources`와 표시용 snapshot 후보를 분리 |
| 시대 탭 UI가 프리팹 row 풀을 초과할 수 있음 | 기술 표시 누락 | generator가 데이터 기반 충분한 row/tap 풀을 생성 |
| 기술 강화/대체 효과를 P02에 섞을 위험 | 범위 확대와 구조 결함 | follow-up 이슈로 분리하고 P02에는 해금/세율/시대만 구현 |

## §8 Follow-up

- 기술 강화/대체 효과 시스템 이슈: 효율 +1, 산출 추가, 투입 추가, 오두막→집 대체, 인구 기반 과학 생산 +1을 별도 설계/구현한다.
- 아이콘 에셋 기반 투입/산출 UI 전환 이슈는 기존 UI 논의에 이어 별도 처리한다.
- #19 전체 자원 중 P02 제외 자원은 대응 기술/건물/효과가 확정된 뒤 추가한다.

## §9 추가 결정: 초기 연구 기술과 시대 진행 규칙 (2026-06-25)

### 원인 분석

- `initial_state.csv`가 `resource`와 `building`만 표현해 시작 연구 완료 기술을 데이터로 지정할 수 없었다.
- `TryResearch()`가 `technology.unlocksEraId`를 즉시 `CurrentEraId`에 반영해, 사용자가 요청한 “현재 시대 기술 절반 이상 연구 → 다음 시대 탭 표시 → 다음 시대 기술 1개 연구 시 시대 진보” 규칙을 표현하지 못했다.
- `CivicHudController.ResearchRequestedTechnology()`가 연구 성공 후 선택 탭을 `simulation.Snapshot.CurrentEraId`로 덮어써, 이전 시대 기술을 연구할 때 사용자가 보고 있던 탭이 현재 시대 탭으로 자동 변경됐다.

### 결정사항

- 게임 시작 시대는 `eras.csv`의 최저 `order` 시대이며, 현재 데이터에서는 `primitive`가 `order = 0`인 원시시대다.
- 시작 연구 완료 기술은 `initial_state.csv`의 `kind = technology` 행으로 지정한다.
- 시작 연구 완료 기술은 `primitive_agriculture`, `primitive_architecture`, `wood_processing`이다.
- `initial_state.csv`의 technology 행은 `amount = 1`만 허용하고, 데이터 검증에서 기술 ID 참조를 확인한다.
- `unlocksEraId` 컬럼은 호환 목적으로 유지하지만 P02 런타임 시대 진행에는 사용하지 않는다. 기존 `primitive_agriculture`, `calendar`의 `unlocksEraId` 값은 비웠다.
- 현재 시대 이하의 기술 탭은 항상 표시한다.
- 다음 시대 탭은 현재 시대 기술의 절반 이상이 연구되면 표시한다. 절반 이상은 `researched * 2 >= total`로 판정한다.
- 다음 시대 기술을 1개 이상 연구하면 현재 시대가 해당 기술의 시대로 진보한다.
- 이전 시대 기술을 나중에 연구해도 현재 시대는 퇴보하지 않는다.
- 기술 연구 후 HUD의 선택 시대 탭은 유지한다. 선택 탭이 더 이상 표시 불가능한 경우에만 기존 fallback으로 현재 시대 또는 첫 표시 가능 탭을 선택한다.

### 구현 내용

- `CivicInitialState`에 시작 연구 기술 목록을 추가했다.
- `CivicGameState`가 초기화 시 `data.InitialState.Technologies`를 `ResearchedTechnologyIds`에 반영하도록 수정했다.
- `CanResearch()`와 시대 탭 snapshot 표시 기준을 `IsEraVisible()`로 통일했다.
- `TryResearch()`는 더 이상 `unlocksEraId`를 적용하지 않고, 연구한 기술의 시대가 현재 시대보다 높을 때만 `CurrentEraId`를 올린다.
- `CivicHudController`는 연구 후 선택 탭을 강제로 현재 시대로 바꾸지 않는다.
- 테스트 편의를 위해 `CivicHudController.SelectedTechnologyEraId` 읽기 전용 속성을 추가했다.

### 검증 결과

- `dotnet msbuild Civic\Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true`: PASS
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action ValidateData`: PASS (`Civic/Logs/Codex/ValidateData-20260625-093242.log`)
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action TestEditMode`: PASS, 14 passed / 0 failed (`Civic/Logs/Codex/EditMode-20260625-093309.xml`)
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action TestPlayMode`: PASS, 1 passed / 0 failed (`Civic/Logs/Codex/PlayMode-20260625-093341.xml`)
- UI 프리팹 직렬화 필드 변경은 없어 `GenerateUI`와 `ValidateUI`는 생략했다.

## §10 추가 결정: 인구 생산 건물 보너스는 누적 생산이 아닌 현재 활성 효과 (2026-06-25)

### 원인 분석

P02 최초 구현은 “인구 생산 건물이 매 1초 tick마다 인구층 소비 자원을 소비하고, 소비량만큼 `population` 생산량을 더한다”는 방식이었다. 이 구조에서는 `produced[population]`이 일반 자원 생산량처럼 `State.Resources["population"]`에 누적되어, 오두막 같은 인구 생산 건물이 시간이 지날수록 영구 인구를 계속 증가시키는 문제가 있었다.

사용자 의도는 이와 달랐다. 인구층 소비 자원은 “초당 인구를 새로 생산하는 재료”가 아니라, 인구 생산 건물의 보너스 효과를 켜는 현재 수요 조건이다. 예를 들어 오두막이 밀·나무·공구를 요구한다면, 각 수요가 충족되는 동안 해당 보너스 인구가 활성화되고, 이후 공구 수요가 미충족되면 공구에 해당하는 보너스만 취소되어야 한다. 공구가 다시 충족되면 보너스도 다시 활성화된다.

### 결정사항

- `population`은 일반 stockpile 누적 자원이 아니라 현재 유효 인구 캐시로 취급한다.
- 초기 인구는 `CivicGameState.BasePopulation`에 보관한다.
- 인구 생산 건물의 기본 인구는 `건물 수 × population output`으로 매 snapshot/tick마다 재계산한다.
- 인구층 소비 자원에 의한 인구 증가는 영구 누적 생산이 아니라 현재 활성 보너스다.
- 활성 보너스는 현재 해금된 인구층 소비 자원의 수요 충족량으로 계산한다.
- 수요가 충족되지 않는 자원의 보너스는 즉시 빠지고, 다시 충족되면 다시 반영된다.
- 과학 생산은 저장된 과거 인구가 아니라 현재 유효 인구를 기준으로 계산한다.
- 자원 상세 UI의 인구 항목은 기본 인구/건물 인구와 활성 인구 보너스를 분리해 보여준다. 활성 보너스 항목에는 `/s` 또는 `/틱` 누적 표현을 사용하지 않는다.

### 구현 내용

- `CivicGameState`에 `BasePopulation`을 추가하고, `initial_state.csv`의 `resource,population` 값을 이 기준값으로 읽는다.
- `TryBuild()`에서 인구 생산 건물의 `population` output을 `AddResource()`로 즉시 누적하지 않도록 수정했다.
- `CalculateEffectivePopulation()`을 추가해 `BasePopulation + 인구 생산 건물 기본 인구 + 현재 활성 인구 보너스`를 정수 인구로 계산한다.
- `CalculateRates()`에서 `produced[population]` 누적을 제거하고, `CivicRateSet.EffectivePopulation`을 통해 현재 유효 인구를 전달한다.
- `ApplyRates()`는 `population`을 일반 생산/소비 delta로 갱신하지 않고 현재 유효 인구로 덮어쓴다.
- `CalculateSnapshot()`은 tick 적용 후 현재 상태 기준으로 다시 계산해 stockpile과 활성 보너스 표시가 엇갈리지 않게 했다.
- `CivicHudView`의 인구 상세 문구를 “수요 충족 → 활성 인구 +N”으로 변경하고, 기본 인구 계산에서 활성 보너스를 중복 차감했다.
- EditMode 테스트를 `PopulationConsumptionActivatesCurrentBonusWithoutAccumulating`으로 갱신했다.

### 검증 시나리오

- 오두막 100개, 밀 97, 나무 112, 공구 80, 공구 해금 상태에서 tick 적용 전 현재 snapshot은 기본 인구 203 + 활성 보너스 277 = 인구 480으로 표시된다.
- 1초 tick 후 밀과 공구가 0이 되면 해당 보너스는 취소되고, 남은 나무 12에 해당하는 보너스만 유지되어 인구 215가 된다.
- 밀·나무·공구를 각 100으로 다시 보충하면 세 자원 보너스가 모두 다시 활성화되어 인구 503이 된다.
- 위 변화는 영구 인구 누적이 아니라 현재 수요 충족 상태 변화로만 발생한다.

### 검증 결과

- `dotnet msbuild Civic\Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true`: PASS
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action ValidateData`: PASS (`Civic/Logs/Codex/ValidateData-20260625-095922.log`)
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action TestEditMode`: PASS, 14 passed / 0 failed (`Civic/Logs/Codex/EditMode-20260625-100059.xml`)
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action TestPlayMode`: PASS, 1 passed / 0 failed (`Civic/Logs/Codex/PlayMode-20260625-100124.xml`)
- UI 프리팹 구조와 직렬화 필드 변경은 없어 `GenerateUI`와 `ValidateUI`는 생략했다.
