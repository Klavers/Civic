# P04 — 모듈형 문명 시스템 확장 (Umbrella)

> **Status**: planned
> **Branch**: `planning/P04`
> **Baseline**: P03 병합 상태의 경제·기술·prefab HUD
> **Proposal inventory**: [SUG06~SUG14](../suggestion/)
> **Feature contract single source**: [SUG14 — 모듈형 기능 토글과 조합](../suggestion/SUG14_모듈형_기능_토글과_조합.md)
> **Domain rule sources**: [SUG06](../suggestion/SUG06_환생과_문명_유산.md), [SUG07](../suggestion/SUG07_도전과제와_메타_보상.md), [SUG08](../suggestion/SUG08_환생_시작_문명.md), [SUG09](../suggestion/SUG09_문명과_국가_설립.md), [SUG10](../suggestion/SUG10_정치와_사회_체계.md), [SUG11](../suggestion/SUG11_조건부_이벤트와_사건_연쇄.md), [SUG12](../suggestion/SUG12_불가사의와_거대_건축.md), [SUG13](../suggestion/SUG13_위인과_인물_라이프사이클.md)
> **Relevant code roots**: `Civic/Assets/_Project/Runtime/Simulation/`, `Runtime/UI/`, `Editor/UI/`, `Data/`, `Tests/`

## §0 Context

P03까지의 Civic은 경제·기술 효과·prefab HUD를 하나의 기본 게임으로 제공한다. SUG06~SUG13은 환생, 도전과제, 시작 문명, 국가 설립, 정치·사회 체계, 이벤트, 불가사의, 위인이라는 큰 시스템을 추가한다. 이들을 직접 연결하면 한 기능의 수정이 다른 기능과 저장·UI·테스트에 연쇄적으로 영향을 주고, 일부 시스템만 시험하거나 비활성화하기 어렵다.

P04는 신규 기능 전체를 한 번에 강결합 구현하지 않는다. 먼저 기능 catalog, dependency resolver, profile, module registry, optional UI slot, save header, 조합 테스트 기반을 만든다. 이후 SUG06~SUG13을 독립 도메인 모듈로 추가하고, 두 기능 이상이 동시에 켜질 때만 필요한 동작은 별도 integration adapter로 연결한다.

모듈 ID, 의존 연산자, OFF fallback, 토글 방식은 SUG14가 단일 소스다. P04는 해당 계약을 구현하는 기술 구조와 순서만 정의하며 수치·도전 조건·문명 목록·이벤트·불가사의·인물 내용은 각 SUG 문서를 참조한다.

## §1 목표 (Goals)

1. 모든 신규 모듈을 OFF한 profile에서 현재 P03 게임의 계산·UI·데이터 동작을 보존한다.
2. SUG06~SUG13의 도메인 기능을 독립 등록·초기화·tick·snapshot·UI·저장 단위로 구현한다.
3. `requiresAll`, `requiresAny`, `extendsWhenAll`, `conflictsWith`, `fallbackTo` 관계를 선언형 데이터로 해석한다.
4. 도메인 모듈과 조합 adapter를 분리해 A 또는 B 단독, A+B, 충돌 조합을 각각 테스트할 수 있게 한다.
5. 새 게임·환생 profile을 런 시작 시 불변 feature set으로 확정하고 저장·불러오기에서 재현한다.
6. optional UI를 prefab 우선 규칙으로 구현해 기능 OFF 시 빈 패널·Missing Script·런타임 계층 생성을 남기지 않는다.
7. Baseline, 단독 모듈, 관계 진리표, pairwise, AllOn 검증을 자동화한다.
8. SUG06~SUG13의 신규 기능을 서로 독립적으로 추가·수정하고 필요한 adapter만 교체할 수 있는 개발 경계를 확립한다.

## §2 비목표 (Non-goals)

- P04 첫 구현 PR에서 SUG06~SUG13의 모든 콘텐츠와 최종 밸런스를 완성하지 않는다.
- 모듈을 런 도중 임의로 hot swap하지 않는다.
- 기존 경제·기술·가격·GDP 계산을 optional module 구현으로 대체하지 않는다.
- 런타임 `new GameObject` 또는 `AddComponent`로 module UI 계층을 생성하지 않는다.
- SUG14에 없는 새 module ID·의존 연산자를 하위 plan에서 임의 정의하지 않는다.
- 모든 2^N 조합을 수동 테스트 목록으로 작성하지 않는다.
- 실존 문명·국가·위인 명칭 확정과 아트 제작은 P04 구조 구현 범위 밖이다.
- `continuousHistory` 같은 환생 상보 campaign mode는 별도 승인 전 구현하지 않는다.

