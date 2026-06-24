# P01 — 핵심 경제/자원 UI 세로 슬라이스 (Umbrella)

> **Status**: done
> **Branch**: `P01/implement`
> **GitHub umbrella issue**: [#12 P01: 핵심 경제/자원 UI 세로 슬라이스 umbrella](https://github.com/Klavers/Civic/issues/12)
> **Parent context**: [#2 기본 게임 기획서 추가](https://github.com/Klavers/Civic/issues/2), [`Civic/GAME_DESIGN.md`](../../../Civic/GAME_DESIGN.md)
> **Related implementation issues**: [#3](https://github.com/Klavers/Civic/issues/3), [#4](https://github.com/Klavers/Civic/issues/4), [#5](https://github.com/Klavers/Civic/issues/5), [#6](https://github.com/Klavers/Civic/issues/6), [#7](https://github.com/Klavers/Civic/issues/7), [#8](https://github.com/Klavers/Civic/issues/8), [#9](https://github.com/Klavers/Civic/issues/9), [#10](https://github.com/Klavers/Civic/issues/10), [#11](https://github.com/Klavers/Civic/issues/11)
> **UI workflow dependency**: [P00 Unity 작업환경 및 프리팹 UI 기반](./P00_Unity_Workspace_And_Prefab_UI_done.md)

## §0 Context

`Civic/GAME_DESIGN.md`가 게임 규칙의 정본이다. P01은 해당 기획서의 초기 세로 슬라이스를 Unity 런타임 모델, CSV 데이터, 틱 시뮬레이션, uGUI 프리팹 HUD에 연결한다.

P01은 별도 `P01a` 문서를 만들지 않고 GitHub issue [#3](https://github.com/Klavers/Civic/issues/3)~[#11](https://github.com/Klavers/Civic/issues/11)을 하위 구현 단위로 사용한다. 구현은 `P01/implement` 단일 브랜치와 단일 최종 PR로 제출하되, 내부 커밋·테스트·체크박스는 이슈 단위로 추적한다.

P00에서 확정한 프리팹 UI 규칙은 유지한다. 런타임은 저장된 프리팹 참조와 데이터 바인딩만 수행하고, UI 계층을 런타임에서 생성하지 않는다.

## §1 목표 (Goals)

1. **핵심 틱 루프 구현** — 초기 인구/식량/건설력, 수도와 기본 생산건물, 과학 생산, GDP 계산 기반을 한 틱 루프에서 동작시킨다. 대상: [#3](https://github.com/Klavers/Civic/issues/3)
2. **CSV 데이터 구조 확정** — 시대, 기술, 건물, 자원, 초기 상태를 카테고리별 CSV 시트로 분리하고 `CivicGameDataSource.asset`을 런타임 진입점으로 둔다. 대상: [#4](https://github.com/Klavers/Civic/issues/4)
3. **식량 통산값 구현** — 하위 식량계 자원의 보유량/증가량을 환산비로 합산하고, 식량 자체는 가격/GDP 대상에서 제외한다. 대상: [#5](https://github.com/Klavers/Civic/issues/5)
4. **국고/건설력/건설부문 구현** — GDP 기반 국고 증가, 건설자재와 국고 소비, 부족분 비례 가동, 음수 국고 방지를 구현한다. 대상: [#6](https://github.com/Klavers/Civic/issues/6)
5. **생산 효율과 품귀 구현** — 소비 우선순위 없이 자원별 공급률과 건물 효율을 2-pass로 계산한다. 대상: [#7](https://github.com/Klavers/Civic/issues/7)
6. **기술 연구와 시대 진입 구현** — 과학을 소비해 기술을 개방하고 다음 시대 기술 개방 시 시대를 전환한다. 대상: [#8](https://github.com/Klavers/Civic/issues/8)
7. **즉시 건설과 인구 제한 구현** — 건설력만으로 즉시 건설하고 전체 건물 수가 인구 이상이면 추가 건설을 막는다. 대상: [#9](https://github.com/Klavers/Civic/issues/9)
8. **변동 가격과 GDP 구현** — `Civic/GAME_DESIGN.md`의 가격 보간 규칙과 GDP 포함/제외 규칙을 코드로 검증 가능하게 만든다. 대상: [#10](https://github.com/Klavers/Civic/issues/10)
9. **Victoria 3 유사 HUD 구현** — 좌상단 요약 바, 좌측 상세 패널, 우측 고정 자원 요약 패널, 상단 알림을 프리팹 UI로 구현한다. 대상: [#11](https://github.com/Klavers/Civic/issues/11)

## §2 비목표 (Non-goals)

- 전체 시대/기술/건물 콘텐츠의 최종 밸런스 확정
- 저장/불러오기, 오프라인 진행, 장기 메타 진행
- 모바일/다중 해상도 UI polish, 아트 리소스 완성
- 플레이어 튜토리얼, 복합 알림 시스템, 접근성 옵션
- P01 범위를 넘어서는 전투, 외교, 지도, 국가 선택 기능

## §3 결정 요약

| 주제 | 결정 |
|-|-|
| PR 운영 | `P01/implement` 단일 브랜치, 단일 최종 PR |
| 데이터 원본 | `Assets/_Project/Data/Sheets/*.csv` |
| 런타임 데이터 진입점 | `CivicGameDataSource.asset`이 CSV TextAsset들을 참조 |
| CSV 파싱 | 런타임 직접 파싱, 헤더 필수, 따옴표 인용/이스케이프 지원 |
| ID 정책 | ASCII stable ID + `displayNameKo` |
| 수치 타입 | 로컬 `CivicNumber` |
| 큰 수 CSV 표기 | `1000`, `1.5e6` 같은 단일 문자열 |
| 시뮬레이션 | Unity 비의존 순수 코어 + `Advance(seconds)` |
| 계산 순서 | 2-pass 계산 |
| 재고 정책 | 공급률 계산에 재고 버퍼 사용, 소비 후 0 하한 |
| 국고 순서 | tick 내 수입 후 지출 |
| 수도 기본 생산 | 밀, 목재, 국고, 건설력을 각각 +1/s 생산 |
| UI | `IdlePanel`/dummy UI를 `CivicHud`로 대체 |
| 우측 자원 패널 | Victoria 3 우측 요약 항목처럼 고정 표시 |
| 좌측 패널 | 자원/건물/기술 상세 분석과 액션 |

## §4 CSV 스키마

CSV 파일은 `Civic/Assets/_Project/Data/Sheets/`에 둔다.

### `resources.csv`

`id,displayNameKo,category,basePrice,foodConversion,isStockpile,sortOrder`

- `category`: `element`, `aggregate`, `numeric`
- 인구, 과학, 국고, 건설력, 식량 통산값도 통합 자원 정의로 관리한다.
- 식량 통산값은 `aggregate`, 밀 같은 하위 식량 자원은 `element`로 둔다.

### `buildings.csv`

기본 컬럼:

`id,displayNameKo,eraId,role,isBuildable,constructionCost,treasuryCost,populationUse,unlockedByTechnologyId,sortOrder`

입출력 반복 컬럼:

- `input1Id,input1Amount` … `input5Id,input5Amount`
- `output1Id,output1Amount` … `output5Id,output5Amount`

`role`: `capital`, `production`, `construction`, `housing`

### `technologies.csv`

`id,displayNameKo,eraId,cost,unlocksEraId,prerequisiteTechnologyIds,taxRateAdd,sortOrder`

- 선행기술은 세미콜론 구분 ID 목록으로 둔다.
- P01의 기술 효과는 `taxRateAdd`만 구현한다.

### `eras.csv`

`id,displayNameKo,order`

시작 시대는 `order`가 가장 낮은 시대다.

### `initial_state.csv`

`kind,id,amount`

- `kind`: `resource`, `building`
- 초기 식량 5는 `resource,wheat,5`와 `foodConversion = 1`로 표현한다.
- 초기 인구 3, 건설력 5, 수도 1도 이 파일에서 읽는다.

## §5 적용 대상 인벤토리

| Issue | 범위 | 선행 관계 | 주요 산출물 |
|-|-|-|-|
| [#3](https://github.com/Klavers/Civic/issues/3) | 핵심 자원/생산 시뮬레이션 루프 | 없음 | 런타임 시뮬레이션 루프, 초기 상태, 틱 결과 |
| [#4](https://github.com/Klavers/Civic/issues/4) | 시대별 기술/건물 데이터 구조 | 없음 | CSV 파서, 데이터 소스, 검증 메뉴 |
| [#5](https://github.com/Klavers/Civic/issues/5) | 식량 통산값과 하위 식량 UI | [#3](https://github.com/Klavers/Civic/issues/3) | 식량 집계 계산, 하위 항목 UI 바인딩 |
| [#6](https://github.com/Klavers/Civic/issues/6) | 건설력/국고/건설부문 운영 | [#3](https://github.com/Klavers/Civic/issues/3) | 국고/건설력 계산, 부족분 비례 가동 |
| [#7](https://github.com/Klavers/Civic/issues/7) | 생산 효율과 품귀 | [#3](https://github.com/Klavers/Civic/issues/3) | 공급률, 효율, 품귀 상태 |
| [#8](https://github.com/Klavers/Civic/issues/8) | 기술 연구와 시대 진입 | [#3](https://github.com/Klavers/Civic/issues/3), [#4](https://github.com/Klavers/Civic/issues/4) | 기술 개방 액션, 시대 전환 |
| [#9](https://github.com/Klavers/Civic/issues/9) | 건물 수/인구 제한과 즉시 건설 | [#3](https://github.com/Klavers/Civic/issues/3), [#6](https://github.com/Klavers/Civic/issues/6) | 건설 액션, 건물 수 제한 |
| [#10](https://github.com/Klavers/Civic/issues/10) | 수요/공급 기반 변동 가격과 GDP | [#3](https://github.com/Klavers/Civic/issues/3), [#7](https://github.com/Klavers/Civic/issues/7) | 가격 보간, GDP 계산 |
| [#11](https://github.com/Klavers/Civic/issues/11) | 우측 자원 패널과 품귀/가격 표시 | [#5](https://github.com/Klavers/Civic/issues/5), [#7](https://github.com/Klavers/Civic/issues/7), [#10](https://github.com/Klavers/Civic/issues/10) | uGUI 프리팹 HUD, 자원 요약/상세 UI |

## §6 용어 정리

| 용어 | 의미 | 정본 |
|-|-|-|
| 요소 자원 | 생산/소비/가격/GDP 계산에 직접 참여하는 자원 | `Civic/GAME_DESIGN.md` §4, §7 |
| 식량 통산값 | 하위 식량계 자원 보유량/증가량에 환산비를 적용한 합산 표시값 | `Civic/GAME_DESIGN.md` §4 |
| 국고 | GDP와 수도 기본 생산량을 통해 증가하고 건설부문 운영비에 사용되는 재정 자원 | `Civic/GAME_DESIGN.md` §3 |
| 건설력 | 건물 즉시 건설 비용으로 사용하는 축적 자원 | `Civic/GAME_DESIGN.md` §3 |
| 공급률 | 실제 공급량 / 정상 가동 필요량 | `Civic/GAME_DESIGN.md` §6 |
| 생산 효율 | 건물의 투입 자원 공급률 평균으로 결정되는 실제 가동 비율 | `Civic/GAME_DESIGN.md` §6 |
| 변동 가격 | 공급/수요 비율에 따라 기준가 대비 선형 보간되는 현재 가격 | `Civic/GAME_DESIGN.md` §7 |

## §7 단계 (Steps)

### Step 1 — 이슈 결정사항 기록

- [x] [#3](https://github.com/Klavers/Civic/issues/3)~[#12](https://github.com/Klavers/Civic/issues/12)에 grill 결정사항을 UTF-8 파일 기반 코멘트로 기록한다.
- [x] 코멘트는 모두 `> *This was generated by AI during triage.*`로 시작한다.

### Step 2 — 데이터 기반 구현

- [x] `CivicNumber`를 구현한다.
- [x] CSV parser와 schema 검증을 구현한다.
- [x] `CivicGameDataSource.asset`과 예시 CSV 파일을 추가한다.
- [x] `Tools > Civic > Data > Validate` 메뉴를 추가한다.
- [x] Data Validate 실패 시 Editor 메뉴는 대화상자와 상세 로그를 표시하고 batchmode는 예외/종료코드로 실패한다.

### Step 3 — 시뮬레이션 코어 구현

- [x] `Runtime/Simulation` 계층을 만들고 UI 런타임과 책임을 분리한다.
- [x] 초기 상태 인구 3, 식량 5, 건설력 5, 수도 1개를 CSV에서 로드한다.
- [x] 2-pass tick에서 정상 수요/공급률/효율과 실제 생산/소비/가격/GDP/국고를 계산한다.
- [x] 재고 버퍼와 재고 0 하한을 적용한다.
- [x] 과학 연구, 시대 전환, 즉시 건설, 인구 제한 액션을 구현한다.

### Step 4 — 프리팹 기반 `CivicHud` 구현

- [x] 기존 `IdlePanel`/dummy UI를 `CivicHud`로 대체한다.
- [x] 좌상단 요약 바에 인구, 건물수/인구, GDP, 국고, 건설력, 과학을 표시한다.
- [x] 좌측 사이드바와 자원/건물/기술 상세 패널을 구현한다.
- [x] 건설 패널은 건물별 개별 건설 버튼으로 특정 건물을 선택해 건설한다.
- [x] 기술 패널은 기술별 개별 연구 버튼으로 특정 기술을 선택해 연구한다.
- [x] 우측 고정 자원 요약 패널에 관리 대상 전체 자원의 자원명, 재고, 순증감/s를 표시한다.
- [x] 우측 식량 통산값 아래 하위 식량 자원 펼침을 지원한다.
- [x] 상단 알림 3종(품귀, 연구 가능, 건설 불가)을 표시한다.
- [x] `UiPrefabGenerator`는 Base Prefab을 갱신하고 사용자 Variant는 덮어쓰지 않는다.

### Step 5 — 이슈 체크박스 검증과 문서 동기화

- [x] 각 이슈 acceptance criteria를 구현 단위 테스트, PlayMode 테스트, 수동 검증 결과와 매칭한다.
- [x] 통과한 항목만 `check-and-verify` 절차로 GitHub issue 체크박스에 반영한다.
- [x] 사용자 요청에 따라 P01 구현 PR 생성 시점에 suffix 규칙을 적용해 본 문서를 `docs/plan/done/P01_Core_Economy_And_Resource_UI_Umbrella_done.md`로 이관한다.

## §8 검증 (Verification)

- [x] EditMode: CSV parser, 누락/중복/참조 오류, `CivicNumber` parse/산술/표시를 테스트한다.
- [x] EditMode: 초기 상태 로드, 틱 생산, 과학 증가, 식량 환산, 국고/건설력, 공급률, 가격 보간, GDP 포함/제외 규칙을 테스트한다.
- [x] EditMode: 기술 개방/과학 차감/시대 전환, 즉시 건설/인구 제한을 테스트한다.
- [x] EditMode: `UiPrefabValidator.ValidateAll()`이 새 `CivicHud`의 필수 컴포넌트와 직렬화 참조를 검사한다.
- [x] PlayMode: `SampleScene`에서 저장된 프리팹이 instantiate되고, 틱 진행 후 HUD 숫자가 갱신된다.
- [x] PlayMode: 건물별 건설 버튼, 기술별 연구 버튼, 우측 식량 펼침, 상단 알림이 동작한다.
- [x] 열린 Unity Editor: `Tools > Civic > Data > Validate`, `Tools > Civic > UI > Generate`, `Tools > Civic > UI > Validate`를 실행하고 성공 대화상자를 확인한다.
- [x] 닫힌 Unity Editor: 필요 시 `scripts/Invoke-Unity.ps1 -Action GenerateUI|ValidateUI|TestEditMode|TestPlayMode`로 자동 검증한다.
- [x] GitHub: [#3](https://github.com/Klavers/Civic/issues/3)~[#12](https://github.com/Klavers/Civic/issues/12)의 체크박스는 실제 구현/검증이 끝난 항목만 체크한다.

### 구현 중 추가 결정

- 건물/기술 사이드 패널은 텍스트 목록과 버튼 목록을 분리하지 않고, 항목별 `ActionRow` 안에 정보 텍스트와 해당 액션 버튼을 함께 배치한다.
- `population`을 output으로 생산하는 집 계열 건물은 인구 한도를 늘리는 건물이므로 현재 건물 수가 인구와 같아도 건설 가능하다. 이 예외는 인구 cap 검사에만 적용하고, 건설력과 해금 조건은 그대로 유지한다.
- 자원 상세 패널은 +1 건물 예상 변화치를 표시하지 않고, 자원별 생산처와 생산처별 GDP 기여를 표시한다.
- +1 건물 예상 변화치는 건물 패널로 이동하고, 건물 패널은 `건물명 / 개수 / 건설비용 / 투입·산출 / GDP 변화 / 건설 버튼` 열을 가진 고정 높이 표로 구성한다.
- 투입·산출은 한 줄에 들어가는 항목 수를 제한하고 초과분은 `+N`으로 요약한다. `+N`과 비활성화된 건설 버튼의 상세 내용은 범용 tooltip으로 표시한다.
- 각 좌측 상세 패널은 콘텐츠가 패널보다 커질 수 있으므로 ScrollRect 기반 스크롤 영역을 사용한다.
- 투입·산출 텍스트를 아이콘으로 대체하는 작업은 P01 범위 밖 후속 이슈로 분리한다.

## §9 리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| #3~#11을 한 PR에 모두 담아 diff가 커짐 | 리뷰 비용 증가, 회귀 원인 추적 어려움 | 단일 PR은 유지하되 이슈별 커밋과 검증 로그로 추적한다. |
| CSV 런타임 파싱 오류가 늦게 발견됨 | PlayMode 진입 후 실패 | `Tools > Civic > Data > Validate`와 EditMode 실패 케이스를 추가한다. |
| `CivicNumber`가 P01 범위보다 커짐 | 구현 지연 | P01에는 parse/비교/사칙연산/min/max/표시만 구현한다. |
| UI가 계산 책임을 갖게 됨 | 시뮬레이션 테스트 불가, 표시/계산 불일치 | UI는 snapshot 표시와 버튼 이벤트 전달만 수행한다. |
| 우측 자원 요약과 좌측 자원 상세가 중복됨 | 사용자 혼동 | 우측은 요약 목록, 좌측은 생산/소비 원인 분석과 +1 건물 예상 변화로 역할을 분리한다. |
| 프리팹 Base 재생성으로 사용자 Variant override 손상 | Editor 수동 수정 손실 | P00 Base/Variant 계약을 유지하고 Generate 재실행 테스트를 반복한다. |
| Unity Editor가 열린 상태에서 CLI batchmode를 실행 | 프로젝트 잠금/Asset Database 충돌 | Editor가 열려 있으면 메뉴와 Test Runner를 사용하고, CLI는 Editor 종료 후 사용한다. |

## §10 후속 (Follow-up)

- P01 완료 후 실제 콘텐츠 확장은 별도 P02 계획으로 분리한다.
- 저장/불러오기와 오프라인 진행은 P01 이후 별도 plan에서 다룬다.
- 범용 기술 효과 시스템은 `taxRateAdd` 이후 별도 plan에서 확장한다.

## §11 2026-06-24 추가 결정 — 자원 상세 표시 B안 적용

사용자 검증에서 자원 상세 패널에 생산처만 표시되고, 인구·과학·건설력·국고 운영비·소비처 설명이 빠지는 문제가 확인되었다. 또한 상단 HUD, 우측 자원 요약, 좌측 자원 상세가 서로 다른 convenience 값과 `snapshot.Resources[]` 값을 참조하면 같은 자원의 표시값이 어긋날 수 있다는 우려가 제기되었다.

이번 수정은 B안으로 처리한다.

- UI 표시 기준은 `CivicGameSnapshot.Resources[]`를 우선한다.
- `snapshot.Population`, `snapshot.Treasury`, `snapshot.ConstructionPower`, `snapshot.Science` convenience 값은 호환·계산 보조용으로 유지하되, HUD 표시값은 resource snapshot stockpile을 우선 참조한다.
- 자원 상세 summary는 보유량, 생산/s, 소비/s, 순증감/s, 가격 배율, GDP 기여 또는 GDP 제외 여부를 함께 표시한다.
- 자원 상세 슬롯은 생산처만 표시하지 않고 생산처, 소비처, 특수 표시 흐름을 함께 표시한다.
- 인구 항목은 `수도 x1 | 수도 인구 총량`, `오두막 xN | 오두막 인구 총량`처럼 인구 출처별 총량을 표시한다.
- 과학 항목은 현재 P01 규칙인 인구 기반 과학 생산을 기준으로 `수도 인구 +N/s`, `오두막 인구 +N/s`처럼 인구 출처별 기여를 표시한다.
- 국고 항목은 건설부문 운영비 소비가 일반 input이 아니더라도 자원 상세에서 `건설부문 운영비 -N/s`로 보이게 한다.
- 건설력 항목은 tick 기반 생산처와 별도로, 건설 버튼 클릭 시 건물별 건설비용만큼 즉시 차감된다는 설명을 표시한다.
- 자원 row 수는 하드코딩 6개가 아니라 CSV 데이터의 non-aggregate 자원 수에 맞춰 생성한다.

C안(자원 흐름 표시용 공용 read model/ledger 도입)은 현재 PR에 즉시 포함하지 않는다. 별도 후속 검토 이슈 [#15](https://github.com/Klavers/Civic/issues/15)에 현재 상황, 논의 원인, 도입/비도입 근거, 제안별 수정방안과 시나리오, 권장안, 추가 grill 필요사항을 기록했다.

검증:

- `scripts/Invoke-Unity.ps1 -Action GenerateUI` 통과 (`GenerateUI-20260624-044844.log`)
- `scripts/Invoke-Unity.ps1 -Action ValidateData` 통과 (`ValidateData-20260624-044905.log`)
- `scripts/Invoke-Unity.ps1 -Action ValidateUI` 통과 (`ValidateUI-20260624-044923.log`)
- `scripts/Invoke-Unity.ps1 -Action TestEditMode` 통과 (`EditMode-20260624-044943.xml`)
- `scripts/Invoke-Unity.ps1 -Action TestPlayMode` 통과 (`PlayMode-20260624-045003.xml`)
- `git diff --check` 통과
