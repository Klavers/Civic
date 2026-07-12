# P06d — 영구 해금과 랜덤 시작 문명

> **Status**: planned
> **Parent**: [P06 Umbrella](./P06_Player_Choice_Governance_Navigation_Accessibility_Umbrella_planned.md)
> **GitHub issue**: [#52 P06d: 영구 해금과 랜덤 시작 문명](https://github.com/Klavers/Civic/issues/52)
> **문명 설계 정본**: [SUG08](../suggestion/SUG08_환생_시작_문명.md)

## §0 Context

현재 MainMenu는 16개 문명을 이전/다음 버튼으로 순회하고 `requiredFeatureIds`가 꺼진 문명만 시작을 차단한다. 수동 해금 상태와 랜덤 시작은 없다. 또한 `civilization_start.csv`는 `resource/building/technology`를 포함하지만 runtime bootstrap은 `resource`만 처리해 일부 문명 시작이 실패할 수 있다.

## §1 목표 (Goals)

1. 수동 영구 해금과 랜덤 해금 우회를 분리한다.
2. 현재 feature 조합에서 정상 작동할 수 있는 모든 문명을 seed 기반으로 추첨한다.
3. 기존 16개 문명 bootstrap을 먼저 정상화한다.
4. 문명 간 상위호환을 막는 장점·약점·난도 계약을 테스트 가능한 형태로 기록한다.

## §2 비목표 (Non-goals)

- 랜덤 추첨이 꺼진 모듈을 자동 활성화하지 않는다.
- 첫 구현에서 재추첨 비용이나 런 준비 고정 정책을 플레이어에게 제공하지 않는다.
- 반복 환생 포인트 배율로 고난도 문명을 보상하지 않는다.
- 상세 문명 도감·3열 비교 UI는 구현하지 않는다.

## §3 데이터·상태 계약

- 시작 방식: `Manual`, `Random`.
- 재추첨 정책 확장점: `Free`, `LockedPerSetup`, `PrestigeCost`; P06d의 활성 정책은 `Free`.
- 랜덤 pool은 기본 문명을 포함한 전체 정의 중 현재 resolved feature set의 `requiredFeatureIds`를 만족하는 문명이다.
- `requiredFeatureIds`는 runtime 호환 조건이며 영구 해금 조건이 아니다.
- 수동 선택 미해금 문명은 표시하되 시작 버튼을 비활성화하고 해금 조건과 `랜덤에서는 등장 가능`을 안내한다.
- 해금 방식은 `default`, `achievement`, `prestigePurchase`, `achievementThenPrestigePurchase`를 지원한다.
- 영구 해금 결과는 meta progress에 저장하며 이후 관련 모듈을 OFF해도 유지한다.
- run launch state는 요청 방식, seed, 실제 추첨 문명 ID를 기록한다.

### 문명 밸런스 계약

- 문명은 핵심 강점·보조 강점·실질 약점·고유 시작 요소를 가진다.
- 고난도 문명은 초반 불리함 뒤 조건부 대체 전략을 열 수 있지만 저난도 문명의 모든 전략을 지배하지 않는다.
- 고난도 추가 보상은 최초 전용 도전과제의 영구 보상만 허용한다.

## §4 구현 단계 (Steps)

1. `civilization_start.csv`의 `resource/building/technology`를 bootstrap 전용 경계에서 모두 처리한다.
2. 시작 건물은 일반 건설비·인구 제한을 적용하지 않고 초기 상태로 배치하며, 시작 기술의 시대 탭 비율 규칙을 명시한다.
3. 문명 해금 정의와 meta 영구 상태를 추가한다.
4. MainMenu에 수동 잠금 표시와 랜덤 결과 확인·무료 재추첨 흐름을 생성한다.
5. 추첨 seed와 resolved ID를 런 시작 계약에 저장한다.

## §5 검증 (Verification)

- [ ] 16개 문명을 각각 새 런으로 초기화해 예외와 누락 start 항목이 없다.
- [ ] 미해금 문명은 수동 선택 불가지만 랜덤 pool에는 포함된다.
- [ ] 꺼진 필수 모듈이 필요한 문명은 랜덤 pool에서 제외되고 이유를 진단할 수 있다.
- [ ] 같은 seed·feature set은 같은 문명을 선택한다.
- [ ] 무료 재추첨, 확정, 수동 복귀가 게임 진입 전에 작동한다.
- [ ] 영구 해금이 저장·재실행·관련 모듈 OFF 뒤에도 유지된다.

## §6 리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| CSV start kind와 runtime 불일치 | 특정 랜덤 결과에서 런 실패 | 전체 문명 초기화 테스트를 release gate로 사용 |
| 무료 재추첨이 빠른 수동 선택이 됨 | 랜덤 도전성 약화 | 초기 사용성 우선, 후속 정책 이슈로 고정·비용형 추적 |
| feature 조합에 따라 pool 크기 변화 | 확률 이해 어려움 | 추첨 전 후보 수와 제외 사유 표시 |

## §7 후속 (Follow-up)

- [#54 문명 도감·상세 비교 UI](https://github.com/Klavers/Civic/issues/54)
- [#56 `LockedPerSetup`과 `PrestigeCost` 재추첨 정책 활성화](https://github.com/Klavers/Civic/issues/56)
- [#55 문명·전용 제도 정식 밸런스 패스](https://github.com/Klavers/Civic/issues/55)
