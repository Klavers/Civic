# P06a — 개발자·AI용 다국어 용어사전

> **Status**: planned
> **Parent**: [P06 Umbrella](./P06_Player_Choice_Governance_Navigation_Accessibility_Umbrella_planned.md)
> **GitHub issue**: [#49 P06a: 개발자·AI용 다국어 용어사전](https://github.com/Klavers/Civic/issues/49)
> **Runtime localization 정본**: [LOCALIZATION_FORMAT.md](../localization/LOCALIZATION_FORMAT.md)

## §0 Context

현재 runtime localization은 strict YAML과 `effect.<effectType>` key를 사용하지만, 문명·제도·이벤트 설명은 도메인 CSV에 있고 버튼·상태 문구 일부는 C#·생성기에 존재한다. 새 기능이나 번역을 작성할 때 `국고/재정`, `영입/고용`처럼 동일 개념을 여러 표현으로 쓰는 일을 막을 개발자·AI 참고 사전이 필요하다.

## §1 목표 (Goals)

1. 게임 전체 concept, UI 동사, 상태 표현, 반복 문구를 stable ID로 정리한다.
2. 한국어와 영어의 권장어·지양어·예문을 한 행에서 비교할 수 있게 한다.
3. AGENTS와 localization 작성 절차에서 사전 선확인을 요구한다.

## §2 비목표 (Non-goals)

- 게임 runtime, `ValidateData`, 자동 번역 또는 YAML 생성에 CSV를 연결하지 않는다.
- 별도 writing-style CSV를 만들지 않는다.
- 번역되지 않은 언어를 AI 임시 번역으로 채우지 않는다.

## §3 데이터 계약

- `docs/localization/glossary_concepts.csv`
  - `conceptId,entryType,domain,definitionKo,usageContext,sourceReference,notes`
  - `entryType`: `term`, `phrase`, `uiAction`, `status`.
- `docs/localization/glossary_translations.csv`
  - `conceptId,koPreferred,koAvoid,enPreferred,enAvoid,koExample,enExample`
  - 한 concept는 정확히 한 행이며 두 파일은 `conceptId`로 대응한다.
- FORMAT·token·concept link 문법은 기존 `LOCALIZATION_FORMAT.md`만 정본으로 유지한다.
- 실제 문장 스타일은 runtime YAML과 도메인 콘텐츠의 승인 문장을 사례로 참조한다.

## §4 구현 단계 (Steps)

1. 현재 YAML, 도메인 CSV, 생성기·UI 문자열에서 핵심 concept inventory를 만든다.
2. 한국어·영어 권장어와 지양어를 작성한다.
3. 반복 문구와 버튼·상태 표현을 별도 `entryType`으로 추가한다.
4. AGENTS와 `LOCALIZATION_FORMAT.md`에 작성 전 확인 절차를 연결한다.

## §5 검증 (Verification)

- [ ] 두 CSV의 `conceptId`가 중복되지 않고 상호 누락이 없다.
- [ ] `treasury`, `construction_power`, `institution`, `reform`, `unlock`, `recruit` 등 현재 핵심 용어가 포함된다.
- [ ] 한국어·영어가 한 행에서 비교되고 지양어와 예문이 구분된다.
- [ ] runtime 코드와 Validator가 두 CSV를 읽지 않는다.

## §6 리스크 (Risks)

| 리스크 | 영향 | 완화 |
|-|-|-|
| 사전과 실제 문구가 수동으로 어긋남 | 참고자료 신뢰 하락 | 문구 변경 PR에서 사전 확인·필요 시 동시 갱신 |
| 언어 열 증가 | CSV 가로 폭 확대 | 실제 번역 착수 언어만 열 추가 |

## §7 후속 (Follow-up)

- 언어 수가 크게 늘면 concept master + 언어별 파일 전환 여부를 별도 검토한다.
