# 문서 타입 구분 가이드

`plan/` 내 문서는 4가지 타입으로 나뉜다. 각 타입별 작명·구조·용도가 다르다.

## 1. P## — 시스템/기능 플랜

**용도**: 실제 구현을 목표로 하는 기술 플랜. 머지 대상.

**파일명**: `P##_Descriptive_Name_planned.md`
- `##` = 2자리 숫자 (기존 최대 번호 + 1 자동 부여)
- 연속 번호가 원칙이나 서브플랜은 alphabet suffix 허용: `P12a`, `P12b`

**서브플랜 패턴**:
- 상위 `P12` = umbrella(총괄)
- 하위 `P12a` / `P12b` / `P12c` = 각 서브 범위
- 각각 독립 PR 가능하나 의존 순서 존재

## 2. S## — 제안/사이드카

**용도**: 정식 구현 예정이 확정되지 않은 제안·사이드카·탐색 문서. 승인 시 P## 로 격상되기도 함.

**파일명**: `S##_Descriptive_Name_planned.md`
- `##` = 일반적으로 P## 번호 뒤를 따라감
- 보류 상태면 `_deferred_planned` 혼합 suffix 허용

**P## 격상 조건**:
- 사용자가 정식 구현 승인
- 격상 시 suffix는 여전히 `_planned`, P-prefix로 rename

## 3. CPR — Cross-Plan Review

**용도**: 여러 플랜 간 결정 충돌·용어 불일치·미결 항목 교차 검토. 자체는 구현하지 않고 다른 플랜의 **패치 지시서** 역할.

**파일명**: `CPR_<scope>_<title>_<suffix>.md`
- `<scope>`: 검토 대상 플랜 범위 (예: `P12-P15`, `P20`)
- `<title>`: 라운드·주제 (`initial`, `round2`, `followup`, `implementation_audit`)
- `<suffix>`: 상태 (`_planned` / `_done` / `_absorbed`)

**라운드 누적 금지**: 2라운드 이상이면 새 파일. 단일 파일 누적 불가.

**구조**: `cpr.md` 템플릿 참조. §4.4 미결 섹션 필수.

## 4. Umbrella (총괄)

**용도**: 여러 서브플랜의 상위 문서. 공통 설계·인벤토리·용어를 정의.

**파일명**: `P##_Descriptive_Name_Umbrella_planned.md`

**구조**: `plan_umbrella.md` 템플릿 참조.

## 문서 타입 선택 flowchart

```
신규 플랜 요청
  ├─ 여러 서브플랜을 묶는 상위 문서인가? → P##_..._Umbrella_planned.md
  ├─ 정식 구현 확정 플랜인가? → P##_..._planned.md
  ├─ 제안·탐색 단계인가? → S##_..._planned.md
  ├─ 여러 기존 플랜의 교차 검토인가? → CPR_<scope>_<title>_<suffix>.md
  └─ 기존 플랜 보류인가? → S##_..._deferred_planned.md
```

## 자주 하는 실수

1. ❌ 서브플랜을 S##로 신설 (서브플랜은 P##+alphabet suffix가 맞음)
2. ❌ CPR을 구식 형식으로 작명 (현 규약은 `CPR_<scope>_<title>_<suffix>.md`)
3. ❌ `_done` 파일을 `plan/` 루트에 둠 (→ `plan/done/`으로 이동)
4. ❌ Silent 중복: 하위 플랜에 상위 단일 소스의 enum/수식 다시 정의
