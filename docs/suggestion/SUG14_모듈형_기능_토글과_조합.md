# SUG14 — 모듈형 기능 토글과 조합

> 상태: 제안
> 정본 범위: SUG06~SUG13의 모듈 ID, 의존·상호배타 연산자, OFF fallback, 기능 토글 방식
> 연계: [SUG06](./SUG06_환생과_문명_유산.md), [SUG07](./SUG07_도전과제와_메타_보상.md), [SUG08](./SUG08_환생_시작_문명.md), [SUG09](./SUG09_문명과_국가_설립.md), [SUG10](./SUG10_정치와_사회_체계.md), [SUG11](./SUG11_조건부_이벤트와_사건_연쇄.md), [SUG12](./SUG12_불가사의와_거대_건축.md), [SUG13](./SUG13_위인과_인물_라이프사이클.md)

## 1. 제안 목적

SUG06~SUG13의 기능을 한 덩어리로 구현하지 않고 독립 모듈과 조합 모듈로 분리한다. 플레이어·기획자·테스트가 필요한 기능만 활성화할 수 있어야 하며, 두 기능이 동시에 켜졌을 때만 의미가 생기는 연계 기능과 서로 함께 사용할 수 없는 상보적 기능도 명시적으로 처리해야 한다.

핵심 목표는 다음과 같다.

- 신규 시스템을 모두 꺼도 현재 P03 경제·기술·UI 동작이 그대로 유지된다.
- A 또는 B만 켜도 해당 기능이 독립적으로 작동한다.
- A와 B를 함께 켰을 때만 C 또는 추가 연계 D가 자동 활성화된다.
- A와 충돌하는 E는 설정 단계에서 차단하고 이유와 대안을 제시한다.
- 모듈 조합별 저장·불러오기·UI·테스트 범위를 재현할 수 있다.

## 2. 항상 활성화되는 현재 게임 기반

다음 기반은 기능 토글 대상이 아니다.

| 기반 | 유지 이유 | 신규 모듈이 허용되는 접근 |
|-|-|-|
| 경제 시뮬레이션 | 자원, 건물, 생산·소비, 가격, GDP의 정본 | modifier와 command를 통한 확장 |
| 시대·기술 | 해금과 시대 진행의 정본 | 조건 조회와 기술 효과 등록 |
| 데이터 로더·검증 | CSV 참조 무결성의 정본 | 모듈별 데이터 source 등록 |
| snapshot·resource flow | UI 표시와 테스트의 정본 | sourceType이 있는 추가 흐름 제공 |
| prefab UI 기반 | 사용자 편집 가능 UI 계약 | 미리 생성된 module slot 활성화 |
| 기본 HUD·건설·연구 | 모든 신규 기능 OFF 기준선 | 기존 직렬화 참조·동작 변경 금지 |

신규 모듈은 기반 코드를 직접 대체하지 않는다. 비활성 상태에서는 등록하지 않은 modifier, 빈 command handler, 숨겨진 prefab panel, Null Object service를 사용해 기존 경로가 그대로 실행되게 한다.

## 3. 모듈 상태와 의존 연산자

### 3.1 상태

| 상태 | 의미 | UI 처리 |
|-|-|-|
| `Unavailable` | 빌드·데이터에 기능이 없거나 선행 feature 미충족 | 숨김 또는 잠금 |
| `Available` | 사용할 수 있으나 현재 profile에서 OFF | OFF 표시 |
| `Enabled` | 사용자가 요청한 상태 | ON 표시 |
| `ResolvedEnabled` | 의존성 계산 후 실제 활성 | 기능 노출 |
| `Blocked` | 충돌·의존 누락으로 활성 불가 | 사유와 해결 버튼 |

저장 파일에는 사용자가 선택한 `Enabled` 목록과 resolver가 확정한 `ResolvedEnabled` 목록을 함께 기록한다.

### 3.2 연산자