## §3 적용 대상 인벤토리와 단일 소스

| 제안 | P04 도메인 | 규칙 정본 | P04 구현 책임 |
|-|-|-|-|
| SUG06 | 메타 진행 | 환생 점수·초기화·유산은 SUG06 | 런 기록 service, reset command, meta save, modifier provider |
| SUG07 | 진행·도전 | 분류·조건·보상은 SUG07 | condition tracking, reward dispatch, snapshot·UI |
| SUG08 | 시작 정체성 | 문명 특성·시작 상태는 SUG08 | run bootstrap provider, trait source, 선택 UI |
| SUG09 | 국가 정체성 | 설립 절차·국가 규칙은 SUG09 | candidate·formation state, charter command, identity snapshot |
| SUG10 | 통치 | 정책·개혁 규칙은 SUG10 | political state, reform process, institution modifier |
| SUG11 | 서사 | scheduler·event 규칙은 SUG11 | deterministic scheduler, choice command, history |
| SUG12 | 프로젝트 | 비용·진행·효과는 SUG12 | delivery·construction state, upkeep, project UI |
| SUG13 | 인물 | 생성·배치·임기·유산은 SUG13 | candidate generator, slot command, lifecycle state |
| SUG14 | 기능 그래프 | module ID·관계·profile·OFF fallback은 SUG14 | catalog, resolver, registry, profile UI, 조합 검증 |

### 단일 소스 규칙

- module ID·관계 연산자·OFF fallback: SUG14 §3~§5 참조. P04 하위 plan 내 재정의 금지.
- 환생 점수와 유산 수치: SUG06 참조. P04는 계산 service 경계만 소유한다.
- 도전·문명·국가·정책·이벤트·불가사의·인물 콘텐츠: 해당 SUG 문서 참조.
- 시뮬레이션 자원·기술·가격·GDP: 기존 `Civic/GAME_DESIGN.md`와 P01~P03 정본 유지.
- P04가 새로 소유하는 정본은 feature framework API, module lifecycle, 구현 순서, 테스트 profile 계약이다.

## §4 설계 원칙

1. **Baseline 보존** — 신규 module registry가 비어 있으면 현재 simulation과 HUD 결과가 동일해야 한다.
2. **등록 기반 확장** — core가 개별 도메인 타입을 조회하지 않고 module이 interface 구현을 등록한다.
3. **조합 adapter 분리** — 도메인 assembly는 서로 참조하지 않고 integration assembly가 필요한 양쪽 capability를 조합한다.
4. **런 시작 불변** — resolved feature set은 런 생성 후 변경하지 않는다.
5. **데이터 단방향 의존** — optional 데이터가 core ID를 참조할 수 있지만 core CSV는 optional ID를 필수 참조하지 않는다.
6. **명시적 fallback** — dependency가 없을 때 정지하거나 null 예외를 내지 않고 SUG14의 OFF 동작을 사용한다.
7. **출처 추적** — modifier·자원 흐름·해금·이벤트 결과는 sourceType/sourceId를 snapshot에 남긴다.
8. **prefab 우선** — 생성기가 module panel과 공통 row pool을 만들고 런타임은 활성화·바인딩만 한다.
9. **저장 버전 명시** — module마다 payload version을 가지며 누락·불일치는 조용히 삭제하지 않는다.
10. **독립 테스트 우선** — 각 모듈은 adapter 없이도 기능과 fallback을 검증할 수 있어야 한다.

## §5 기술 아키텍처

### 5.1 assembly와 폴더 경계

권장 구조:

```text
Runtime/
  Features/
    Abstractions/       # feature set, module, capability, modifier 계약
    Framework/          # catalog, resolver, registry, profile, lifecycle
    Modules/
      Prestige/
      Achievements/
      Civilizations/
      Nations/
      Governance/
      Events/
      Wonders/
      People/
    Integrations/       # 둘 이상의 capability를 참조하는 adapter
  Simulation/           # 기존 core, optional 도메인 구체 타입 참조 금지
  UI/
    Features/           # 공통 module navigation·panel binding
Editor/
  Features/             # catalog/profile/data validator
  UI/                   # module prefab slot 생성·검증
Tests/
  EditMode/Features/
  PlayMode/Features/
```

