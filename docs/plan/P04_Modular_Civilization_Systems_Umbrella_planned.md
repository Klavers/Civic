# P04 — 모듈형 문명 시스템 확장 (Umbrella)

> **Status**: planned
> **Branch**: `P04/implement`
> **Baseline**: P03 병합 상태의 경제·기술·prefab HUD
> **GitHub issue**: [#36 P04: 모듈형 기능 토글과 문명 시스템 확장](https://github.com/Klavers/Civic/issues/36)
> **Proposal inventory**: [SUG06~SUG14](../suggestion/)
> **Feature contract single source**: [SUG14 — 모듈형 기능 토글과 조합](../suggestion/SUG14_모듈형_기능_토글과_조합.md)
> **Domain rule sources**: [SUG06](../suggestion/SUG06_환생과_문명_유산.md), [SUG07](../suggestion/SUG07_도전과제와_메타_보상.md), [SUG08](../suggestion/SUG08_환생_시작_문명.md), [SUG09](../suggestion/SUG09_문명과_국가_설립.md), [SUG10](../suggestion/SUG10_정치와_사회_체계.md), [SUG11](../suggestion/SUG11_조건부_이벤트와_사건_연쇄.md), [SUG12](../suggestion/SUG12_불가사의와_거대_건축.md), [SUG13](../suggestion/SUG13_위인과_인물_라이프사이클.md)
> **Relevant code roots**: `Civic/Assets/_Project/Runtime/Simulation/`, `Runtime/UI/`, `Editor/UI/`, `Data/`, `Tests/`

## §0 Context

P03까지의 Civic은 경제·기술 효과·prefab HUD를 하나의 기본 게임으로 제공한다. SUG06~SUG13은 환생, 도전과제, 시작 문명, 국가 설립, 정치·사회 체계, 이벤트, 불가사의, 위인이라는 큰 시스템을 추가한다. 이들을 직접 연결하면 한 기능의 수정이 다른 기능과 저장·UI·테스트에 연쇄적으로 영향을 주고, 일부 시스템만 시험하거나 비활성화하기 어렵다.

P04는 신규 기능 전체를 한 번에 강결합 구현하지 않는다. 먼저 고정 runtime registry, dependency resolver, 런별 feature selection, optional UI slot, save header, 조합 테스트 기반을 만든다. 이후 SUG06~SUG13을 독립 도메인 모듈로 추가하고, 두 기능 이상이 동시에 켜질 때만 필요한 동작은 별도 integration adapter로 연결한다.

모듈 ID, 의존 연산자, OFF fallback, 토글 방식은 SUG14가 단일 소스다. P04는 해당 계약을 구현하는 기술 구조와 순서만 정의하며 수치·도전 조건·문명 목록·이벤트·불가사의·인물 내용은 각 SUG 문서를 참조한다.

## §1 목표 (Goals)

1. 모든 신규 모듈을 OFF한 Baseline case에서 현재 P03 게임의 계산·UI·데이터 동작을 보존한다.
2. SUG06~SUG13의 도메인 기능을 독립 등록·초기화·tick·snapshot·UI·저장 단위로 구현한다.
3. `requiresAll`, `requiresAny`, `extendsWhenAll`, `conflictsWith`, `fallbackTo` 관계를 선언형 데이터로 해석한다.
4. 도메인 모듈과 조합 adapter를 분리해 A 또는 B 단독, A+B, 충돌 조합을 각각 테스트할 수 있게 한다.
5. 새 게임의 사용자 선택을 런 시작 시 불변 feature set으로 확정하고 저장·불러오기에서 재현한다.
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
- 규칙 preset과 메타 진행에 따른 module 단계 해금은 구현하지 않는다.
- 프로젝트 전역 Feature Catalog asset과 build별 module 포함/제외는 구현하지 않는다.
- 외부 DLL·사용자 mod 자동 발견과 hot reload는 구현하지 않는다.

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
| SUG14 | 기능 그래프 | module ID·관계·런별 선택·OFF fallback은 SUG14 | resolver, runtime registry, 메인 메뉴 UI, 조합 검증 |

### 단일 소스 규칙

- module ID·관계 연산자·OFF fallback: SUG14 §3~§5 참조. P04 하위 plan 내 재정의 금지.
- 환생 점수와 유산 수치: SUG06 참조. P04는 계산 service 경계만 소유한다.
- 도전·문명·국가·정책·이벤트·불가사의·인물 콘텐츠: 해당 SUG 문서 참조.
- 시뮬레이션 자원·기술·가격·GDP: 기존 `Civic/GAME_DESIGN.md`와 P01~P03 정본 유지.
- P04가 새로 소유하는 정본은 feature framework API, module lifecycle, 구현 순서, Feature Matrix case 계약이다.

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
    Framework/          # resolver, runtime registry, selection, lifecycle
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
  Features/             # registry/selection/data validator
  UI/                   # module prefab slot 생성·검증
Tests/
  EditMode/Features/
  PlayMode/Features/
```

초기에는 SUG별 domain assembly를 사용하고, 세부 subfeature마다 assembly를 만들지 않는다. `Abstractions → Framework → Domain → Integrations`의 단방향 참조를 유지한다. 기존 Simulation assembly는 Abstractions만 참조하거나 framework가 외부에서 주입되도록 해 domain 역참조를 막는다.

### 5.2 module lifecycle

1. core 데이터와 build에 포함된 고정 runtime registry를 로드한다.
2. registry의 전체 모듈을 `Available` 집합으로 사용한다.
3. 메인 메뉴 사용자 선택 또는 Feature Matrix case로 requested set을 만든다.
4. resolver가 의존·충돌·fallback을 계산해 immutable resolved set을 만든다.
5. `ValidateSelection`으로 requested/resolved set과 필수 data·prefab·save version을 검사한다.
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

- build에 포함된 SUG06~SUG13 module definition과 integration 관계는 runtime registry가 제공한다.
- module ID와 관계는 SUG14를 정본으로 하며 별도 Feature Catalog asset을 만들지 않는다.
- 플레이어 선택은 메인 메뉴의 런별 requested feature set, 테스트 선택은 Feature Matrix case로 입력한다.
- 각 도메인 CSV는 독립 TextAsset reference를 갖고 module이 OFF여도 Editor 전체 정의 검증은 가능해야 한다.
- `ValidateAllDefinitions`: ID 중복, 참조, 순환, enum, 양수·범위, prefab asset 존재를 검사한다.
- `ValidateSelection`: requested/resolved set, fallback, conflict, included data, UI slot, save version을 검사한다.
- 생성기와 validator는 module 목록을 runtime registry에서 읽고 하드코딩된 패널 수를 사용하지 않는다.

### 5.5 UI와 prefab

- `UiPrefabGenerator`는 Feature Setup 화면, 공통 module toggle row, module navigation slot, domain panel root를 생성한다.
- 각 domain panel은 독립 Base Prefab과 사용자 편집 Variant를 갖는다.
- integration adapter는 별도 대형 패널보다 관련 domain panel의 미리 생성된 integration section을 사용한다.
- 기능 OFF 시 navigation button, panel, HUD summary, alert source를 모두 비활성화한다.
- 모든 Feature Matrix case에서 Missing Script, 직렬화 참조, ScrollRect, EventSystem, tooltip을 검증한다.
- module row 수는 runtime registry 기반으로 계산하고 UI 코드의 별도 숫자 하드코딩을 금지한다.

### 5.6 저장·메타 진행

- `CivicRunSave`와 `CivicMetaSave`를 분리한다.
- run save에는 requested/resolved set, core state, module payload header·payload를 둔다.
- meta save에는 환생 포인트, 유산, 도전, 문명 해금, 도감 기록을 module별 namespace로 둔다.
- save backend가 아직 없다면 P04a에서 DTO·serializer·in-memory round-trip부터 만들고 P04b 착수 전에 파일 persistence를 확정한다.
- 모듈 제거·버전 변경은 migration table이 있을 때만 허용한다.
- unknown payload를 조용히 폐기하지 않고 안전한 read-only 진단 또는 로드 실패를 제공한다.

## §6 서브플랜 분할과 구현 순서

### P04a — Feature Framework와 Baseline (최우선)

- SUG14의 runtime registry, dependency resolver, 런별 selection, module lifecycle 구현
- Baseline case와 기존 게임 golden snapshot 확립
- Feature Setup prefab, module slot, Editor validator 구현
- save header·module payload round-trip 기반 구현
- 단독 dummy module과 dependency/conflict/fallback fixture로 framework 검증

완료 조건: 신규 기능 데이터가 없어도 현재 게임이 동일하게 작동하고, fixture 조합 진리표가 모두 통과한다.

### P04b — 메타 진행과 시작 정체성

- SUG06 환생·런 기록·유산
- SUG07 도전과제·보상
- SUG08 시작 문명·특성·run bootstrap
- 세 도메인의 단독 모드와 SUG14가 선언한 조합 adapter

완료 조건: 각 도메인 단독 case와 세 기능 조합 case의 저장·UI·환생 흐름이 통과한다.

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
- 대표 기능 조합별 콘텐츠와 밸런스 시나리오
- pairwise 조합 자동 생성과 장시간 simulation soak test
- 기능별 도움말·복잡도·상호작용 설명 정리

완료 조건: Baseline과 단독·pairwise·AllOn이 동일 build에서 재현되고 modifier·save·UI 중복이 없다.

### 분할 근거

- framework 없이 도메인을 먼저 구현하면 feature check가 각 코드에 흩어지고 제거가 어려워진다.
- 메타·정체성은 새 런 bootstrap과 저장을 먼저 검증해야 후속 국가·정치가 안정된다.
- 이벤트는 다른 상태를 소유하지 않는 adapter로 마지막에 연결해야 domain 간 순환 참조를 피할 수 있다.
- 각 서브플랜은 독립 PR과 Feature Matrix 테스트가 가능해야 하며, P04e 이전에도 Baseline은 항상 배포 가능해야 한다.

## §7 구현 단계 (Steps)

### Step 1 — 기준선 고정

- [ ] P03 병합 commit에서 Baseline 데이터·snapshot·HUD·Unity 테스트 결과를 기록한다.
- [ ] 기존 simulation의 public command, tick, snapshot, UI binding 의존을 inventory한다.
- [ ] core에서 optional domain을 참조하게 될 위험 지점을 테스트로 고정한다.

### Step 2 — SUG14 framework

- [ ] SUG06~SUG13 module definition과 integration relation을 제공하는 runtime registry를 추가한다.
- [ ] deterministic dependency resolver와 cycle/conflict diagnostics를 구현한다.
- [ ] immutable resolved feature set과 module registry를 구현한다.
- [ ] Null Object·fallback·adapter 자동 등록 계약을 구현한다.
- [ ] Editor `Tools > Civic > Features > Validate` 메뉴와 batch action을 추가한다.

### Step 3 — 런별 선택·UI·저장 기반

- [ ] Baseline 및 개발 Feature Matrix case를 코드로 생성한다.
- [ ] Feature Setup Base Prefab, 사용자 Variant, module row와 요약 패널을 생성한다.
- [ ] 새 런 시작 전에만 gameplay module을 변경하도록 UI를 제한한다.
- [ ] run/meta save DTO와 module payload header round-trip을 구현한다.
- [ ] 런 선택과 save의 requested/resolved set 불일치를 검증한다.

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

### Step 6 — Feature Matrix 조합 검증

- [ ] Baseline, 단독 module, 관계 fixture, pairwise, AllOn Feature Matrix case를 생성한다.
- [ ] Feature Matrix case별 resolver·UI·save validation을 실행한다.
- [ ] 장시간 Advance simulation에서 음수 자원, 중복 modifier, event 폭주, save payload 증가를 검사한다.
- [ ] 사용자 도움말에 ON/OFF 영향과 fallback을 표시한다.

### Step 7 — 문서·이슈 동기화

- [ ] 각 하위 plan·issue는 구현한 SUG와 SUG14 관계를 링크한다.
- [ ] 체크박스는 `check-and-verify`로 실제 Feature Matrix 테스트 통과 항목만 갱신한다.
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

- SUG06~SUG13 각각을 단독 ON한 case가 예외 없이 시작·진행·저장·로드된다.
- 필요한 연계 기능이 없을 때 SUG14 OFF fallback이 작동한다.
- 비활성 도메인의 데이터·UI·modifier가 snapshot에 나타나지 않는다.

### 8.4 조합 검증

- SUG14 §5의 A/B/C/D/E 시나리오를 진리표 테스트로 고정한다.
- pairwise case에서 startup, 10분 simulation, UI navigation, save round-trip을 실행한다.
- AllOn에서 adapter 중복 등록, modifier cap 초과, 순환 event, 상충 command가 없다.
- 기능 조합을 바꾸려면 새 런이 필요하고 기존 save는 원래 resolved set으로만 로드된다.

### 8.5 UI·prefab 검증

- Feature Matrix case별 navigation button과 panel 활성 상태가 정확하다.
- module OFF Baseline에서도 Missing Script·null 직렬화 참조가 없다.
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
- feature 검증 action 추가 후 `GenerateMainMenu`, `ValidateMainMenu`, `FeatureMatrix`, `ValidateData`, `GenerateUI`, `ValidateUI`, `TestEditMode`, `TestPlayMode`를 실행한다.
- Editor가 열려 있으면 menu와 Test Runner 절차를 사용한다.
- MSBuild는 Unity import 후 보조 검증으로 실행한다.

## §9 리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| 조합 수가 기하급수적으로 증가 | 모든 조합 수동 테스트 불가 | 선언 관계 fixture + Baseline + 단독 + pairwise + AllOn을 release gate로 사용 |
| core에 feature if 문이 확산 | OFF 상태도 신규 코드에 종속 | registry·capability 주입, core의 domain type 참조 금지 테스트 |
| module OFF 시 상태 잔존 | 숨은 modifier·tick·save 오염 | 런 시작 registry 고정, source audit, Baseline golden |
| adapter가 domain state를 소유 | adapter 제거 시 save 손상 | adapter는 계산·연결만 수행하고 state는 domain이 소유 |
| 저장 버전 drift | 기능 토글 후 로드 실패·데이터 손실 | module payload header, migration table, 조용한 삭제 금지 |
| optional UI가 prefab 규칙 위반 | 런타임 계층 생성·사용자 수정 손실 | module Base/Variant, generator·validator, runtime 활성화만 허용 |
| 모든 기능 ON이 기본 전제화 | 독립 모듈 테스트 무의미 | 각 domain 단독 완료를 adapter 착수 gate로 설정 |
| 영구 modifier 중첩 폭주 | 경제 밸런스 붕괴 | sourceType·capGroup, Feature Matrix case별 modifier audit |
| disabled 콘텐츠 참조 오류 | 런 시작 실패 또는 숨은 항목 노출 | ValidateAllDefinitions와 ValidateSelection 분리 |
| 설정 UI 선택 피로 | 초보자 이탈 | 기능 설명, 선택 수·자동 연계 요약, 잘못된 조합의 시작 차단 |

## §10 미결 항목과 권장안

### 10.1 플레이어 고급 토글 공개 시점 — 결정 완료

- (a) 현재: 메인 메뉴에서 새 게임 시작 전에 전체 module toggle을 공개한다.
- (b) 제외: preset 우선 노출과 메타 진행 단계 해금은 사용자 결정으로 구현하지 않는다.
- (c) 결정: 모든 선택 가능한 module을 처음부터 직접 ON/OFF하고 Feature Matrix는 개발·테스트 전용 API로 제공한다.

### 10.2 공식 밸런스 preset — P04 1차 범위 제외

- (a) 현재: gameplay preset은 구현하지 않는다.
- (b) 테스트 대안: Baseline·단독·pairwise·AllOn은 player preset이 아니라 자동 검증 case로만 사용한다.
- (c) 결정: 밸런스 preset은 후속 범위로 두고 P04 1차는 조합 정합성만 보장한다.

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
- 대표 기능 조합별 밸런스와 장시간 idle 진행 속도는 P04e 이후 독립 밸런스 plan으로 분리한다.
- 구현 완료 후 P04 umbrella의 실제 완료 범위에 따라 `_done` 또는 부분 후속 plan 상태를 결정한다.

## §12 SUG14 1차 구현 현황 (2026-06-28)

P04 전체 도메인 구현에 앞서 SUG14 기반을 먼저 구현했다.

- 같은 build 안의 고정 runtime registry에 SUG06~SUG13 상위 모듈 8종을 등록했다.
- requested/resolved set, 의존·충돌 진단, 조합 integration 자동 활성화와 런 시작 후 immutable lock을 구현했다.
- `CivicMainMenu` Base Prefab·사용자 Variant와 `MainMenu.unity`를 생성하고 Build Settings의 0번에 MainMenu, 1번에 SampleScene을 등록했다.
- 메인 메뉴에서 런 시작 전 모듈을 개별 ON/OFF하고 선택 수·자동 연계 수·오류를 확인할 수 있다.
- Feature Matrix는 Baseline 1 + 단독 8 + pairwise 28 + AllOn 1의 총 38 case를 Build Settings 변경 없이 실행한다.
- 프로젝트 전역 Feature Catalog asset, 규칙 preset, 메타 진행 해금, 외부 DLL/mod 발견은 생성하지 않았다.
- SUG06~SUG13의 실제 gameplay 효과는 아직 구현하지 않았으며, 현재 토글은 선택·검증·런 전달 기반까지만 제공한다.

검증 결과:

- Unity `Compile`, `GenerateMainMenu`, `ValidateMainMenu`, `FeatureMatrix` 통과
- 기존 `ValidateData`, `GenerateUI`, `ValidateUI` 회귀 통과
- EditMode 27/27, PlayMode 2/2 통과
- `dotnet msbuild Civic/Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true` 통과

체크박스는 `check-and-verify` 승인 절차를 거치기 전까지 이 구현 현황만 기록하고 자동 변경하지 않는다.

## §13 SUG06~SUG13 도메인 1차 구현 현황 (2026-06-28)

SUG14 기반 위에 상위 모듈 8종의 실제 런타임 도메인 동작을 연결했다. 본 절은 §12의 기반 구현 이후 누적 결과이며, SUG 문서에 수치·공식이 명시된 항목과 모듈 독립성 검증을 우선한다.

### 13.1 공통 실행·저장 경계

- `CivicModuleRuntime`이 resolved feature set에 포함된 모듈만 생성하고 `Advance`, 건설, 연구 event를 전달한다.
- sourceType/sourceId/capGroup을 갖는 공통 modifier ledger를 core 건설비·기술비·투입·산출·GDP·가격·세수·인구 소비 계산에 연결했다.
- 모든 모듈 OFF의 Baseline은 직접 `CivicGameSimulation` 실행과 동일한 snapshot을 유지한다.
- `CivicMetaProgress`는 버전이 포함된 JSON 파일로 영속화하고 교체 저장 시 `.bak`을 남긴다. 테스트는 in-memory store로 격리한다.
- 런 저장의 requested/resolved feature set과 module payload version을 검증하는 `CivicRunFeatureSaveHeader` JSON round-trip을 구현했다. 전체 core/module run payload 직렬화는 아직 구현하지 않았다.

### 13.2 도메인 모듈

- SUG06: 런 최고 GDP·인구·시대·기술·도전 점수 기반 환생 preview와 1회 환생 command를 구현했다. UI는 2단계 확인 후 MainMenu로 돌아가 새 런을 시작한다.
- SUG07: 제안된 도전과제 15개, 일반·금지·지속 조건, 계정 1회 완료와 환생 보상 연계를 구현했다.
- SUG08: 기본 문명과 제안 문명 15개, 시작 자원·건물·기술·modifier를 구현했다. MainMenu에서 시작 문명 모듈 ON 시 현재 feature 조합에서 유효한 문명을 선택한다.
- SUG09: 국가 15개, 조건 발견률, 준비·중단·헌장 확정과 현재 국가 modifier를 구현했다.
- SUG10: 5분류×3개 제도, 정치력·정당성·피로도, 단일 개혁, 25/50/75% threshold와 GDP 비율 유지비를 구현했다.
- SUG11: 이벤트 15개×선택지 3개, 5초 scheduler, 확정·조건부·연쇄, 누적 가중치·pity, 결정적 seed/sequence, cooldown·run 상한, 3개 queue, 기본 일시정지, 기간 modifier와 순환 chain validator를 구현했다.
- SUG12: 불가사의 15개, 한 번에 1개 프로젝트, 자원 납품, 70% 취소 환급, 유지비, 완공·기간 modifier와 메타 기록을 구현했다.
- SUG13: 위인 15명, 후보 3명·120초, 활성 슬롯 3개, 영입·배치·1회 능력·600~1800초 임기·은퇴와 10% 유산을 구현했다.

### 13.3 prefab UI

- MainMenu Base/Variant에 모듈 토글 8개와 시작 문명 선택 영역을 생성했다.
- CivicHud Base/Variant에 활성 모듈만 표시하는 8개 tab과 18개 고정 action row pool을 생성했다.
- 환생, 국가 설립·취소·헌장 확정, 개혁·취소, 이벤트 선택, 불가사의 착공·취소, 위인 영입·능력 command를 저장된 row/button에서 실행한다.
- 런타임에서 UI 계층을 생성하지 않으며 사용자 Variant를 재생성 시 덮어쓰지 않는다.

### 13.4 검증 결과

- `Compile`, `GenerateMainMenu`, `ValidateMainMenu`, `GenerateUI`, `ValidateData`, `ValidateUI`, `FeatureMatrix` PASS
- Feature Matrix resolver: Baseline 1 + 단독 8 + pairwise 28 + AllOn 1 = 38 case PASS
- 실제 `CivicModuleRuntime`: 위 38 case 생성·1초 Advance·활성 module 집합 일치 PASS
- AllOn 600초 Advance: 음수 자원 및 exact modifier 중복 없음 PASS
- EditMode: 52/52 PASS (`EditMode-20260628-213742.xml`)
- PlayMode: 4/4 PASS (`PlayMode-20260628-213818.xml`)
- MSBuild: 외부 권한 재실행 PASS. 최초 실패는 `%LOCALAPPDATA%/Microsoft SDKs` sandbox 읽기 제한이 원인이었다.
- 최종 상태: Unity 프로세스 없음, `Civic/Temp/UnityLockfile` 없음.

## §14 구현 후 미결 항목과 권장안

### 14.1 문명 유산 구매 비용

- (a) 현재: SUG06은 `costPerRank` 필드를 제안하지만 각 유산의 실제 단계별 비용을 정하지 않았다. 임의 비용을 만들지 않았으며 HUD에 보유 rank와 효과를 표시하되 구매 버튼은 `비용 미결정`으로 비활성화한다.
- (b) 옵션: 유산별 고정 비용표 / 카테고리 공통 등비 비용 / 환생 횟수에 따른 동적 비용. 고정 비용표는 설명과 검증이 쉽고, 공통 곡선은 조정이 쉽고, 동적 비용은 반복 런 억제가 강하지만 예측성이 낮다.
- (c) 권장: 유산별 `costPerRank` 고정 배열을 CSV 정본으로 추가하고 SUG06 grill에서 15개 비용과 재분배 정책을 함께 확정한다. 확정 전 구매·효과 적용을 활성화하지 않는다.

### 14.2 수치·하위 시스템이 없는 `planned` 효과

- (a) 현재: 문명·국가·정치·이벤트·불가사의·위인 CSV에 총 149개 `planned` 행이 있다. 이는 SUG 원문에 효과 방향만 있고 수치·기간·대상 계산식이 없거나 생활수준·지지 블록·충성도·후보 가중치처럼 아직 없는 하위 시스템을 요구하는 항목이다.
- (b) 옵션: AI 추정 수치로 즉시 활성화 / `planned` 상태를 유지하고 후속 grill / 행을 삭제해 현재 가능한 효과만 남김. 즉시 활성화는 플레이 가능 범위가 넓지만 사용자 미결정을 숨기며, 삭제는 제안 추적성을 잃는다.
- (c) 권장: `planned` 행을 유지하고 각 도메인 UI에 미적용 효과 수를 명시하는 표시를 추가한다. SUG별 후속 plan에서 수치·수명·stack cap·OFF fallback을 결정한 뒤 작은 묶음으로 활성화한다.

### 14.3 정치·사회 선행 시스템

- (a) 현재: 정당성 50, 지지 0.5, 정치력 1/s는 SUG10이 공식을 미결로 남겨둔 중립 기술 기본값이다. `royal_authority`, `law_code`, `citizenship` 기술과 생활수준·이해집단이 core 데이터에 없어 일부 제도와 사건은 현재 해금될 수 없다.
- (b) 옵션: P04에서 신규 기술·생활수준까지 확장 / 기존 기술에 임시 매핑 / 해당 조건을 후속 경제·사회 plan으로 분리. 임시 매핑은 빠르지만 기술 의미를 왜곡하고 P03 정본을 조용히 바꾼다.
- (c) 권장: 임시 매핑하지 않는다. 신규 기술 도입 여부와 생활수준 공식을 별도 grill로 확정한 뒤 기술 CSV·시뮬레이션·UI를 함께 변경한다.

### 14.4 전체 런 저장·불러오기

- (a) 현재: meta save와 feature/module header는 영속화됐지만 core 자원·건물·기술 및 각 module 런 상태 payload는 아직 직렬화하지 않는다.
- (b) 옵션: P04에서 전체 run save까지 구현 / P04는 header와 meta만 완료하고 독립 저장 plan으로 분리 / 저장 기능을 계속 제외. 전체 구현은 P04 범위와 결합되지만 migration·transaction·복구 UI까지 필요하다.
- (c) 권장: 기존 P01~P03에 run save가 없으므로 별도 P05 저장 plan에서 core snapshot 복원 계약과 module별 payload version·migration을 함께 구현한다. P04에서는 header 불일치가 조용히 통과하지 않는 기반만 유지한다.

### 14.5 런타임 UI의 후속 상호작용

- (a) 현재: 핵심 command는 실행 가능하지만 유산 구매, 위인 배치 변경, 복수 국가 헌장, 이벤트 도감·삽화, 접근성 확률 표시, 세부 지원·저항 breakdown은 미구현이다.
- (b) 옵션: 하나의 범용 panel을 계속 확장 / 도메인별 독립 Base/Variant panel로 분리 / 세부 기능을 text-only 디버그 UI로 유지. 범용 panel은 빠르지만 행 정보가 복잡해지고 도메인별 panel은 편집성과 장기 확장성이 높다.
- (c) 권장: 현재 범용 panel은 조합 검증과 1차 플레이 경로로 유지하고, 각 미결 도메인 규칙이 확정될 때 독립 Base/Variant panel로 분리한다.

## §15 추가 결정: 임시 밸런스·매핑과 도메인별 패널 (2026-06-28)

사용자 결정에 따라 §14.1~§14.3과 §14.5의 미결 상태를 P04 안에서 다음과 같이 해소한다. 아래 수치는 모두 플레이 가능한 기준선을 만들기 위한 초기값이며, 정식 밸런스 라운드에서 재조정한다.

### 15.1 문명 유산 구매비용

- `legacy_perks.csv`가 단계별 비용 배열을 정본으로 소유한다.
- 5단계 일반 유산은 `3;6;12;24;48`, 3단계 유산은 `5;10;20`, 1단계 강력 유산은 효과 강도에 따라 `20` 또는 `30`을 초기값으로 사용한다.
- 구매 시 환생 포인트를 즉시 차감하고 rank를 영구 저장한다. 최대 rank, 비용 부족, 잘못된 ID는 구매를 거부한다.
- 유산 효과는 기존 효과 ID를 보존하면서 현재 구현 가능한 공통 수정자 또는 모듈 명령으로 임시 매핑한다.

### 15.2 149개 planned 효과의 초기 런타임 매핑

- 원래 설계 의미는 `effectType=planned`와 기존 `targetId`로 보존한다.
- 각 행에 `runtimeEffectType`, `runtimeTargetId`, `amount`, `duration`, `capGroup`을 명시한다. `runtime*` 필드는 정식 효과 시스템 도입 전의 임시 동작이며 원래 의미를 대체하지 않는다.
- 기간 `0`은 해당 소스가 유지되는 동안 영구 적용을 뜻한다. 선택형 이벤트·인물 능력처럼 일시 효과가 자연스러운 항목은 30~180초 범위의 초기값을 사용한다.
- `modifier_caps.csv`가 cap group별 최소·최대 합계를 단일 소스로 소유한다. 공통 modifier 원장은 동일 효과·대상·cap group의 합을 이 범위로 제한한다.
- 임시 매핑이 없는 planned 행, 존재하지 않는 cap group, 음수 duration은 데이터 검증 실패로 처리한다.

### 15.3 정치 공식과 기술·생활수준 임시 매핑

- `politics_rules.csv`가 정당성, 지지, 정치력 생산, 생활수준 공식의 초기 계수를 소유한다. 코드 상수는 이 파일을 참조한다.
- 생활수준은 기본값 + 인구 소비자원 평균 공급률 + 1인당 GDP + 국고 건전성으로 계산하고 0~100으로 제한한다.
- 아직 정식 기술이 없는 `royal_authority`, `law_code`, `citizenship`은 `technology_aliases.csv`에서 각각 현재 기술에 임시 연결한다. 원래 조건 ID는 유지하여 향후 정식 기술 추가 시 데이터 파일만 교체할 수 있게 한다.
- 정치 UI에는 정당성·정치력·생활수준·개혁 진행/저항을 함께 표시한다.

### 15.4 SUG06~SUG13 도메인별 플레이 UI

- 기존 단일 범용 action 목록을 8개 도메인 panel root로 분리한다. 각 panel은 고유 제목·상태 요약·스크롤·고정 row pool을 가진다.
- HUD에는 런타임 계층을 만들지 않고 생성기가 모든 panel과 row를 프리팹에 직렬화한다.
- 상위 module tab은 활성화된 기능만 표시하며, 선택된 도메인의 panel만 활성화한다.
- 초기 도메인 UI는 환생/유산, 도전과제, 시작 문명, 국가 수립, 정치·사회제도, 이벤트, 불가사의, 위인으로 구분한다. 각 도메인 renderer는 독립 메서드로 유지해 후속 전용 row prefab으로 교체할 수 있게 한다.

### 15.5 검증 기준

- 149개 planned 행 모두에 0이 아닌 초기 효과값, 유효한 duration, 존재하는 cap group, 지원되는 임시 런타임 매핑이 있어야 한다.
- 유산 비용 배열 길이는 `maxRank`와 같아야 하며 비용은 양수·단조 증가여야 한다.
- 기술 alias 대상은 실제 `technologies.csv` ID여야 한다.
- Base/Variant 생성 재실행 시 8개 도메인 panel과 row가 중복되지 않아야 한다.
- `ValidateData`, `GenerateUI`, `ValidateUI`, EditMode, PlayMode, Feature Matrix, MSBuild를 통과한 뒤 구현 완료 범위를 판단한다.

## §16 임시 밸런스·도메인 패널 구현 현황 (2026-06-29)

### 16.1 구현 완료 소스

- 문명 유산 15종에 단계별 구매비용을 추가하고 환생 포인트 차감·rank 영구 저장·다음 런 효과 적용 경로를 연결했다.
- 기존 planned 효과 149개 전부에 0이 아닌 초기값, 기간, cap group, `runtimeEffectType/runtimeTargetId` 임시 매핑을 추가했다.
- 공통 modifier 원장이 동일 cap group의 합계를 `modifier_caps.csv` 범위로 제한한다.
- 정치 기본식과 생활수준 계수를 `politics_rules.csv`로 이동하고, 생활수준을 인구 소비자원 공급률·1인당 GDP·국고 건전성으로 계산한다.
- `royal_authority`, `law_code`, `citizenship`은 `technology_aliases.csv`에서 기존 기술에 임시 매핑한다.
- planned 효과는 원래 설계 ID를 유지하면서 경제·정치·국가·이벤트·불가사의·위인 공통 수정자 채널에 임시 적용된다.
- 기존 단일 module scroll을 8개 도메인별 panel root와 각 18개 고정 row pool로 분리하도록 `UiPrefabGenerator`와 HUD view를 변경했다.
- 환생 유산 구매, 국가 설립, 개혁, 이벤트 선택, 불가사의 착공, 위인 영입·능력·배치 변경을 각 도메인 panel에서 수행하도록 연결했다.

### 16.2 현재 검증 상태

- CSV 정적 감사: planned 149, 0 값 0, 매핑 누락 0, 잘못된 cap 0, 음수 기간 0 PASS.
- 유산 비용 감사: rank 수 불일치 0, 0 이하 비용 0 PASS.
- 기술 alias 감사: 존재하지 않는 매핑 대상 0 PASS.
- 사용자가 Unity Editor를 정상 실행·종료한 뒤 Unity 프로세스 0개와 `Civic/Temp/UnityLockfile` 부재를 확인했다.
- `GenerateMainMenu`, `GenerateUI`, `ValidateMainMenu`, `ValidateData`, `ValidateUI` PASS.
- 생성된 `CivicHud_Base.prefab`에는 도메인 panel 8개와 고정 action row 144개가 있으며, 기존 단일 `ModuleActionRow`는 0개다.
- EditMode 56/56 PASS (`EditMode-20260629-030630.xml`).
- PlayMode 5/5 PASS (`PlayMode-20260629-030659.xml`).
- Feature Matrix 38 case PASS (`CIVIC_FEATURE_MATRIX_OK cases=38`).
- 전체 `dotnet msbuild Civic/Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true` PASS.
- Unity 로그의 라이선스 토큰 갱신 경고와 종료 시 `Curl error 42`는 남았지만 모든 action이 종료 코드 0과 해당 성공 marker를 반환했으며 생성·검증·테스트 결과에는 영향을 주지 않았다.
- 최종 상태는 Unity 프로세스 0개, `Civic/Temp/UnityLockfile` 없음이다.

### 16.3 MainMenu 카메라 결정

- `MainMenu.unity`는 사용자 편집 `CivicMainMenu` Variant의 인스턴스만 가진다. 카메라 GameObject는 없다.
- MainMenu Base Prefab의 Canvas는 `ScreenSpaceOverlay`(`m_RenderMode: 0`)이고 `worldCamera` 참조가 비어 있다. Overlay Canvas는 카메라 없이 렌더링되므로 현재 UI 전용 MainMenu에는 카메라가 필요하지 않다.
- Base/Variant에는 `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `EventSystem`, `InputSystemUIInputModule`이 있으며 `ValidateMainMenu`가 관련 직렬화 참조·씬 인스턴스·Build Settings를 검증한다.
- 이후 2D/3D 배경, 후처리, 카메라 애니메이션 또는 World Space/Screen Space Camera UI를 도입할 때만 MainMenu 카메라를 별도 plan에서 추가한다.

### 16.4 Unity 도구 실행 순서

Editor가 닫혀 있고 Unity 프로세스와 `Civic/Temp/UnityLockfile`이 없을 때 다음 순서를 사용한다.

1. MainMenu 생성기·씬·프리팹을 변경했다면 `GenerateMainMenu`.
2. HUD 생성기·직렬화 view를 변경했다면 `GenerateUI`.
3. `ValidateMainMenu`.
4. `ValidateData`.
5. `ValidateUI`.
6. `TestEditMode`.
7. `TestPlayMode`.
8. `FeatureMatrix`.
9. `dotnet msbuild Civic/Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true`.

생성기 변경이 없는 검증에서는 1~2단계를 생략할 수 있다. Editor가 열려 있으면 CLI 래퍼를 실행하지 않고 대응 메뉴와 Test Runner를 사용한다.

## §17 추가 결정: 공통 패널·게임 메뉴·모듈 정보 UX (2026-06-30)

### 17.1 사용자 요청과 관찰된 원인

- 자원·건물·기술 패널은 `CivicHudPanelMode`로 전환되지만 모듈 패널은 별도 `GameObject` 활성 상태를 소유해 서로를 닫지 않는다.
- 자원·건물·기술 패널에는 공통 닫기 버튼과 `None` 상태가 없고, 환생 모듈이 OFF면 MainMenu 복귀 명령도 없다.
- 불가사의·국가·위인·정치·도전과제 데이터에는 조건·비용·효과가 있지만 `CivicModulePanelView`가 일부 상태만 한 줄로 노출한다.
- 이벤트 선택은 모듈 목록 행에서 직접 처리하며, 이력은 event ID·choice ID·발생 시각만 보존해 실제 적용 결과를 설명할 수 없다.
- MainMenu에는 옵션 패널이 없으며 현재 영속 데이터는 `meta-progress.json`과 교체 저장 backup뿐이다. 전체 런 저장은 아직 구현되지 않았다.

### 17.2 공통 패널과 ESC 규칙

- 좌측 자원·건물·기술·모듈 패널은 하나의 상호배타 그룹으로 취급한다. 다른 패널을 열면 기존 패널은 즉시 닫힌다.
- 자원·건물·기술 패널에 공통 닫기 버튼을 추가하고 `CivicHudPanelMode.None`을 지원한다. 런 시작 시 기본 상태는 `None`이다.
- ESC 우선순위는 이벤트·디버그 같은 modal 닫기 → 열린 좌측 패널 닫기 → 게임 메뉴 popup 열기 순서다.
- 게임 메뉴 popup은 `계속 플레이`와 `메인 메뉴로`를 제공한다. MainMenu 이동은 현재 런을 저장하지 않고 종료하며 feature launch 상태를 초기화한다.

### 17.3 디버그 모드

- Backquote(`) 키로 런타임 디버그 패널을 열고 닫는다. 디버그 상태는 저장하지 않는다.
- 활성 모듈만 디버그 대상에 포함한다.
- 이벤트 강제 대기열 등록, 위인 강제 후보 등록, 국가 조건 강제 충족, 정치체계 강제 해금·테스트 비용 지급, 선택 문명으로 런 재시작을 지원한다.
- 디버그 명령은 일반 플레이 경로와 구분되는 명시적 API를 사용하고 테스트에서만 결과를 직접 단언한다.

### 17.4 모듈 조건·효과 표시

- 국가·불가사의·위인·정치체계는 미해금 상태도 목록에 유지하고 현재값/비교식/목표값을 tooltip에 표시한다.
- 불가사의는 착공 국고, 자원별 총 필요량·초당 납품량, 자원이 충분할 때의 예상 최소 시간, 유지비, 완공 효과를 표시한다.
- 위인은 전체 15명을 상태별로 표시하고 발견 조건, 특성, 능동 능력, 허용 배치, 현재 배치, 잔여 임기·사용 횟수를 표시한다. 활성 슬롯은 3개다. 현 구현에서 배치 ID는 효과 종류를 바꾸지 않고 같은 배치 중복 시 1명 100%, 2명 70%, 3명 이후 50%로 특성 효과가 감소한다.
- 정치체계는 카테고리 header 아래 그룹화한다. 각 카테고리는 정확히 1개만 활성화되며 서로 다른 카테고리는 동시에 하나씩 활성화된다. 현행·개혁 가능·미해금을 구분하고 해금 조건·비용·효과를 표시한다.
- 도전과제는 `달성 가능`, `이번 런 실패/불가능`, `필요 모듈 OFF`, `완료`를 구분한다.

### 17.5 도전과제 영구 보상

- 기존 도전과제 일부에 초기 영구 modifier 보상을 CSV로 추가한다.
- 보상은 최초 달성 즉시 적용하고 메타 진행에 완료 ID를 저장해 이후 런에도 적용한다. 도전과제 모듈이 OFF인 런에서는 해당 보상을 적용하지 않는다.
- 효과는 공통 modifier ledger와 cap group을 사용하며 초기값은 후속 밸런스 조정 대상이다.

### 17.6 이벤트 popup·알림·이력

- 새 이벤트가 queue에 들어오면 Victoria 3 계열과 유사한 modal popup을 자동으로 연다. 제목·설명·발생 원인·선택지·선택 효과를 표시한다.
- popup을 닫아도 이벤트는 queue에 남고 상단 이벤트 알림 버튼으로 다시 열 수 있다. 대기 이벤트가 기본 일시정지 대상이면 popup을 닫아도 해결 전까지 시뮬레이션은 정지한다.
- 모듈 패널은 대기 이벤트 요약과 해결 이력을 표시한다. 이력에는 이벤트명, 선택한 대응, 발생 시각, 실제 적용된 즉시값·modifier·기간을 저장한다.

### 17.7 MainMenu 옵션과 데이터 삭제

- MainMenu에 옵션 버튼과 옵션 패널을 추가한다.
- 세이브데이터 삭제는 2단계 확인 후 `meta-progress.json`, `.bak`, `.tmp`를 삭제하고 빈 메타 진행으로 교체한다.
- 전체 런 저장 파일은 현재 존재하지 않으므로 본 기능은 메타 진행만 삭제한다. 전체 런 저장·복원은 §14.4의 별도 후속 범위를 유지한다.

### 17.8 검증 기준

- 패널 상호배타, 공통 닫기, ESC 우선순위와 환생 모듈 OFF MainMenu 복귀를 PlayMode에서 검증한다.
- 모든 미해금 국가·불가사의·위인·정치체계가 정보와 조건을 포함해 렌더링되는지 EditMode/PlayMode에서 검증한다.
- 카테고리별 정치체계 하나만 활성화되고 다른 카테고리는 동시 활성 가능한지 단위 테스트한다.
- 영구 도전과제 modifier가 최초 달성 즉시, 다음 런, 모듈 OFF 조건에서 각각 올바르게 동작하는지 검증한다.
- 이벤트 popup 자동 표시·닫기·상단 알림 재호출·선택·결과 이력을 검증한다.
- 옵션의 메타 저장 삭제는 임시 디렉토리 store로 검증한다.
- `GenerateMainMenu`, `GenerateUI`, Feature Matrix, EditMode, PlayMode, MSBuild를 통과해야 한다.

## §18 공통 UX·정보 표시 구현 현황 (2026-06-30)

### 18.1 구현 완료

- 자원·건물·기술·모듈 패널을 상호배타로 연결하고 자원·건물·기술 공통 닫기 버튼과 `None` 상태를 추가했다.
- ESC로 modal 또는 좌측 패널을 우선 닫고, 열린 패널이 없으면 계속 플레이/MainMenu 이동 popup을 연다. 환생 모듈 OFF에서도 MainMenu로 돌아갈 수 있다.
- Backquote(`) 디버그 패널에서 활성 모듈의 이벤트 강제 발생, 위인 후보 등록, 국가 조건 충족, 정치체계 해금·비용 지급, 선택 문명 런 재시작을 지원한다.
- MainMenu 옵션 패널과 2단계 메타 세이브 삭제를 구현했다. JSON primary·backup·temporary 파일을 함께 삭제한다.
- 미충족 국가·문명·불가사의·위인·정치체계를 숨기지 않고 조건·비용·효과 tooltip을 제공한다.
- 불가사의별 착공 국고, 필요 자원·납품률·현재 납품량, 예상 최소 건축 시간, 유지비, 완공 효과를 표시한다.
- 위인 15명의 발견 조건·특성·능동 능력·배치 가능 위치·현재 배치·배치 중복 배율·3개 활성 슬롯을 표시한다.
- 정치체계를 5개 카테고리로 그룹화하고 그룹마다 정확히 1개 활성, 다른 그룹끼리는 동시 활성임을 header와 tooltip에 표시한다.
- 도전과제를 달성 가능/이번 런 불가능/필요 모듈 OFF/완료로 구분하고 10개 초기 영구 modifier 보상을 추가했다.
- 이벤트는 자동 modal popup, 닫기, 상단 알림 재호출, 선택지 효과 tooltip, 대기 목록, 선택·실제 적용 결과 이력을 제공한다. 이벤트 이력 panel은 64개 고정 row pool을 사용한다.

### 18.2 검증 결과와 잔여 검증

- checkpoint commit: `a713334` (`P04 임시 밸런스와 도메인 패널 구현`).
- Unity `Compile` PASS (`Compile-20260630-160438.log`).
- 전체 MSBuild PASS.
- `GenerateMainMenu` 및 내부 `ValidateMainMenu` PASS (`GenerateMainMenu-20260630-160649.log`).
- `GenerateUI` 및 내부 `ValidateData`·`ValidateUI` PASS (`GenerateUI-20260630-160706.log`).
- Feature Matrix 38 case PASS (`FeatureMatrix-20260630-160735.log`).
- 생성 산출물 정적 확인: MainMenu Options 1개, HUD DetailClose/Exit/Event/Debug 1개씩, 도메인 panel 8개, action row 총 202개.
- EditMode·PlayMode 자동 실행은 코드 실패가 아니라 Codex 외부 실행 승인 사용량 제한으로 시작 전에 거부됐다. 동일 명령을 우회하지 않았으며 Test Runner 실행이 잔여 검증이다.
- 최종 Unity 프로세스 0개, `Civic/Temp/UnityLockfile` 없음.

## §19 기술 스냅샷 성능·도메인 카드 UX 보정 (2026-06-30)

### 19.1 프로파일러 관찰과 원인

- Unity Profiler에서 `CivicGameSimulation.BuildTechnologySnapshot()`의 기술 효과 요약 생성이 한 프레임 누적 약 1672ms, 28회 호출로 관찰됐다.
- 기존 구현은 스냅샷을 갱신할 때 기술마다 전체 자원과 전체 건물을 다시 검색하고, 해금·건물·기술효과 열거자를 `Concat(...).ToArray()`로 병합한 뒤 동일한 한국어 요약 문자열을 반복 생성했다.
- 자원·건물·기술효과 정의는 런 중 불변이므로 프레임 단위 재계산이 필요하지 않다. 시뮬레이션 생성 시 자원·건물·효과 목록을 각각 한 번 순회해 기술 ID별 요약 사전을 만들고 이후 스냅샷은 같은 문자열 참조를 재사용한다.
- 회귀 기준은 스냅샷 갱신 전후 같은 기술의 `EffectSummary` 참조가 동일하고, Profiler의 프레임 경로에서 `ConcatNIterator.LazyToArray()`가 사라지는 것이다.

### 19.2 설명 카드 공통 구조

- 시작 문명·국가 설립·정치체계·불가사의·위인 행은 2단 카드로 만든다. 위쪽 진한 영역은 명칭·상태·행동 버튼, 아래쪽 연한 동일 폭 영역은 설명과 핵심 효과를 표시한다.
- 상세 조건·전체 비용·전체 효과 목록과 비활성 사유는 tooltip에 유지하되 핵심 설명과 효과를 카드에서 직접 읽을 수 있게 하여 tooltip 의존을 줄인다.
- 런타임에서 UI 계층을 만들지 않는다. `UiPrefabGenerator`가 모든 설명 영역, 정치 탭, 국가 필터를 Base Prefab의 고정 pool로 생성하고 view는 표시·텍스트·이벤트만 바인딩한다.

### 19.3 도메인별 표시 결정

- 시작 문명 패널은 런 도중 바꿀 수 없는 현재 문명 1개만 표시한다. 설명 카드에는 문명 설명, 시작 상태, 적용 효과를 표시한다.
- 정치·사회체계는 `정부 유형 / 유권자 권리 / 복지 정책 / 교육 정책 / 경제 체계` 탭으로 전환한다. 탭에는 그룹명과 현행 체계명을 함께 표시하고, 선택한 그룹의 체계만 목록에 표시한다. 그룹 header 행은 제거한다.
- 각 정치 그룹은 정확히 1개 체계가 활성이고 서로 다른 그룹은 동시에 활성 상태를 가진다는 기존 모델을 유지한다.
- 불가사의 카드는 개념 설명과 완공 효과를, 위인 카드는 직군 설명·상시 특성·능동 능력을, 국가 카드는 설명·설립 효과를 직접 표시한다.

### 19.4 위인 동작 의미

> **대체됨(2026-06-30):** 아래 3-slot·상시 특성·혼잡 배율 모델은 사용자 재결정에 따라 §20.2의 4개 직책 슬롯·미배치 무효·직책별 효과 모델로 대체한다. 과거 구현 의도 추적을 위해 원문은 보존한다.

- 활성 위인 슬롯은 3개다. 영입된 위인은 임기 동안 상시 특성을 제공하며 능동 능력을 사용하지 않아도 이 효과는 적용된다.
- 능동 능력은 상시 특성과 별개다. 사용하면 자원을 즉시 지급하거나 `personAbility` 소스의 추가 modifier를 정해진 기간 동안 적용하고 런별 사용 횟수를 차감한다.
- 배치 변경은 현재 효과 종류나 대상을 바꾸지 않는다. 같은 배치에 위인이 겹칠 때 상시 특성 배율을 첫 번째 100%, 두 번째 70%, 세 번째 이후 50%로 낮추는 혼잡 관리 요소다.
- 따라서 현재 배치 UI는 “어떤 부서에서 다른 효과를 발동”하는 시스템이 아니라 “같은 배치 중복을 분산해 특성 손실을 줄이는 선택”을 뜻한다.

### 19.5 이번 런 달성 불가 국가 필터

- 국가 설립 패널은 이번 런에서 절대로 충족할 수 없는 항목을 기본 숨김한다. `달성불가 조건의 문명·국가도 표시` 체크박스를 켜면 다시 표시한다.
- 오판으로 달성 가능한 국가를 숨기지 않도록 판정은 보수적으로 제한한다. 런 시작 후 변경할 수 없는 시작 문명 불일치, 런 시작 시 OFF인 필수 모듈, 이미 상한·정확값을 지나 되돌릴 수 없는 단조 metric만 달성 불가로 본다.
- 일반 GDP·인구·자원·건물·기술 목표처럼 이후 성장으로 충족 가능한 조건은 현재 미충족이어도 숨기지 않는다. 대안 그룹은 모든 대안이 달성 불가일 때만 해당 그룹을 달성 불가로 판정한다.

### 19.6 Tooltip 수명주기

- Tooltip 오브젝트는 프리팹에서 비활성으로 저장한다. 같은 비활성 오브젝트의 `Awake()`에서 다시 숨기면 최초 표시 시점에 `Awake → Hide`가 실행되어 첫 tooltip이 즉시 사라지므로 중복 초기화는 제거한다.
- tooltip trigger가 비활성화될 때 pointer 상태를 초기화하고 공용 tooltip을 즉시 숨긴다.
- 패널·이벤트 popup·게임 메뉴·디버그 popup을 닫거나 모듈 탭을 전환할 때도 공용 tooltip을 명시적으로 숨긴다. 따라서 pointer exit 이벤트가 발생하지 않는 비활성화 경로에서도 tooltip이 남지 않아야 한다.

### 19.7 검증 기준

- 기술 효과 요약이 스냅샷 간 같은 참조로 재사용되고 기존 요약 내용이 유지된다.
- 생성된 모든 모듈 행에 직렬화된 설명 영역이 있고, 정치 탭 8-slot pool과 국가 달성불가 필터 참조가 Validator를 통과한다.
- 시작 문명은 현재 문명 1개만, 정치는 선택 그룹만 표시한다.
- 시작 문명 불일치 국가가 기본 필터 대상이 되고 체크박스로 다시 표시된다.
- 설명 카드가 패널 폭을 넘지 않으며 스크롤에서 행 단위로 이동한다.
- 패널·popup 비활성화 후 tooltip이 남지 않는다.
- `GenerateUI`, `ValidateData`, `ValidateUI`, EditMode, PlayMode, MSBuild를 통과한 뒤 완료 여부를 판단한다.

### 19.8 구현·검증 결과

- 기술 효과 요약을 시뮬레이션 생성 시 자원·건물·기술효과 각 1회 순회로 일괄 인덱싱하고 스냅샷 간 같은 문자열 참조를 재사용하도록 변경했다.
- 시작 문명 1개 카드, 정치 5개 그룹 탭(프리팹 8-slot pool), 국가 달성불가 필터, 문명·국가·정치·불가사의·위인 설명 카드를 Base Prefab 생성기에 반영했다.
- Tooltip 첫 표시 시 `Awake → Hide`로 즉시 사라지던 기존 결함과 패널·popup 비활성화 후 잔존하던 수명주기 결함을 함께 수정했다.
- 첫 샌드박스 Unity 실행은 Licensing Client IPC 연결 실패로 중단했으며, Unity 프로세스 부재를 확인하고 stale `UnityLockfile`을 제거한 뒤 외부 실행으로 재시도했다.
- `GenerateUI` 및 내부 `ValidateData`·`ValidateUI` PASS (`GenerateUI-20260630-225843.log`).
- EditMode 61/61 PASS (`EditMode-20260630-225919.xml`).
- PlayMode 9/9 PASS (`PlayMode-20260630-230012.xml`). 현재 문명 단일 카드, 정치 탭 전환, 국가 필터, 설명 카드, tooltip 닫기 동작을 직접 단언한다.
- 전체 MSBuild PASS.
- 최종 Unity 프로세스 없음, `Civic/Temp/UnityLockfile` 없음.
- 실제 Editor Profiler의 프레임 시간은 사용자가 동일 시나리오로 다시 측정한다. 자동 테스트는 반복 스냅샷에서 기술 효과 요약 참조가 재사용되고 프레임 경로에 `ConcatNIterator.LazyToArray()`가 필요하지 않은 구조임을 검증한다.

## §20 디버그 도구·위인 직책 슬롯 보정 (2026-06-30)

### 20.1 디버그 패널 목적과 배치

- 기존 760×500 debug popup은 도메인·대상 selector, 설명, 실행 버튼이 같은 중앙 영역을 공유해 긴 설명 문자열이 selector와 겹친다.
- popup을 확대하고 `빠른 명령 → 즉시 처리 toggle → 도메인 selector → 대상 selector → 독립 설명 box → 실행 버튼` 순으로 수직 분리한다.
- 빠른 명령은 현재 해금된 저장형 자원 각각에 `+9999`, 모든 기술 직접 연구 완료, 환생 포인트 `+9999`를 제공한다. aggregate 자원은 직접 저장값이 아니므로 자원 지급 대상에서 제외한다.
- `사회구조/체계·국가·불가사의 즉시 처리` toggle은 debug session에서만 유지하며 저장하지 않는다. ON이면 정치체계는 선택 즉시 개혁 완료, 국가는 즉시 설립, 불가사의는 비용·조건과 무관하게 즉시 완공한다. OFF이면 기존 debug 준비 동작과 정상 플레이 시간을 유지한다.
- 불가사의를 debug 도메인 selector에 추가한다.

### 20.2 위인 상태와 직책 규칙

- `후보 → 영입됨/미배치 → 직책 배치 → 은퇴`를 명시적 상태 흐름으로 사용한다.
- 영입만 된 위인은 상시 modifier와 능동 능력을 적용할 수 없다. 직책에 배치된 동안에만 해당 인물·직책 조합의 효과가 활성화되고 능력 버튼이 활성화된다.
- 기본 직책은 `국가지도자`, `군사안보담당관`, `과학연구담당관`, `사회경제담당관` 4개이며 `person_positions.csv`가 ID·표시명·설명·순서·모듈 의존을 소유한다. 향후 다른 모듈은 같은 데이터 계약으로 직책을 추가할 수 있다.
- 한 직책에는 한 명만 배치할 수 있다. 이미 점유된 직책에 다른 위인을 배치하면 기존 위인은 영입됨/미배치 상태로 돌아가며 새 위인이 그 직책을 점유한다.
- 인물별 직책 효과는 `person_traits.csv`의 `positionId`가 단일 소스로 소유한다. 기존 인물 고유 효과는 의미에 맞는 직책으로 이동하고, 나머지 직책에는 직군별 초기 효과를 추가한다. 군사안보담당관은 군사 시스템이 없으므로 초기에는 정당성·개혁 저항·사건 안정성 계열의 임시 민정 효과를 사용하며 후속 군사 plan에서 교체한다.
- 은퇴 유산은 마지막으로 배치됐던 직책 효과만 10%로 남긴다. 미배치 상태로 은퇴하면 직책 유산 효과가 없다.
- 기존 `위인의 경쟁` 이벤트가 참조하던 같은 배치 중복 조건은 새 1직책 1인 규칙에서 성립할 수 없으므로, 서로 다른 직책에 위인 2명 이상이 배치된 조건으로 이전한다. 구형 `person.sameAssignmentPair` metric은 데이터 호환을 위해 0으로 유지한다.

### 20.3 위인 패널 UX

- 패널 최상단 4개 고정 직책 카드는 `직책명 · 배치 인물/공석`과 현재 적용 효과를 항상 표시한다. 배치 인물의 능력 버튼도 같은 카드에 두어 패널을 연 뒤 1회 클릭으로 자주 쓰는 능력을 사용할 수 있다.
- 그 아래 인물 카드는 발견·후보·영입·미배치·배치·은퇴 상태를 표시한다. 영입된 인물에는 4개 직책 버튼을 고정 생성하며, 버튼 1회로 배치 또는 직책 변경한다.
- 인물 카드의 `직책 해제`는 현재 배치를 해제한다. 점유된 직책 버튼 tooltip은 현재 점유자와 교체 결과를 설명한다.
- 프리팹 우선 원칙을 유지한다. `UiPrefabGenerator`가 위인 행마다 최대 4개의 직책 선택 버튼을 생성하고 런타임은 계층 생성 없이 표시·활성·tooltip만 바인딩한다.

### 20.4 검증 기준

- 영입 직후 modifier가 없고 능력 사용이 거부되어야 한다.
- 직책 배치 후 해당 인물·직책 효과만 적용되고, 직책 변경 시 이전 효과가 제거되어야 한다.
- 같은 직책에 다른 인물을 배치하면 기존 인물이 미배치되고 한 직책 한 명이 유지되어야 한다.
- 미배치 위인의 능력은 거부되고 배치 후에는 사용 가능해야 한다.
- 4개 직책 카드와 4개 직책 선택 버튼 pool이 생성·검증되어야 한다.
- debug 빠른 명령, 불가사의 target, 즉시 처리 toggle의 OFF/ON 경로를 EditMode·PlayMode에서 검증한다.
- `GenerateUI`, `ValidateData`, `ValidateUI`, EditMode, PlayMode, MSBuild를 통과해야 한다.

### 20.5 구현 현황

- debug popup을 980×720으로 확대하고 빠른 명령, 즉시 처리 toggle, selector, 설명 box, 실행 버튼 영역을 분리했다.
- 해금 자원 `+9999`, 모든 기술 연구, 환생 포인트 `+9999`, 정치·국가·불가사의 즉시 처리 경로를 구현했다.
- 4개 직책 데이터와 15명×4직책 초기 효과를 추가하고, 영입/미배치 상태에서는 modifier·능력 사용이 모두 차단되도록 변경했다.
- 패널 상단 직책 카드에서 현재 배치·효과·능력을 확인하고, 인물 카드의 4개 고정 버튼으로 1회 클릭 배치·교체가 가능하도록 생성기와 view를 확장했다.
- `위인의 경쟁` 이벤트 조건을 폐기된 같은 배치 중복 metric에서 `person.assignedCount >= 2`로 이전했다.
- 정적 검증에서 직책 4개, 인물 15명, 직책 효과 누락 0건, planned 효과 149개 유지, CSV 초과 열 0건, `git diff --check` 통과를 확인했다.
- `GenerateUI`, `ValidateData`, `ValidateUI` PASS. 갱신된 `CivicHud_Base.prefab`의 debug 빠른 명령·즉시 처리 toggle·위인 직책 버튼 직렬화 참조를 확인했다.
- EditMode 67/67 PASS (`EditMode-20260701-033935.xml`).
- 첫 PlayMode 실행은 빠른 명령 테스트에서 모든 기술 연구 후 자동 이벤트 popup이 발생해, 같은 테스트의 ESC 패널 우선순위 단언과 상태가 간섭하여 8/9로 실패했다. 제품의 이벤트 우선 ESC 규칙은 유지하고 빠른 명령 검증을 독립 테스트로 분리했다.
- 수정 후 PlayMode 10/10 PASS (`PlayMode-20260701-034435.xml`).
- `dotnet msbuild Civic/Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true` PASS.