| 연산자 | 의미 | 예시 |
|-|-|-|
| `requiresAll` | 지정 기능이 모두 ON이어야 활성 | 도전과제 환생 보상은 도전과제+환생 필요 |
| `requiresAny` | 대체 기능 중 하나 이상 필요 | 국가 설립은 시작 문명 또는 기본 정체성 provider 필요 |
| `extendsWhenAll` | 각 기능은 독립 작동하고 함께 ON일 때 추가 adapter 활성 | 이벤트+정치 → 개혁 사건 |
| `conflictsWith` | 동시에 활성화할 수 없음 | 환생 초기화와 연속 캠페인 모드 |
| `fallbackTo` | 연계 모듈이 없을 때 단순 동작으로 대체 | 이벤트 OFF일 때 위인 은퇴를 고정 결과로 처리 |
| `recommends` | 없어도 작동하지만 함께 사용을 권장 | 국가 설립은 정치 모듈 권장 |

resolver는 순환 의존, 존재하지 않는 ID, 충돌하는 자동 활성화를 시작 전에 오류로 보고해야 한다.

## 4. SUG06~SUG13 모듈 분류

### 4.1 독립 도메인 모듈

| 모듈 ID | 출처 | 책임 | OFF일 때 fallback |
|-|-|-|-|
| `meta.prestige` | SUG06 | 런 기록 점수, 환생 가능 여부, 초기화 command | 환생 버튼·메타 점수 없음, 연속 현재 게임 |
| `meta.legacyPerks` | SUG06 | 환생 포인트 소비와 영구 유산 modifier | 환생 포인트는 기록만 하거나 숨김 |
| `progress.achievements` | SUG07 | 조건 추적, 완료 기록, 기본 장식 보상 | 도전 패널·추적 없음 |
| `progress.permanentRewards` | SUG07 | 도전과제 영구 modifier 지급 | 도전은 배지·기록만 제공 |
| `identity.startCivilizations` | SUG08 | 새 런의 시작 문명 선택·특성·시작 상태 | `default` 문명 하나를 자동 사용 |
| `identity.nationFormation` | SUG09 | 국가 후보·준비·헌장·현재 국가 modifier | 시작 정체성을 런 종료까지 유지 |
| `governance.politics` | SUG10 | 제도, 정당성, 정치력, 현재 정책 | 기존 경제 규칙만 사용 |
| `governance.reforms` | SUG10 | 정책 변경 진행·지지·저항 | 정책을 시작 기본값으로 고정 |
| `narrative.events` | SUG11 | event scheduler, 선택, history, 기간 효과 | 확률·선택 사건 없음 |
| `projects.wonders` | SUG12 | 불가사의 해금·납품·건설·완공 효과 | 일반 건물만 사용 |
| `people.greatPeople` | SUG13 | 후보 생성, 슬롯, 배치, 상시 특성 | 인물 modifier 없음 |
| `people.lifecycle` | SUG13 | 임기, 은퇴·이탈, 런 유산 | 활성 인물을 고정 임기 없는 조언자로 처리 |

### 4.2 조합 adapter 모듈

| adapter ID | 활성 조건 | 추가 기능 | 한쪽만 ON일 때 |
|-|-|-|-|
| `integration.achievementPrestige` | `progress.achievements` + `meta.prestige` | 도전 완료 환생 포인트 | 도전은 기본 보상, 환생은 경제 기록만 계산 |
| `integration.achievementPermanentRewards` | `progress.achievements` + `progress.permanentRewards` | 영구 modifier 지급 | 도전 기록만 또는 영구 보상 source 없음 |
| `integration.prestigeCivilizations` | `meta.prestige` + `identity.startCivilizations` | 환생 화면 문명 선택·해금 | 환생은 default 시작, 문명은 일반 새 게임에서 선택 |
| `integration.civilizationNations` | `identity.startCivilizations` + `identity.nationFormation` | 출신 문명 전용 후계 국가 | 국가는 default 정체성 기반 범용 후보만 제공 |
| `integration.nationPolitics` | `identity.nationFormation` + `governance.politics` | 국가 헌장·정부형태·정책 설립 조건 | 국가는 고정 modifier, 정치는 국가 비연계 기본 정책 |
| `integration.reformEvents` | `governance.reforms` + `narrative.events` | 개혁 25/50/75% 사건 | 개혁은 고정 속도, 이벤트는 경제·기술 사건만 제공 |
| `integration.wonderEvents` | `projects.wonders` + `narrative.events` | 건설 단계·완공 사건 | 불가사의는 고정 진행, 이벤트는 불가사의 조건 제외 |
| `integration.peopleEvents` | `people.greatPeople` + `narrative.events` | 경쟁·위기·은퇴 사건 | 인물은 정해진 후보·임기 결과, 이벤트는 인물 조건 제외 |
| `integration.wonderPeople` | `projects.wonders` + `people.greatPeople` | 위인 프로젝트 배치·마지막 설계도 | 불가사의와 위인이 각자 독립 작동 |
| `integration.fullLegacyCampaign` | SUG06~SUG13 도메인 전체 | 전체 연대기·종합 도전·문명 방주 결말 | 숨김, 개별 기능은 유지 |

