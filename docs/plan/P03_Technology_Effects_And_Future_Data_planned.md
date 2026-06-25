# P03: 기술 효과·확장 시대 데이터 통합

## §0 Context

- 선행 계획: [P02_Population_Consumption_Integer_Cap_Unlock_UI_planned.md](P02_Population_Consumption_Integer_Cap_Unlock_UI_planned.md)
- 관련 이슈: #19, #23, #27, #28
- 원자료:
  - `docs/시대별_기술목록.csv`
  - `docs/시대별_기술(기술,영향받는건물및생산품,기술해금효과)_raw.csv`
  - `docs/기술별_건물(기술,영향받는건물,추가투입자원,추가산출량)_raw.csv`
- 런타임 데이터:
  - `Civic/Assets/_Project/Data/Sheets/resources.csv`
  - `Civic/Assets/_Project/Data/Sheets/buildings.csv`
  - `Civic/Assets/_Project/Data/Sheets/technologies.csv`
  - `Civic/Assets/_Project/Data/Sheets/technology_effects.csv`
  - `Civic/Assets/_Project/Data/Sheets/eras.csv`

P03는 P02에서 보류한 기술 강화/대체 효과와 #19의 미도입 자원 목록을 통합한다. 범위는 원시시대부터 미래시대까지이며, 전기/현대/정보/미래 기술은 원자료의 시대별 기술 효과표를 기준으로 편입한다.

## §1 Goals

1. #19의 미도입 자원을 게임 데이터에 포함한다.
2. 원시~미래시대 전체 시대·기술 목록을 `eras.csv`, `technologies.csv`에 반영한다.
3. 건물 최초 해금과 기술 효과를 분리해 `technology_effects.csv`를 추가한다.
4. 세율 효과의 단일 소스를 `technology_effects.csv`로 옮긴다.
5. 연구 완료 기술의 산출 추가, 조건부 투입 산출, 세율 증가를 시뮬레이션에 반영한다.
6. 기술 패널, 건물 패널, 자원 상세에 기술 효과를 표시한다.
7. 그룹 대상 효과는 계산하지 않고 후속 구현 예정으로 표시하며 별도 이슈로 추적한다.

## §2 Non-goals

- P03에서는 `공장 계열`, `모든 광산` 같은 그룹 대상 효과를 런타임 계산하지 않는다.
- 저장 데이터 마이그레이션은 수행하지 않는다.
- 기술 선행 조건(`prerequisiteTechnologyIds`)은 비워 둔다.
- 밸런스 최종 조정은 수행하지 않는다. 신규 건물 기본값은 임시 규칙을 따른다.
- 프리팹 수동 편집본을 덮어쓰지 않는다. 필요한 경우 AI 관리 Base Prefab만 생성기로 갱신한다.

## §3 결정 요약

### 데이터·명칭

- `오두막`은 내부 ID까지 `house`로 변경하고 표시명은 `집`으로 한다.
- `stone=석재`, `clothing=의복`, `science=과학`을 유지한다.
- `food=식량 aggregate`, `groceries=식료품 element`로 둔다.
- `유황`, `폭약`을 정본 자원명으로 사용한다.
- `털 손질소`, `관개 농장` 같은 별도 건물은 만들지 않고 `목축장`, `밀농장` 기술 효과로 흡수한다.
- 광산 표기는 `구리 광산`, `철 광산`, `금 광산`, `석탄 광산`, `유황 광산`, `우라늄 광산`으로 정규화한다.
- 공방/공장 계열 표기는 `의복 공방`, `종이 공방`, `약품 공방`, `유리 공방`, `가구 공방`, `기호품 공장`, `식료품 공장`, `방직 공장`으로 정규화한다.

### 시대·비용

- 시대는 원시/고대/고전/중세/르네상스/산업/전기/현대/정보/미래 10개로 확장한다.
- 기술 비용은 시대 순서별 `3, 5, 8, 13, 21, 34, 55, 89, 144, 233`을 사용한다.
- `prerequisiteTechnologyIds`는 비워 둔다.

### 기술 효과