초기에는 SUG별 domain assembly를 사용하고, 세부 subfeature마다 assembly를 만들지 않는다. `Abstractions → Framework → Domain → Integrations`의 단방향 참조를 유지한다. 기존 Simulation assembly는 Abstractions만 참조하거나 framework가 외부에서 주입되도록 해 domain 역참조를 막는다.

### 5.2 module lifecycle

1. core 데이터와 Feature Catalog를 로드한다.
2. project inclusion과 meta unlock으로 `Available` 집합을 만든다.
3. profile과 사용자 override로 requested set을 만든다.
4. resolver가 의존·충돌·fallback을 계산해 immutable resolved set을 만든다.
5. `ValidateProfile`로 필수 data·prefab·save version을 검사한다.
6. registry가 enabled domain module과 integration adapter를 한 번씩 등록한다.
7. 새 런 bootstrap, tick participant, command handler, modifier provider, snapshot contributor, UI binder를 구성한다.
8. 저장 시 resolved set과 module payload header를 기록한다.

등록 실패 시 일부 module만 조용히 끄지 않는다. 런 시작을 중단하고 module ID·누락 capability·해결 방법을 오류로 제공한다.

### 5.3 capability 경계

P04a에서 최소 interface를 확정한다.

- module lifecycle: 초기화·종료·save payload 제공
- run bootstrap: 시작 자원·기술·정체성 기여
- command handler: 환생·개혁·건국·이벤트 선택·프로젝트·인물 배치
- tick participant: 명시된 phase에서 상태 갱신
- modifier provider: core 계산에 적용할 modifier와 cap group 제공
- condition value provider: 도전·국가·이벤트가 조회하는 값 제공
- snapshot contributor: UI read model 제공
- UI binder: 저장된 prefab reference에 상태와 command 연결

정확한 interface 이름과 tick phase enum은 P04a가 정본으로 확정한다. 후속 module plan은 이를 참조하고 별도 변형을 만들지 않는다.

### 5.4 데이터와 검증

- `CivicFeatureCatalog.asset`을 feature data 진입점으로 둔다.
- module·dependency·profile schema는 SUG14 §10을 구현한다.
- 각 도메인 CSV는 독립 TextAsset reference를 갖고 module이 OFF여도 Editor 전체 정의 검증은 가능해야 한다.
- `ValidateAllDefinitions`: ID 중복, 참조, 순환, enum, 양수·범위, prefab asset 존재를 검사한다.
- `ValidateProfile`: requested/resolved set, fallback, conflict, included data, UI slot, save version을 검사한다.
- 생성기와 validator는 module 목록을 catalog에서 읽고 하드코딩된 패널 수를 사용하지 않는다.

### 5.5 UI와 prefab

- `UiPrefabGenerator`는 Feature Setup 화면, 공통 module toggle row, module navigation slot, domain panel root를 생성한다.
- 각 domain panel은 독립 Base Prefab과 사용자 편집 Variant를 갖는다.
- integration adapter는 별도 대형 패널보다 관련 domain panel의 미리 생성된 integration section을 사용한다.
- 기능 OFF 시 navigation button, panel, HUD summary, alert source를 모두 비활성화한다.
- 모든 profile에서 Missing Script, 직렬화 참조, ScrollRect, EventSystem, tooltip을 검증한다.
- module row 수는 catalog·CSV 기반으로 계산하고 고정 수량 테스트를 금지한다.

### 5.6 저장·메타 진행

- `CivicRunSave`와 `CivicMetaSave`를 분리한다.
- run save에는 profile, requested/resolved set, core state, module payload header·payload를 둔다.
- meta save에는 환생 포인트, 유산, 도전, 문명 해금, 도감 기록을 module별 namespace로 둔다.
- save backend가 아직 없다면 P04a에서 DTO·serializer·in-memory round-trip부터 만들고 P04b 착수 전에 파일 persistence를 확정한다.
- 모듈 제거·버전 변경은 migration table이 있을 때만 허용한다.
- unknown payload를 조용히 폐기하지 않고 안전한 read-only 진단 또는 로드 실패를 제공한다.

## §6 서브플랜 분할과 구현 순서

### P04a — Feature Framework와 Baseline (최우선)

- SUG14의 catalog, dependency resolver, profile, registry, module lifecycle 구현
- Baseline profile과 기존 게임 golden snapshot 확립
- Feature Setup prefab, module slot, Editor validator 구현
- save header·module payload round-trip 기반 구현
- 단독 dummy module과 dependency/conflict/fallback fixture로 framework 검증