조합 adapter는 사용자가 직접 체크하지 않는다. resolver가 필요한 도메인 모듈이 모두 `ResolvedEnabled`일 때 자동 활성화한다.

## 5. 의존·조합·상호배타 시나리오

### 5.1 A+B일 때만 C

- A=`progress.achievements`
- B=`meta.prestige`
- C=`integration.achievementPrestige`

| A | B | 결과 |
|-|-|-|
| OFF | OFF | 도전·환생 모두 없음 |
| ON | OFF | 도전과제와 배지 작동, 환생 포인트 보상 없음 |
| OFF | ON | GDP·인구·시대·기술 기반 환생 작동, 도전 점수 없음 |
| ON | ON | 각 기능과 도전과제 환생 포인트 adapter 모두 작동 |

### 5.2 A와 B 독립 + 동시 활성 D

- A=`governance.reforms`
- B=`narrative.events`
- D=`integration.reformEvents`

A만 ON이면 개혁 진행은 고정 지지·저항 계산으로 작동한다. B만 ON이면 경제·기술 사건만 발생한다. 둘 다 ON이면 개혁 단계 사건, 반대·타협 선택, 사건에 따른 진행도 변화가 추가된다.

### 5.3 A와 함께 사용할 수 없는 E

- A=`meta.prestige`의 `strictReset` 모드
- E=`campaign.continuousHistory` 모드

`strictReset`은 환생 시 경제·기술·국가 상태를 초기화하지만 `continuousHistory`는 동일 세계 상태를 유지한 채 시대만 확장한다. 저장 계약과 목표가 상반되므로 `conflictsWith`로 동시에 선택하지 못하게 한다. UI는 현재 선택을 끌지, 대체 모드로 바꿀지 명시적으로 묻는다.

### 5.4 requiresAny 대체 provider

`identity.nationFormation`은 `identity.startCivilizations` 또는 항상 제공되는 `identity.defaultProvider` 중 하나를 요구한다. 시작 문명 모듈이 OFF여도 default 정체성으로 범용 국가를 설립할 수 있어 국가 시스템이 불필요하게 종속되지 않는다.

### 5.5 fallback 연계

`people.lifecycle`이 ON이고 `narrative.events`가 OFF이면 은퇴·이탈은 고정 threshold와 선택 없는 결과로 처리한다. 이벤트가 ON이면 같은 lifecycle 상태에 사건 선택만 확장한다. 이벤트 OFF가 인물 상태를 멈추거나 저장을 깨뜨려서는 안 된다.

## 6. 기능을 켜고 끄는 방식 5가지

| 방식 | 적용 시점·사용자 | 장점 | 단점 | 영향도·비용 |
|-|-|-|-|-|
| 1. 프로젝트 전역 Feature Catalog | 개발자가 Unity asset에서 빌드에 포함할 모듈 지정 | 가장 단순하고 누락 asset을 조기 검증 | 런별 선택 불가, build variant 증가 | 영향 중간, 구현비 낮음 |
| 2. 새 게임·환생 고급 설정 | 플레이어가 런 시작 전에 모듈별 ON/OFF | 원하는 복잡도와 조합을 직접 선택 | 잘못된 조합과 선택 피로 가능 | 영향 높음, 구현비 중간 |
| 3. 규칙 preset/profile | `기본`, `메타 진행`, `정치 서사`, `전체` 같은 묶음 선택 | 초보자도 안전한 조합 사용, 테스트 재현 쉬움 | 세부 조정 자유도 감소 | 영향 낮음, 구현비 낮음 |
| 4. 메타 진행에 따른 단계 해금 | 환생·도전·시대 기록에 따라 모듈 선택권 개방 | 복잡도가 점진적으로 증가 | 원하는 기능 접근이 늦고 강제 진행으로 느껴질 수 있음 | 영향 높음, 구현비 중간 |
| 5. 개발·테스트 Feature Matrix | Editor/CLI profile로 단독·쌍·전체 조합 실행 | 회귀 원인 분리와 CI 자동화에 최적 | 일반 플레이어용 UI가 아님 | 영향 낮음, 구현비 중간 |