- `technology_effects.csv`를 추가하고 `CivicGameDataSource.asset`에 연결한다.
- 효과 타입은 `outputAdd`, `conditionalOutputAdd`, `taxRateAdd`, `plannedFollowUp`을 사용한다.
- `효율 +N`/`추가산출량 N`은 건물 1개당 `+N/s` 산출 추가로 해석한다.
- `OO 투입 시` 효과는 건물 1개당 해당 투입 자원 `1/s` 수요를 추가하고, 해당 자원의 공급률에 비례해 보너스 산출을 만든다.
- `/`로 묶인 투입은 각각 독립 효과다. 예: 철/석탄/유리 각각 충족 시 기계부품 `+1/s`, 모두 충족 시 총 `+3/s`.
- 그룹 대상 효과는 `plannedFollowUp`으로 두고 기술 UI에 “후속 구현 예정”을 표시한다.

## §4 Steps

- [ ] 원자료 CSV 3개를 P03 결정사항에 맞게 정정하고 Git 추적 대상에 포함한다.
- [ ] `resources.csv`에 #19 미도입 자원과 해금 기술을 반영한다.
- [ ] `eras.csv`를 10개 시대까지 확장한다.
- [ ] `technologies.csv`를 원시~미래 전체 기술 목록으로 확장한다.
- [ ] `buildings.csv`의 명칭, `house` ID, 신규 생산 건물을 정규화한다.
- [ ] `technology_effects.csv`를 추가하고 `CivicGameDataSource.asset`에 연결한다.
- [ ] CSV parser/data model/validator가 `technology_effects.csv`를 로드·검증하도록 수정한다.
- [ ] 시뮬레이션에 `outputAdd`, `conditionalOutputAdd`, `taxRateAdd`, `plannedFollowUp` 처리를 반영한다.
- [ ] 기술 패널에 효과 요약을 표시한다.
- [ ] 건물 패널에 적용 후 투입/산출과 GDP 변화를 표시한다.
- [ ] 자원 상세에 기술 효과로 생기는 생산/소비 흐름을 원인별로 표시한다.
- [ ] P03 umbrella 이슈를 생성한다.
- [ ] #19/#23에 P03 결정사항과 반영/제외 범위를 코멘트한다.
- [ ] 그룹 대상 효과 follow-up 이슈를 생성한다.
- [ ] Unity 데이터/UI/테스트 검증을 통과한 항목만 체크박스에 반영한다.

## §5 Verification

- `ValidateData`
  - 신규 `technology_effects.csv` 파싱
  - 참조 무결성
  - 양수 수치
  - `effectType` 검증
  - `technology_effects.csv` 세율 단일 소스 검증
- EditMode
  - #19 미도입 자원 전체 포함
  - 기본 산출 추가 효과 생산량/GDP/자원 상세 반영
  - 조건부 투입 효과 수요·소비·보너스 산출 반영
  - `/` 다중 투입의 독립 효과 해석
  - 그룹효과 보류 기술의 연구/표시 가능성과 계산 미적용
- PlayMode
  - 기술 패널 효과 요약 표시
  - 연구 후 건물 패널 투입/산출 갱신
  - 자원 상세 생산/소비 흐름 갱신
- 보조 검증
  - `dotnet msbuild Civic/Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true`
  - Editor가 닫혀 있고 `Civic/Temp/UnityLockfile`이 없을 때만 `scripts/Invoke-Unity.ps1` 사용

## §6 Risks

| 리스크 | 영향 | 완화 |
|-|-|-|
| 기술 효과와 건물 기본 recipe가 같은 효과를 중복 표현 | 생산량/GDP 과대 계산 | 건물 최초해금은 건물 recipe에, 강화 효과는 `technology_effects.csv`에 둔다. |
| 그룹 대상 효과를 임의로 펼쳐 계산 | 공장/광산 분류 기준이 불명확한 상태에서 잘못된 밸런스 발생 | P03에서는 `plannedFollowUp`으로만 표시하고 별도 이슈에서 grill한다. |
| 기술 패널 효과 요약이 기존 row 높이에 맞지 않음 | UI 겹침 | 생성기의 기술 row 높이를 늘리고 Generate/Validate로 확인한다. |
| `house` ID 변경으로 과거 저장 데이터와 충돌 | 기존 세이브 로드 실패 가능 | 저장 데이터 마이그레이션은 현재 범위 밖으로 명시하고 후속 필요 시 별도 이슈화한다. |
| 원자료 CSV와 런타임 CSV가 재차 불일치 | 추후 데이터 수정 근거 상실 | 원자료 CSV도 P03 결정사항에 맞게 정정하고 이슈 코멘트에 기준을 남긴다. |