완료 조건: 신규 기능 데이터가 없어도 현재 게임이 동일하게 작동하고, fixture 조합 진리표가 모두 통과한다.

### P04b — 메타 진행과 시작 정체성

- SUG06 환생·런 기록·유산
- SUG07 도전과제·보상
- SUG08 시작 문명·특성·run bootstrap
- 세 도메인의 단독 모드와 SUG14가 선언한 조합 adapter

완료 조건: 각 도메인 단독 profile과 세 기능 조합 profile의 저장·UI·환생 흐름이 통과한다.

### P04c — 국가와 통치

- SUG09 국가 후보·설립·헌장
- SUG10 정치·사회 체계·개혁
- default 정체성 fallback과 국가-정치 adapter

완료 조건: 시작 문명 OFF에서도 범용 국가가 작동하고, 정치 OFF에서는 고정 국가 modifier fallback이 작동한다.

### P04d — 이벤트·불가사의·위인

- SUG11 deterministic scheduler와 event history
- SUG12 불가사의 납품·건설·완공
- SUG13 위인 후보·배치·lifecycle
- 이벤트 OFF 고정 결과와 각 pair adapter

완료 조건: 세 도메인이 각각 단독 작동하고, 이벤트를 추가했을 때 상태를 대체하지 않고 선택지만 확장한다.

### P04e — 전체 조합·밸런스·콘텐츠

- SUG14 AllOn adapter와 종합 연대기·도전·결말 연결
- 공식 preset별 대표 콘텐츠와 밸런스 시나리오
- pairwise 조합 자동 생성과 장시간 simulation soak test
- profile별 도움말·복잡도·추천 설정 정리

완료 조건: Baseline과 공식 preset, AllOn이 동일 build에서 재현되고 modifier·save·UI 중복이 없다.

### 분할 근거

- framework 없이 도메인을 먼저 구현하면 feature check가 각 코드에 흩어지고 제거가 어려워진다.
- 메타·정체성은 새 런 bootstrap과 저장을 먼저 검증해야 후속 국가·정치가 안정된다.
- 이벤트는 다른 상태를 소유하지 않는 adapter로 마지막에 연결해야 domain 간 순환 참조를 피할 수 있다.
- 각 서브플랜은 독립 PR과 profile 테스트가 가능해야 하며, P04e 이전에도 Baseline은 항상 배포 가능해야 한다.

## §7 구현 단계 (Steps)

### Step 1 — 기준선 고정

- [ ] P03 병합 commit에서 Baseline 데이터·snapshot·HUD·Unity 테스트 결과를 기록한다.
- [ ] 기존 simulation의 public command, tick, snapshot, UI binding 의존을 inventory한다.
- [ ] core에서 optional domain을 참조하게 될 위험 지점을 테스트로 고정한다.

### Step 2 — SUG14 framework

- [ ] Feature Catalog asset과 module/dependency/profile schema를 추가한다.
- [ ] deterministic dependency resolver와 cycle/conflict diagnostics를 구현한다.
- [ ] immutable resolved feature set과 module registry를 구현한다.
- [ ] Null Object·fallback·adapter 자동 등록 계약을 구현한다.
- [ ] Editor `Tools > Civic > Features > Validate` 메뉴와 batch action을 추가한다.

### Step 3 — profile·UI·저장 기반

- [ ] Baseline 및 개발 fixture profile을 데이터로 정의한다.
- [ ] Feature Setup Base Prefab, 사용자 Variant, module row와 요약 패널을 생성한다.
- [ ] 새 런 시작 전에만 gameplay module을 변경하도록 UI를 제한한다.
- [ ] run/meta save DTO와 module payload header round-trip을 구현한다.
- [ ] profile과 save의 requested/resolved set 불일치를 검증한다.

### Step 4 — 도메인 세로 슬라이스 반복

각 SUG 도메인은 다음 순서를 반복한다.

1. module data와 validator
2. pure state/service와 command
3. tick·modifier·condition·snapshot capability 등록
4. Base Prefab과 binder
5. module OFF 회귀 테스트
6. module 단독 ON EditMode·PlayMode 테스트
7. save round-trip
8. 관련 adapter 추가 전 독립 완료 확인

### Step 5 — integration adapter