### 권장안

한 가지 방식만 선택하지 않고 계층형으로 결합한다.

1. Feature Catalog가 빌드에 존재하는 기능의 상한을 정한다.
2. preset이 안전한 기본 조합을 제공한다.
3. 고급 설정에서 preset을 복제해 런별 override를 허용한다.
4. 메타 해금은 기능의 `Available` 여부만 제어하고 강제 ON하지 않는다.
5. 개발·CI는 같은 profile schema를 사용해 조합을 재현한다.

권장 기본 profile인 `Baseline`은 SUG06~SUG13 도메인을 모두 OFF로 둔다. 기존 P03 저장·시뮬레이션·HUD 회귀 테스트는 항상 이 profile로 실행한다.

## 7. 런 중 토글 정책

기능을 아무 때나 켜고 끄면 이미 생성된 상태와 modifier를 제거해야 하므로 기본적으로 금지한다.

| 정책 | 대상 | 처리 |
|-|-|-|
| `StartOnly` | 환생, 문명, 국가, 정치, 이벤트, 불가사의, 위인 | 새 게임·환생 시작 화면에서만 변경 |
| `SafePauseToggle` | 알림, 도움말, 이벤트 자동 일시정지 같은 표현 기능 | 일시정지 중 즉시 변경 |
| `DeveloperRestartRequired` | 개발 profile 변경 | 상태 폐기 후 새 런 시작 |
| `MigrationRequired` | 저장 중인 기능 제거 | 명시적 migration이 없으면 로드 차단 |

모듈 OFF는 단순 GameObject 비활성화가 아니다. 해당 모듈의 command, tick participant, modifier provider, event source, save state, UI panel을 모두 resolver 결과에 따라 등록하지 않아야 한다.

## 8. 기존 게임을 보존하는 구현 경계

### 8.1 시뮬레이션

- 기존 `Advance(seconds)`와 1초 tick 순서를 바꾸지 않는다.
- optional system은 `ICivicTickParticipant`의 명시된 phase에 등록한다.
- 기존 자원·건물 계산을 수정하는 대신 `ICivicModifierProvider`로 가산·곱·상한 modifier를 제공한다.
- 비활성 모듈 목록은 tick 중 조회하지 않고 런 시작 시 resolved registry를 만든다.
- modifier에는 sourceType, sourceId, capGroup을 남겨 자원 상세와 테스트가 출처를 확인할 수 있게 한다.

### 8.2 데이터

- core CSV는 optional module ID를 필수 참조하지 않는다.
- optional CSV가 core ID를 참조하는 단방향 의존만 허용한다.
- `ValidateAllDefinitions`는 프로젝트에 포함된 모든 데이터의 정적 오류를 검사한다.
- `ValidateProfile`은 선택 profile의 의존·충돌·필수 asset·fallback을 검사한다.
- disabled module 데이터는 런 snapshot과 UI 후보에 포함하지 않는다.

### 8.3 UI

- 런타임에서 UI 계층을 생성하지 않는다.
- `UiPrefabGenerator`가 module panel slot과 공통 toggle row prefab을 만든다.
- 활성 profile에 따라 저장된 panel GameObject와 navigation button만 활성화한다.
- 모듈 OFF 시 빈 탭·빈 알림·Missing Script가 남지 않도록 validator가 검사한다.
- 사용자 Variant는 생성기가 덮어쓰지 않는 기존 계약을 유지한다.

### 8.4 저장