## §7 Follow-up

- 그룹 대상 기술 효과 구현 이슈
  - 대상: `공장 계열`, `모든 광산`
  - 필요 결정: 그룹 정의 방식, groupId 컬럼 소유 위치, UI 표시 방식, 기존 효과와의 중복 방지
- 기술 효과 밸런싱 이슈
  - 현재 비용과 산출량은 구조 검증용 기본값이다.
  - 전체 시대 진행 속도와 자원 체인 체감은 별도 밸런스 조정이 필요하다.
- 저장 데이터 마이그레이션 이슈
  - `hut` 저장값이 존재하는 시점이 되면 `house`로 이전하는 규칙이 필요하다.

## §8 구현·검증 기록 (2026-06-25)

### 구현 내용

- P03 umbrella 이슈 #28을 생성했다.
- 그룹 대상 기술 효과 follow-up 이슈 #27을 생성했다.
- #19/#23에 P03 결정사항, 반영 범위, 제외 범위, 후속 이슈 링크를 코멘트했다.
- 원자료 CSV 3개를 P03 결정에 맞게 정정했다.
  - `시대별_기술목록.csv`는 원시~미래 10개 시대까지 확장하고, PowerShell CSV 파서가 검증할 수 있도록 `Tech01~Tech12` 헤더를 사용한다.
  - `기술별_건물(... )_raw.csv`는 `건물 최초해금` 값을 전 행 `O/X`로 채웠다.
  - 시대별 기술 효과 raw CSV는 `집`, `식료품 공장`, `방직 공장`, 광산 표기 등 P03 정규 명칭으로 교정했다.
- `resources.csv`에 #19 미도입 자원을 포함했다.
- `eras.csv`를 원시/고대/고전/중세/르네상스/산업/전기/현대/정보/미래 10개 시대까지 확장했다.
- `technologies.csv`를 원시~미래 전체 기술로 확장하고, 기술 비용을 시대별 결정값으로 채웠다.
- `buildings.csv`에서 `hut`을 `house`로 바꾸고 표시명을 `집`으로 정규화했다.
- `technology_effects.csv`를 추가하고 `CivicGameDataSource.asset`에 연결했다.
- `CivicGameDataLoader`에 기술 효과 파싱·참조·양수값·세율 단일 소스 검증을 추가했다.
- `CivicGameSimulation`에 `outputAdd`, `conditionalOutputAdd`, `taxRateAdd`, `plannedFollowUp` 처리를 추가했다.
- 주거 건물은 일반 tick 건물에서는 계속 제외하되, `house` 대상 기술 효과는 별도 기술 효과 루프로 계산하도록 보정했다.
- 기술 효과로 발생한 생산/소비 흐름을 자원 상세에 원인별로 표시한다.
- 건물 패널의 투입/산출 델타와 GDP 변화가 활성 기술 효과를 포함하도록 했다.
- 기술 패널에 기술 효과 요약을 표시하고, UI 생성기의 기술 row 높이를 2줄 요약 기준으로 늘렸다.

### 검증 결과

- `dotnet msbuild Civic\Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true`: PASS
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action GenerateUI`: PASS
  - 최종 로그: `Civic/Logs/Codex/GenerateUI-20260625-174026.log`
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action ValidateData`: PASS
  - 로그: `Civic/Logs/Codex/ValidateData-20260625-173545.log`
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action ValidateUI`: PASS
  - 최종 로그: `Civic/Logs/Codex/ValidateUI-20260625-174046.log`
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action TestEditMode`: PASS
  - 최종 결과: `Civic/Logs/Codex/EditMode-20260625-173842.xml`
  - 19 passed / 0 failed
- `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Unity.ps1 -Action TestPlayMode`: PASS
  - 최종 결과: `Civic/Logs/Codex/PlayMode-20260625-174106.xml`
  - 1 passed / 0 failed

### 확인된 한계

- #27의 그룹 대상 효과는 P03에서 계산하지 않고 `plannedFollowUp`으로 표시한다.
- `hut → house` 저장 데이터 마이그레이션은 아직 구현하지 않는다.
- 체크박스 본문 갱신은 `check-and-verify` 승인 절차가 필요하지만, 현재 Default 모드에서 사용자 승인 도구가 비활성이라 수행하지 않았다.
