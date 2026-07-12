# P06b — 전체 GUI 배율과 안전 복원

> **Status**: planned
> **Parent**: [P06 Umbrella](./P06_Player_Choice_Governance_Navigation_Accessibility_Umbrella_planned.md)
> **GitHub issue**: [#50 P06b: 전체 GUI 배율과 안전 복원](https://github.com/Klavers/Civic/issues/50)
> **관련 이슈**: [#48 전체 UI TextMeshPro 전환 검토](https://github.com/Klavers/Civic/issues/48)

## §0 Context

현재 HUD와 MainMenu는 1920×1080 기준 고정 크기와 Legacy `Text`를 주로 사용하고 Tooltip만 TMP다. 글자 크기만 변경하면 고정 슬롯에서 겹치므로 글자·버튼·여백·패널을 함께 바꾸는 전체 GUI 배율이 필요하다.

## §1 목표 (Goals)

1. MainMenu와 HUD에 동일한 GUI 배율을 즉시 적용한다.
2. 위험 배율에서 사용자가 설정 UI를 잃지 않도록 확인·자동복원한다.
3. 설정을 기기 로컬에 저장하고 다음 실행에 복원한다.

## §2 비목표 (Non-goals)

- 월드 카메라와 게임 simulation 해상도를 변경하지 않는다.
- 전체 UI TMP 전환을 선행조건으로 삼지 않는다.
- 모든 해상도와 50~200% 전 구간을 완전 반응형으로 보장하지 않는다.

## §3 동작 계약

- 지원 단계: `50, 75, 80, 90, 100, 110, 125, 150, 200%`; 기본값 `100%`.
- 1920×1080의 `80~125%`에서 모든 기능 접근과 레이아웃을 보장한다.
- 보장 범위 밖이거나 현재 해상도의 logical safe area보다 panel이 클 것으로 예상되면 적용 전 경고한다.
- 위험 배율 적용 후 15초 안에 `유지`하지 않으면 이전 배율로 복원한다.
- 확인·복원 UI는 사용자 배율과 분리된 최고 sorting order의 prefab safety canvas를 사용한다.
- MainMenu 옵션과 게임 내 ESC 옵션에서 즉시 적용한다.
- 기기 로컬 설정 service가 percent를 저장하며 메타 진행·런 저장과 분리한다.
- Legacy `Text`와 `TMP_Text`는 같은 Canvas 배율을 따르며 개별 font size를 재작성하지 않는다.

## §4 구현 단계 (Steps)

1. 배율 값·저장·preview/confirm/revert를 제공하는 공용 UI scale service를 만든다.
2. MainMenu와 HUD CanvasScaler에 사용자 배율 multiplier를 연결한다.
3. 독립 safety canvas와 15초 countdown popup을 생성·검증한다.
4. MainMenu 옵션과 ESC 옵션에 동일 선택 UI를 연결한다.
5. panel·Tooltip·modal 경계와 ScrollRect를 배율별로 검증한다.

## §5 검증 (Verification)

- [ ] 9개 값만 선택할 수 있고 재실행 후 유지된다.
- [ ] MainMenu와 HUD에서 즉시 적용되며 월드 카메라는 변하지 않는다.
- [ ] 15초 미확인, ESC, 씬 전환에서 이전 값이 복원된다.
- [ ] safety popup은 50%와 200%에서도 접근 가능하다.
- [ ] 1920×1080의 80·90·100·110·125%에서 주요 panel·modal·Tooltip이 잘리지 않는다.

## §6 리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| 200%에서 panel이 logical canvas보다 큼 | 기능 접근 불가 | 경고·자동복원·ScrollRect, 완전 반응형은 후속 |
| 저장값 손상 | 시작 즉시 UI 접근 불가 | 허용 목록 외 값은 100% fallback |
| Canvas별 배율 불일치 | Tooltip·modal 위치 어긋남 | 공용 service와 prefab Validator |

## §7 후속 (Follow-up)

- [#57](https://github.com/Klavers/Civic/issues/57)에서 50~200% 전 구간의 완전 반응형 레이아웃을 추적한다.