- 저장에 profileId, requestedFeatures, resolvedFeatures, moduleVersion을 기록한다.
- 로드 시 현재 build에 없는 필수 모듈이 있으면 자동 삭제하지 않고 migration 또는 명시적 실패를 제공한다.
- 모듈 OFF 상태로 저장된 런에 숨은 기본 상태를 임의 생성하지 않는다.
- 조합 adapter 상태는 재계산 가능 데이터만 저장하고 중복 modifier를 직렬화하지 않는다.

## 9. UI 제안

- 새 게임·환생 화면에 `게임 시스템` 단계를 추가한다.
- 기본 화면은 preset 4개만 보여주고 `고급 설정`에서 모듈 트리를 연다.
- 각 행에 ON/OFF, 출처 SUG, 복잡도, 저장 영향, 필요한 기능, 충돌 기능을 표시한다.
- A를 켤 때 필요한 B가 OFF이면 `B도 켜기`, `A 취소`, `대체 fallback 사용`을 제시한다.
- A와 E가 충돌하면 자동으로 한쪽을 끄지 않고 비교 대화상자로 선택받는다.
- 조합 adapter는 별도 토글 대신 `A+B 활성화로 다음 연계 기능이 켜짐`이라는 읽기 전용 목록에 표시한다.
- 시작 전 `Resolved Feature Summary`에서 실제 활성 기능과 비활성 fallback을 확인한다.

## 10. 데이터 모델 제안

| 데이터 | 주요 필드 | 역할 |
|-|-|-|
| `CivicFeatureCatalog.asset` | module definitions, profile assets, build inclusion | 프로젝트 정본 진입점 |
| `feature_modules.csv` | id, category, sourceSuggestion, defaultState, runtimePolicy, moduleVersion | 도메인 모듈 정의 |
| `feature_dependencies.csv` | moduleId, relationType, targetIds, autoResolvePolicy, fallbackId | 의존·충돌 정본 |
| `feature_profiles.csv` | profileId, displayNameKo, enabledModuleIds, lockedModuleIds | preset 정의 |
| `feature_unlocks.csv` | moduleId, conditionType, targetId, value | 메타 Available 조건 |
| `CivicFeatureSelection` | profileId, requestedIds, userOverrides | 사용자 요청 |
| `CivicResolvedFeatureSet` | enabledIds, adapterIds, blockedReasons, fallbacks | 런 불변 결과 |
| `CivicModuleSaveHeader` | moduleId, moduleVersion, payloadVersion | 저장 호환성 |

모듈 ID와 관계 연산자는 본 SUG14가 정본이다. P04와 후속 하위 plan은 literal 목록을 재정의하지 않고 본 문서를 참조한다.

## 11. 테스트 전략

### 필수 profile

- `Baseline`: 신규 모듈 전체 OFF, 현재 게임 회귀
- `EachModule`: 각 도메인 모듈 단독 ON
- `DeclaredDependency`: requiresAll/requiresAny/fallback/conflict 사례
- `Pairwise`: 도메인 모듈 2개 조합의 pairwise set
- `AllOn`: SUG06~SUG13과 모든 adapter 활성

### 검증 항목

- resolver 결과가 입력 순서와 무관하고 deterministic한지
- 충돌·누락·순환 관계가 명시적 오류인지
- 모듈 OFF 시 core snapshot이 Baseline golden 결과와 같은지
- 한쪽만 ON일 때 독립 기능과 fallback이 작동하는지
- 양쪽 ON일 때 adapter가 정확히 1회 등록되는지
- 저장 round-trip 후 requested/resolved set과 module state가 같은지
- profile 변경 저장을 migration 없이 로드하지 않는지
- UI panel·navigation·tooltip·직렬화 참조가 profile별로 유효한지
- `AllOn`에서 modifier source와 capGroup이 중복 적용되지 않는지

전체 조합 2^N을 모두 수동 작성하지 않는다. 선언된 관계의 진리표 테스트와 pairwise 생성, AllOn 통합 테스트를 함께 사용한다.

## 12. 플레이 경험 장단점

### 장점

- 플레이어가 원하는 복잡도와 메타 시스템만 선택할 수 있다.
- 개발자는 신규 기능을 단독 구현·검증한 뒤 안전하게 조합할 수 있다.
- 기능 OFF가 기존 게임으로 돌아가는 명확한 회귀 기준이 된다.
- preset과 메타 해금을 통해 초보자에게 복잡도를 점진적으로 공개할 수 있다.

