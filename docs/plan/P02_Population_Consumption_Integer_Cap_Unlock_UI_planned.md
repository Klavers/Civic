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
6. **인구 생산 건물의 소비 기반 보너스 구현** — 인구 생산 건물은 건설 시 기본 인구를 즉시 지급하고, 매 1초 tick마다 해금된 인구층 소비 자원을 자원별로 최대 `건물 수 × 1`만큼 소비해 실제 소비량만큼 추가 인구를 생산한다.

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
| UI 생성 | 런타임 row 생성 금지. `UiPrefabGenerator`가 충분한 고정 row/tap 풀 생성 |

## §4 CSV 스키마 변경

### `resources.csv`

P02 스키마:

`id,displayNameKo,category,basePrice,foodConversion,isStockpile,isPopulationConsumption,requiredTechnologyId,sortOrder`

- `isPopulationConsumption`: 인구 생산 건물이 1초 tick에서 소비할 수 있는 자원 여부.
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
- `unlocksEraId`에 따른 시대 전환
- `taxRateAdd`
- 자원/건물의 해금 조건 충족

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
- [x] 인구생산 건물 건설 시 기본 population output을 즉시 지급한다.
- [x] 1초 tick에서 해금된 인구층 소비 자원별 실제 정수 소비량만큼 추가 인구를 생산한다.
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
- 1초 tick에서 인구층 소비 자원별 정수 소비와 추가 인구 생산 검증
- 오두막 100개, 자원 97/112/80 시나리오에서 추가 인구 277 검증
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