- [ ] SUG14의 각 adapter를 별도 class·data row·테스트 fixture로 구현한다.
- [ ] A only, B only, A+B 진리표를 모두 검증한다.
- [ ] adapter가 domain state를 소유하지 않고 capability만 조합하는지 review한다.
- [ ] adapter OFF/제거 후 domain save가 그대로 로드되는지 검증한다.

### Step 6 — 공식 profile과 조합 검증

- [ ] Baseline, 권장 preset, 단독 module, 관계 fixture, pairwise, AllOn profile을 생성한다.
- [ ] profile별 data/UI/save validation을 실행한다.
- [ ] 장시간 Advance simulation에서 음수 자원, 중복 modifier, event 폭주, save payload 증가를 검사한다.
- [ ] 사용자 도움말에 ON/OFF 영향과 fallback을 표시한다.

### Step 7 — 문서·이슈 동기화

- [ ] 각 하위 plan·issue는 구현한 SUG와 SUG14 관계를 링크한다.
- [ ] 체크박스는 `check-and-verify`로 실제 profile 테스트 통과 항목만 갱신한다.
- [ ] 변경된 module ID·관계는 SUG14에 먼저 반영하고 하위 문서는 참조만 갱신한다.

## §8 검증 (Verification)

### 8.1 Framework EditMode

- 같은 requested set은 입력 순서와 무관하게 같은 resolved set을 만든다.
- requiresAll·requiresAny·extendsWhenAll·conflictsWith·fallbackTo를 각각 fixture로 검증한다.
- 순환·누락·충돌 자동 활성화는 명확한 module path와 함께 실패한다.
- adapter는 필요한 capability가 모두 있을 때 정확히 1회 등록된다.
- module OFF 시 provider·command·tick·snapshot contributor가 registry에 없다.

### 8.2 Baseline 회귀

- 모든 신규 기능 OFF에서 초기 상태, 1초 tick, 가격, GDP, 국고, 건설력, 연구, 건설 결과가 기준 golden과 같다.
- Baseline HUD에 optional panel·alert·빈 행이 표시되지 않는다.
- 기존 `ValidateData`, `GenerateUI`, `ValidateUI`, EditMode, PlayMode를 계속 통과한다.

### 8.3 Domain 단독 검증

- SUG06~SUG13 각각을 단독 ON한 profile이 예외 없이 시작·진행·저장·로드된다.
- 필요한 연계 기능이 없을 때 SUG14 OFF fallback이 작동한다.
- 비활성 도메인의 데이터·UI·modifier가 snapshot에 나타나지 않는다.

### 8.4 조합 검증

- SUG14 §5의 A/B/C/D/E 시나리오를 진리표 테스트로 고정한다.
- pairwise profile에서 startup, 10분 simulation, UI navigation, save round-trip을 실행한다.
- AllOn에서 adapter 중복 등록, modifier cap 초과, 순환 event, 상충 command가 없다.
- 기능 조합을 바꾸려면 새 런이 필요하고 기존 save는 원래 resolved set으로만 로드된다.

### 8.5 UI·prefab 검증

- profile별 navigation button과 panel 활성 상태가 정확하다.
- module OFF profile에서도 Missing Script·null 직렬화 참조가 없다.
- Base 재생성 시 사용자 Variant override가 보존된다.
- module 목록·row 수 변경 후 Generate 재실행에 중복 object가 없다.
- tooltip에 의존·충돌·fallback·저장 영향이 표시된다.

### 8.6 저장 검증

- run/meta save round-trip이 module별 payload와 version을 보존한다.
- unknown module, missing module, version mismatch가 조용한 데이터 손실 없이 실패한다.
- adapter를 끈 build에서도 domain payload를 중복 변환하지 않는다.
- 환생 reset은 enabled module의 reset contract만 실행하고 core·meta 경계를 침범하지 않는다.

### 8.7 Unity 작업 절차

- Editor가 닫혀 있고 lockfile이 없을 때 `scripts/Invoke-Unity.ps1`을 사용한다.
- feature 검증 action 추가 후 `ValidateFeatures`, `ValidateData`, `GenerateUI`, `ValidateUI`, `TestEditMode`, `TestPlayMode`를 실행한다.
- Editor가 열려 있으면 menu와 Test Runner 절차를 사용한다.
- MSBuild는 Unity import 후 보조 검증으로 실행한다.

