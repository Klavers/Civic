# P06 — 문명 선택·제도 다양화·탐색·접근성 (Umbrella)

> **Status**: planned
> **Baseline**: P04 모듈형 문명 시스템과 P05 UI·로컬라이제이션 기반
> **GitHub issue**: [#58 P06: 문명 선택·제도 다양화·탐색·접근성](https://github.com/Klavers/Civic/issues/58)
> **하위 plan 정본**: [P06a](./P06a_Localization_Glossary_planned.md), [P06b](./P06b_UI_Scale_Accessibility_planned.md), [P06c](./P06c_Domain_Hub_Navigation_planned.md), [P06d](./P06d_Random_Civilization_Start_planned.md), [P06e](./P06e_Low_Tier_Institutions_And_Civilization_Identity_planned.md)

## §0 Context

P04는 SUG06~SUG14의 선택형 모듈과 8개 도메인 패널을 구현했고, P05는 효과 문자열 로컬라이제이션과 Tooltip·modal·반복 입력을 정비했다. 그러나 시작 문명은 수동 순회만 가능하고 영구 해금·랜덤 시작이 없으며, 제도는 저티어 선택지가 부족하다. 모듈 기능은 단일 `모듈` 패널 아래에 몰려 있고, UI는 고정 크기라 접근성 배율을 제공하지 않는다. 개발자·AI가 새 문구를 작성할 때 참고할 다국어 용어 정본도 없다.

P06은 이 다섯 문제를 독립 하위 plan으로 분리한다. 총괄은 구현 순서와 교차 의존만 소유하며, 데이터 스키마·수치·UI 동작은 각 하위 plan을 정본으로 삼는다.

## §1 목표 (Goals)

1. 게임 전체 용어와 반복 문구의 한국어·영어 대응을 개발자용 CSV 사전으로 정본화한다.
2. MainMenu와 HUD 전체에 안전하게 적용되는 사용자 GUI 배율을 제공한다.
3. 모듈 기능을 플레이 맥락에 맞는 도메인 허브와 탭으로 재배치한다.
4. 영구 해금과 랜덤 우회를 분리한 시작 문명 선택 흐름을 만든다.
5. 기존 제도 모듈을 저티어부터 횡적 선택이 가능한 구조로 고도화하고 문명별 고유 제도를 연결한다.
6. 모든 신규 기능에서 SUG14의 런 시작 전 ON/OFF와 런 중 불변 계약을 보존한다.

## §2 비목표 (Non-goals)

- 이번 계획 작성 단계에서 게임 코드·prefab·게임 데이터 CSV를 수정하지 않는다.
- 런 도중 모듈 hot toggle, 규칙 preset, 메타 기반 모듈 해금은 도입하지 않는다.
- P06에서 전체 UI를 TMP로 전환하지 않는다.
- 용어사전을 런타임 또는 `ValidateData` 입력으로 사용하지 않는다.
- provisional 문명·제도 수치를 최종 밸런스로 확정하지 않는다.
- 50~200% 전 구간을 모든 해상도에서 완전 반응형으로 보장하지 않는다.

## §3 하위 plan과 단일 소스

| 순서 | Plan | 정본 책임 | 선행 관계 |
|-|-|-|-|
| 1 | P06a | 용어 concept·번역 대응·작성 절차 | 없음 |
| 2 | P06b | GUI 배율 단계·안전 복원·로컬 설정 | P06a 용어 사용 |
| 3 | P06c | 국가·진행·프로젝트·인물 허브와 OFF 표시 | P06b 배율·스크롤 계약 |
| 4 | P06d | 수동 해금·랜덤 추첨·재현·문명 bootstrap | P06a 표시 용어, P06c 시작 문명 탭 |
| 5 | P06e | 공통 저티어 제도·문명별 고유 제도 | P06c 국가/제도 탭, P06d 시작 정체성 |

- 모듈 ID·ON/OFF·Feature Matrix: [SUG14](../suggestion/SUG14_모듈형_기능_토글과_조합.md) 정본.
- 시작 문명 특성·밸런스 원칙: [SUG08](../suggestion/SUG08_환생_시작_문명.md)과 P06d 정본.
- 제도 그룹·개혁 과정: [SUG10](../suggestion/SUG10_정치와_사회_체계.md)과 P06e 정본.
- runtime localization FORMAT: [LOCALIZATION_FORMAT.md](../localization/LOCALIZATION_FORMAT.md) 정본.

## §4 구현 단계 (Steps)

1. P06a의 사전 파일과 AGENTS 참조 절차를 먼저 도입한다.
2. P06b의 공용 UI 배율 service와 안전 overlay를 MainMenu·HUD에 연결한다.
3. P06c에서 기존 모듈 패널을 도메인 허브로 이관하고 Feature Matrix별 탐색을 검증한다.
4. P06d에서 기존 문명 bootstrap 불일치를 고친 뒤 영구 해금과 랜덤 시작을 활성화한다.
5. P06e에서 공통 저티어 제도와 모든 문명의 고유 제도를 provisional 콘텐츠로 추가한다.
6. 각 하위 plan은 별도 PR로 구현·검증하고 umbrella 이슈에 상태를 동기화한다.

## §5 검증 (Verification)

- [ ] P06a~P06e가 각자의 정본 범위를 재정의 없이 참조한다.
- [ ] 각 하위 PR이 `ValidateData`, 관련 prefab 검증, Feature Matrix, EditMode, PlayMode, MSBuild를 통과한다.
- [ ] Baseline과 각 모듈 단독·pairwise·AllOn에서 기존 경제·기술 동작이 보존된다.
- [ ] 구현·검증이 끝난 항목만 이슈와 plan 체크박스에 반영한다.

## §6 리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| UI 배율과 탐색 개편 동시 변경 | prefab 회귀 범위 확대 | P06b를 먼저 병합하고 P06c에서 해당 기준을 사용 |
| 랜덤 문명이 기존 bootstrap 오류를 노출 | 런 시작 실패 | 모든 문명 초기화 테스트 통과 전 랜덤 활성 금지 |
| 문명 전용 제도 16종의 밸런스 편향 | 특정 문명 상위호환 | provisional 표시, 장점·약점 예산, 후속 정식 밸런스 이슈 |
| 용어 YAML과 사전의 책임 혼동 | 단일 소스 충돌 | 사전은 작성 참고, YAML은 runtime 표시 정본으로 구분 |

## §7 후속 (Follow-up)

- [#54 문명 도감과 시작 문명 특성·고유 제도 상세 비교 UI](https://github.com/Klavers/Civic/issues/54)
- [#55 문명·전용 제도 provisional 수치와 플레이버 정식 밸런스](https://github.com/Klavers/Civic/issues/55)
- [#56 랜덤 재추첨의 런 준비 고정·환생 포인트 비용 정책 활성화](https://github.com/Klavers/Civic/issues/56)
- [#57 50~200% 전 구간 완전 반응형 UI 지원](https://github.com/Klavers/Civic/issues/57)