### 단점 및 완화

- 조합 수 폭증: 선언형 관계와 pairwise 테스트로 줄인다.
- OFF 조합의 서사 공백: 각 모듈에 명시적 fallback과 숨김 규칙을 둔다.
- 저장 호환성 복잡성: 런 시작 고정과 moduleVersion header를 사용한다.
- UI 설정 피로: preset 우선, 고급 설정은 선택적으로 연다.
- 밸런스 분산: Baseline·권장 preset·AllOn만 공식 밸런스 대상으로 우선 관리한다.

## 13. 추가 결정 필요

- 일반 플레이어에게 고급 토글을 처음부터 공개할지 메타 해금할지
- 공식 밸런스·도전과제 유효 profile의 범위
- `continuousHistory` 같은 상보적 campaign mode를 P04 범위에 포함할지
- moduleVersion 불일치 시 migration 실패 UX
- profile 공유·내보내기 기능과 플랫폼 업적 허용 기준

## 14. P04 1차 구현 결정 (2026-06-28)

- 기능을 켜고 끄는 방식 중 `2. 새 게임·환생 고급 설정`과 `5. 개발·테스트 Feature Matrix`만 구현한다.
- `1. 프로젝트 전역 Feature Catalog`, `3. 규칙 preset/profile`, `4. 메타 진행에 따른 단계 해금`은 구현하지 않는다.
- 모든 모듈 코드는 동일 build에 포함하고 runtime registry가 SUG06~SUG13의 고정 module definition과 integration 관계를 제공한다.
- 플레이어 선택은 메인 메뉴의 모듈 설정에서 런 시작 전에 수행하고, 시작 후에는 immutable resolved feature set으로 고정한다.
- 개발·테스트는 같은 resolver API로 Baseline, 단독 모듈, pairwise, AllOn case를 생성한다. Build Settings를 조합마다 변경하지 않는다.
- SUG06~SUG13의 실제 도메인 기능이 구현되기 전에는 토글이 선택·의존성·런 전달 기반만 제공한다. 실제 gameplay 효과는 각 도메인 module 구현 시 같은 ID에 연결한다.
- 외부 DLL·사용자 mod 자동 발견과 hot reload는 범위 밖이다. 이번 메뉴는 build에 포함된 모듈을 DLC/모드처럼 활성화하는 방식이다.
- `CivicFeatureCatalog.asset`, gameplay preset 데이터, meta unlock 데이터는 생성하지 않는다.

## 15. P04 SUG14 1차 구현 결과 (2026-06-28)

- `CivicFeatureRegistry`가 SUG06~SUG13에 대응하는 상위 모듈 8종과 조합 integration ID를 같은 build 안에서 제공한다.
- `CivicFeatureResolver`는 requested set의 누락 의존·충돌을 진단하고, 필요한 모듈이 모두 ON일 때만 integration을 활성화한다.
- `CivicFeatureRuntime`은 메인 메뉴 선택 또는 `-civicFeatures` 명령행 입력을 받아 런 시작 시 resolved set을 불변으로 잠근다. `SampleScene` 직접 실행은 모든 신규 기능 OFF Baseline으로 시작한다.
- `CivicMainMenu` Base Prefab과 사용자 Variant, `MainMenu.unity`를 생성했다. 사용자는 새 게임 전에 8개 모듈을 DLC/모드처럼 개별 ON/OFF할 수 있다.
- 개발·테스트 Feature Matrix는 Baseline 1개, 단독 8개, pairwise 28개, AllOn 1개로 총 38개 case를 같은 resolver로 검증한다.
- 실제 환생·도전·문명·국가·정치·이벤트·불가사의·위인 gameplay는 아직 연결하지 않았다. 메뉴에도 이 제한을 표시하며 각 도메인 구현 시 같은 module ID에 동작을 등록한다.
- Unity 검증 결과: Compile, GenerateMainMenu, ValidateMainMenu, FeatureMatrix, ValidateData, GenerateUI, ValidateUI, EditMode 27/27, PlayMode 2/2 및 MSBuild가 통과했다.
