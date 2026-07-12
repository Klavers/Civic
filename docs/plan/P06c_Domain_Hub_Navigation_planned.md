# P06c — 모듈 도메인 허브와 사이드바 탐색

> **Status**: planned
> **Parent**: [P06 Umbrella](./P06_Player_Choice_Governance_Navigation_Accessibility_Umbrella_planned.md)
> **GitHub issue**: [#51 P06c: 모듈 도메인 허브와 사이드바 탐색](https://github.com/Klavers/Civic/issues/51)
> **Feature 계약**: [SUG14](../suggestion/SUG14_모듈형_기능_토글과_조합.md)

## §0 Context

현재 좌측 사이드바는 `자원/건물/기술/국가/모듈`로 구성되고, SUG06~SUG13 기능은 `모듈` 패널의 8개 탭에 모여 있다. 플레이 맥락과 무관한 단일 모듈 허브는 자주 쓰는 국가·제도·위인·불가사의 기능의 접근성을 낮춘다.

## §1 목표 (Goals)

1. 기존 버튼을 재사용하고 신규 버튼을 최소화하면서 모듈 기능을 도메인별로 재배치한다.
2. OFF 모듈을 숨기되 기본 국가 개요와 core 패널을 보존한다.
3. 작은 화면과 GUI 확대에서 모든 버튼에 접근할 수 있게 한다.

## §2 비목표 (Non-goals)

- MainMenu의 모듈 ON/OFF 방식을 변경하지 않는다.
- 런타임에서 button, tab 또는 panel GameObject를 생성하지 않는다.
- 아이콘 전용 사이드바나 아트 제작을 도입하지 않는다.

## §3 탐색 매핑

- `자원`, `건물`, `기술`: 기존 core panel 유지.
- `국가`: 기존 버튼을 허브로 확장.
  - `국가 개요`는 항상 표시.
  - `시작 문명`, `국가 설립`, `제도`는 해당 모듈 ON일 때만 표시.
- `진행`: 기존 `모듈` 버튼을 이름·역할 변경.
  - `환생·유산`, `도전과제`, `이벤트 이력` 탭.
  - 세 모듈이 모두 OFF이면 버튼 자체를 숨김.
- `프로젝트`: 신규 버튼, 불가사의 모듈 ON일 때만 표시.
- `인물`: 신규 버튼, 위인 모듈 ON일 때만 표시.
- 사이드바 button pool은 세로 `ScrollRect`에 두고 보이는 항목만 재배치한다.
- 모든 좌측 panel은 상호 배타적이며 다른 버튼을 열면 기존 panel을 닫는다.
- panel별 마지막 선택 탭은 런 동안 유지하며 숨겨진 탭이면 첫 표시 가능 탭으로 fallback한다.

## §4 구현 단계 (Steps)

1. 공용 panel route와 feature-aware tab definition을 도입한다.
2. `UiPrefabGenerator`에서 고정 sidebar button pool과 hub tab pool을 생성한다.
3. 기존 모듈 action row와 상태를 새 허브로 이관하고 중복 접근 경로를 제거한다.
4. 닫기·ESC·다른 panel 전환·탭 보존을 공용 controller에서 처리한다.
5. Feature Matrix별 표시 버튼·탭을 검증한다.

## §5 검증 (Verification)

- [ ] Baseline에서 자원·건물·기술·국가 개요만 정상 접근된다.
- [ ] 단독·pairwise·AllOn에서 정확한 버튼과 탭만 표시된다.
- [ ] 빈 진행 허브와 OFF 프로젝트·인물 버튼이 남지 않는다.
- [ ] 다른 panel을 열면 기존 panel과 Tooltip이 닫히고 ESC 우선순위가 유지된다.
- [ ] 125% GUI 배율과 1920×1080에서 sidebar ScrollRect로 모든 버튼에 접근한다.

## §6 리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| 기존 module action 연결 누락 | 기능 버튼 무반응 | feature/action route 데이터 기반 회귀 테스트 |
| 국가 개요와 국가 설립 명칭 혼동 | 탐색 의미 불명확 | 탭 명칭을 용어사전과 일치 |
| OFF 조합에서 빈 panel 노출 | SUG14 fallback 위반 | Feature Matrix UI 검증 |

## §7 후속 (Follow-up)

- 아이콘 에셋이 준비되면 텍스트+아이콘 사이드바 전환을 별도 검토한다.