## §9 리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| 조합 수가 기하급수적으로 증가 | 모든 조합 수동 테스트 불가 | 선언 관계 fixture + pairwise + AllOn + 공식 preset만 release gate |
| core에 feature if 문이 확산 | OFF 상태도 신규 코드에 종속 | registry·capability 주입, core의 domain type 참조 금지 테스트 |
| module OFF 시 상태 잔존 | 숨은 modifier·tick·save 오염 | 런 시작 registry 고정, source audit, Baseline golden |
| adapter가 domain state를 소유 | adapter 제거 시 save 손상 | adapter는 계산·연결만 수행하고 state는 domain이 소유 |
| 저장 버전 drift | 기능 토글 후 로드 실패·데이터 손실 | module payload header, migration table, 조용한 삭제 금지 |
| optional UI가 prefab 규칙 위반 | 런타임 계층 생성·사용자 수정 손실 | module Base/Variant, generator·validator, runtime 활성화만 허용 |
| 모든 기능 ON이 기본 전제화 | 독립 모듈 테스트 무의미 | 각 domain 단독 완료를 adapter 착수 gate로 설정 |
| 영구 modifier 중첩 폭주 | 경제 밸런스 붕괴 | sourceType·capGroup, profile별 modifier audit |
| disabled 콘텐츠 참조 오류 | 런 시작 실패 또는 숨은 항목 노출 | ValidateAllDefinitions와 ValidateProfile 분리 |
| 설정 UI 선택 피로 | 초보자 이탈 | preset 우선, 고급 설정 선택 노출, 의존 자동 해결 미리보기 |

## §10 미결 항목과 권장안

### 10.1 플레이어 고급 토글 공개 시점

- (a) 현재: 토글 UI가 없고 SUG14에서 새 게임·환생 설정을 제안했다.
- (b) 옵션: 처음부터 전체 공개 / 환생 후 공개 / preset만 공개하고 debug에서 전체 사용.
- (c) 권장: 첫 런은 Baseline·권장 preset만, 첫 환생 후 고급 토글 공개. 개발 profile은 항상 전체 사용.

### 10.2 공식 밸런스 profile

- (a) 현재: 모든 조합의 밸런스 기준이 없다.
- (b) 옵션: 모든 조합 공식 지원 / 단일 AllOn만 지원 / 소수 curated preset만 공식 지원.
- (c) 권장: Baseline, Meta, Society, Full Campaign의 소수 preset을 공식 release gate로 두고 custom 조합은 기능 정합성만 보장한다.

### 10.3 assembly 세분화

- (a) 현재: 기존 runtime assembly 중심이며 신규 domain 경계가 없다.
- (b) 옵션: 모든 subfeature별 asmdef / SUG 도메인별 asmdef / 하나의 거대 Features asmdef.
- (c) 권장: Abstractions·Framework·SUG 도메인별·Integrations 단위. 세부 subfeature는 폴더와 namespace로만 나눈다.

### 10.4 저장 backend

- (a) 현재: P01~P03 정본은 저장·불러오기를 비목표로 두었다.
- (b) 옵션: P04a에서 파일 persistence까지 구현 / DTO·serializer만 구현 후 P04b에서 persistence / 메타만 PlayerPrefs 사용.
- (c) 권장: P04a에서 DTO·version·in-memory round-trip, P04b 시작 전에 JSON 파일 persistence plan을 별도 확정. PlayerPrefs에 복합 상태를 저장하지 않는다.

### 10.5 상보 campaign mode

- (a) 현재: SUG14는 strict reset과 continuous history의 충돌 사례만 정의한다.
- (b) 옵션: P04에 두 모드 모두 포함 / strict reset만 구현 / campaign mode 자체를 별도 P05로 분리.
- (c) 권장: P04는 SUG06 strict reset만 구현하고 continuous history는 별도 후속 plan으로 분리한다.

## §11 후속 (Follow-up)

- P04a 착수 전 feature framework API와 tick phase를 grill-me로 확정한다.
- P04b 착수 전 저장 backend와 환생 reset transaction·backup을 별도 plan으로 구체화한다.
- SUG06~SUG13의 수치·콘텐츠는 각 domain 하위 plan에서 grill하고 해당 SUG 문서를 먼저 갱신한다.
- `continuousHistory` campaign mode는 SUG14의 상호배타 사례를 기반으로 별도 제안·plan에서 검토한다.
- 공식 preset별 밸런스와 장시간 idle 진행 속도는 P04e 이후 독립 밸런스 plan으로 분리한다.
- 구현 완료 후 P04 umbrella의 실제 완료 범위에 따라 `_done` 또는 부분 후속 plan 상태를 결정한다.
