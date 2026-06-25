# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

> **상태**: Unity 기반 신규 idle 게임을 준비하는 초기화 단계. 게임 콘셉트·방향성은 추후 `grill-me`를 통해 확정한다. Unity 기술 기반과 프리팹 UI 제작 규약은 즉시 적용된다.

## Project summary

신규 idle 게임 프로젝트다. 현재는 기존 게임을 제거한 저장소 초기화 단계이며, 구체적인 게임 콘셉트와 방향성은 아직 확정하지 않았다.

## Commands

- Unity 버전 정본: `Civic/ProjectSettings/ProjectVersion.txt`
- 프로젝트 열기: Unity Hub에서 저장소의 `Civic/` 폴더 선택
- CLI 래퍼: `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action <Compile|GenerateUI|ValidateUI|ValidateData|TestEditMode|TestPlayMode>`
- 열린 Editor에서 UI 생성: `Tools > Civic > UI > Generate`
- 열린 Editor에서 UI 검증: `Tools > Civic > UI > Validate`
- 열린 Editor에서 데이터 검증: `Tools > Civic > Data > Validate`

현재 기준선(2026-06-23)은 CLI 생성·검증, EditMode·PlayMode 통과, 열린 Editor에서 Generate 2회 재실행·Validate 대화상자·`SampleScene` 업그레이드 기능 수동 확인까지 완료된 상태다. 이후 UI·씬·생성기 변경은 해당 검증을 다시 수행한다.

### Unity Editor 상태별 작업 절차

**권장: 이미 열려 있는 Editor를 그대로 사용한다.** 동일 프로젝트에 두 번째 Unity 프로세스를 실행하면 `Library`와 프로젝트 잠금이 충돌할 수 있으므로 Editor 실행 중에는 `Invoke-Unity.ps1`을 호출하지 않는다.

1. **열린 Editor에서 메뉴·Test Runner 사용 (권장)**
   - AI는 C#·asmdef·문서·생성기 소스를 수정하고, 열린 Editor의 자동 import/compile을 이용한다.
   - Editor가 열려 있는 동안 `.unity`, `.prefab`, `.asset` 직렬화 파일을 외부에서 직접 패치하지 않는다. 씬·프리팹 변경은 Editor 메뉴와 생성기를 통해 수행해 미저장 상태 덮어쓰기와 Asset Database 충돌을 피한다.
   - 프리팹 생성·검증은 각각 `Tools > Civic > UI > Generate`, `Tools > Civic > UI > Validate`로 실행한다.
   - EditMode·PlayMode 테스트는 `Window > General > Test Runner`에서 실행한다.
   - 장점: 프로젝트 잠금 충돌이 없고 현재 Editor 상태를 유지한다. 단점: 메뉴와 테스트 실행에는 사용자 조작이 필요하다. 영향도·비용: 낮음.
2. **저장 후 Editor 종료, CLI 래퍼 사용**
   - 자동화 검증이 필요하면 씬과 에셋을 저장하고 Editor를 종료한 뒤 `scripts/Invoke-Unity.ps1`을 실행한다.
   - 장점: 생성·검증·테스트를 자동화하고 로그를 남길 수 있다. 단점: 현재 Editor 작업이 중단된다. 영향도·비용: 중간.
3. **격리된 임시 프로젝트 복사본에서 CLI 사전검증**
   - 원본 Editor를 유지해야 하면 `Assets`, `Packages`, `ProjectSettings`와 필요한 `.meta`를 임시 디렉토리에 복사해 정확한 Unity 버전으로 compile/test한다. `Library`, `Temp`, `Logs`, `UserSettings`는 복사하지 않는다.
   - 이 방식은 코드 컴파일과 생성기·테스트의 사전검증용이다. 임시 복사본에서 생성한 프리팹·씬을 원본에 덮어쓰지 말고, 최종 생성·검증은 원본 Editor 메뉴에서 다시 수행한다.
   - 장점: 원본 Editor를 닫지 않고 자동 검증할 수 있다. 단점: 복사 시간과 디스크 사용량이 늘며 원본의 현재 미저장 상태는 검증하지 못한다. 영향도·비용: 높음.

Editor가 닫혀 있고 `Civic/Temp/UnityLockfile`이 없을 때만 원본 프로젝트에 CLI 래퍼를 사용한다. `UiPrefabBootstrap`은 프리팹이 없을 때 최초 생성을 보조하지만, 기존 Base 갱신과 명시적 검증은 위 메뉴 또는 CLI 절차로 수행한다. 사용자 편집 Variant는 어느 경로에서도 덮어쓰지 않는다.

### UI 생성·검증 도구 수명주기

`Generate`는 여러 번 실행할 수 있어야 한다. 매 실행 시 AI 관리 Base Prefab은 현재 생성기 코드로 갱신하고, 기존 사용자 Variant는 재사용하며, 동일 이름의 자식·필수 컴포넌트와 `SampleScene`의 `UIRoot`를 중복 생성하지 않는다. `Validate`는 Base/Variant 존재·타입·상속 관계, Missing Script, 직렬화 참조, Canvas와 입력 EventSystem 구성을 검사한다. Editor 메뉴 실행 시 성공·실패 대화상자를 표시하고, 자동 실행에서는 로그와 종료 코드로 판정한다.

도구 정리 방식은 다음 세 가지로 구분한다.

1. **핵심 Generator·Validator 유지 (권장)**
   - `UiPrefabGenerator`, `UiPrefabValidator`, `UiPrefabBootstrap`과 관련 테스트를 저장소에 유지한다.
   - 장점: AI 코드 변경을 프리팹에 반복 반영하고 회귀 검증할 수 있다. 단점: Editor 전용 코드가 계속 남는다. 영향도·비용: 낮음.
2. **일회성 Editor 도구만 검증 후 삭제**
   - 임시 도구는 `Assets/_Project/Editor/OneShot/<TaskName>/`에 두고, 생성 대상·완료 조건·삭제 대상을 대응 plan에 기록한다.
   - 사용자가 메뉴를 실행한 뒤 생성 에셋, Console 성공 로그, Validator, 관련 EditMode·PlayMode 테스트를 확인한다. 사용자 완료 확인 후 호출부를 검색하고 해당 `.cs`·`.meta`·전용 테스트와 임시 상태 파일만 명시적으로 삭제한다. 삭제 후 Editor 재컴파일과 전체 검증을 다시 수행한다.
   - 장점: 완료된 마이그레이션 코드를 남기지 않는다. 단점: 재실행이 필요하면 도구를 복원해야 한다. 영향도·비용: 중간.
3. **핵심 Generator까지 폐기하고 Prefab을 정본으로 전환**
   - 별도 plan에서 Prefab 수동 관리 전환을 승인받은 경우에만 수행한다. `Invoke-Unity.ps1`, Bootstrap, Validator, 테스트의 참조를 함께 제거·대체한 뒤 Prefab과 `.meta`를 검증한다.
   - 장점: Editor 생성 코드가 사라진다. 단점: 이후 AI가 UI 구조를 안전하게 재생성하기 어렵고 수동 편집 비용이 커진다. 영향도·비용: 높음.

핵심 생성기는 단순히 “한 번 실행했다”는 이유로 삭제하지 않는다. 현재 UI처럼 지속적으로 코드 변경을 Prefab에 반영하는 도구는 1안을 적용하고, 독립적인 일회성 변환 도구에만 2안의 확인 후 삭제 절차를 적용한다.

PowerShell의 bare `bash`는 WSL 실행기를 선택할 수 있으므로 사용하지 않는다. Git Bash는 `C:\Program Files\Git\bin\bash.exe`, 다음으로 `C:\Program Files\Git\usr\bin\bash.exe`를 탐색하며 Git Bash 세션 안에서만 현재 `bash`를 사용한다.

## Architecture

- Unity 프로젝트 루트: `Civic/`
- 런타임 UI: `Civic/Assets/_Project/Runtime/UI/`
- Editor 생성·검증 도구: `Civic/Assets/_Project/Editor/UI/`
- 프리팹: `Civic/Assets/_Project/Prefabs/UI/` (`Generated/`는 AI 관리 Base, 상위 폴더는 사용자 편집 Variant)
- 테스트: `Civic/Assets/_Project/Tests/EditMode/UI/`, `Civic/Assets/_Project/Tests/PlayMode/UI/`

## Plan-driven workflow

이 리포는 문서 주도(plan-driven)로 운영한다. `P##_Name_*.md` / `S##_*.md` / `CPR_<scope>_*.md` 계획 파일 + 접미사 상태(`_planned`/`_done`/`_revised_done`/`_canceled`/`_superseded`/`_absorbed`)를 사용한다. 계획 문서는 `docs/plan/` 에 둔다.

- **plan 문서 신설·패치·suffix 이관**: skill `plan-doc` 사용.
- 비자명한 변경 전 관련 `P##` 문서 선독. 문서가 없으면 먼저 plan 작성이 프로젝트 관례.
- 정본 문서(있는 경우)가 영향받으면 **동일 PR 에 동기 갱신 커밋을 포함**한다 — 별도 PR 로 미루지 않는다.

## Conventions specific to this repo

Unity 프로젝트 고유 규약:

- **프리팹 우선 UI.** 런타임에서 `new GameObject`, `AddComponent`, `Awake`/`Start` 기반으로 UI 계층·레이아웃을 만들지 않는다. 저장된 프리팹 인스턴스화와 데이터·이벤트 바인딩만 허용한다.
- **Base/Variant 소유권.** `Prefabs/UI/Generated/` Base는 Editor 생성기가 갱신할 수 있다. 사용자가 편집하는 Variant는 생성기가 기존 파일을 덮어쓰지 않는다.
- **Editor 생성 경계.** 프리팹 생성 코드는 Editor assembly에만 둔다. Runtime assembly는 `UnityEditor`를 참조하지 않는다.
- **Unity 직렬화.** `.meta` 파일을 항상 함께 커밋하고 Force Text·Visible Meta Files 설정을 유지한다.
- **Git LFS 미사용.** 바이너리 에셋도 일반 Git으로 추적하며 `.gitattributes`에 LFS filter를 추가하지 않는다.

공통 규약:

- **함수 제거 금지 원칙.** 계획에 명시되지 않았다면 기존 함수/로직을 조용히 삭제하지 말고, 호출부를 grep으로 먼저 확인한 뒤 마이그레이션 경로를 남긴다.
- **단일 소스 원칙.** 수치·enum·수식은 정본 위치를 하나만 두고, 다른 문서·코드는 참조한다 (silent 중복 금지).

## User collaboration style

- **Selected-text 비평.** ExitPlanMode 거부 시 사용자는 Codex가 쓴 특정 문장을 선택·인용하며 수정 지시할 수 있다. 거부 메시지에 quote 블록이 있으면 그 문장만 **정확히 치환**한다. 전면 재작성 금지.
- **권장안 명시.** 중립적 옵션 나열 지양. "Option A / B / C" 제시 시 반드시 **"권장: B, 이유: …"** 동반.
- **다안 비교 제안.** 방법·지향·순서를 문의·검토·제안할 때는 **3안 이상** + 각 안의 장점·단점·영향도·수행비용 비교 + 권장안(근거 동반)을 제시한다. 단일안·2옵션 나열 지양.
- **확정 결정은 즉시 inline 반영.** 답변 수령 후 문서 반영 → 재확인 → 다음 질문.
- **AskUserQuestion 강제.** 분기 결정·의도 확인은 **반드시** `AskUserQuestion` 툴로 수행한다 — 자유 텍스트 문의 금지. 옵션·근거·트레이드오프 동봉, 권장안은 (Recommended) 라벨 + 첫 옵션. `grill-me` 스킬 사용 중에도 동일.
- **다회차 grill 패턴.** `AskUserQuestion` 은 한 번에 4개 질문까지만 받는다(5개 이상 InputValidationError). 질문이 4개를 초과하면 **묶어서 한 번에 전달하지 말고 여러 차례에 나누어 연속 수행**한다. 각 차수 응답 후 결과를 문서·plan 에 inline 반영 → 다음 차수 진입. 차수 번호를 명시(예: "차수 1 Q1~Q4", "차수 2 Q5~Q8")하여 추적성을 확보한다. 단일 차수 안에서는 4-질문 제한 엄수, 차수 수에는 제한 없음.
- **/grill-me 적극 활용.** 비자명한 plan 진입 전 사용자가 `/grill-me` 로 깊은 검토를 요청할 수 있다. 1회 grill 마다 코드·문서를 직접 추적(line-level)하여 근거 기반으로 질문·권장안을 구성한다. 자유 추정 금지.
- **구현목적 정렬 의무.** 에이전트는 코드를 작성하기 전에 "무엇을 구현하는가" 뿐 아니라 "왜 그렇게 구현하는가" 를 사용자와 합의해야 한다. 구현목적은 사용자 요청사항과 일치해야 하며, 에이전트가 임의로 재정의하지 않는다. 목적이 불명확하거나 도메인 용어 해석이 갈릴 수 있으면 사용자와 확인·합의한 내용을 기준으로 구현한다.
- **구현품질 동시 충족.** 구현은 기능 달성만으로 충분하지 않다. 구현목적을 만족하면서도 기존 기능 회귀, 상태 생명주기 충돌, 책임 혼합, 세션 경계 누수 같은 구조적 결함을 함께 점검한다. "목적은 맞지만 품질이 낮은 임시 구현" 상태로 두지 말고, 남는 한계가 있으면 명시적으로 보고한다.
- **근거 우선 순위.** 추정보다 실제 현상 재현, 관련 문서, 현재 코드, 과거 diff, 사용자와 직접 논의하여 확정한 결정을 우선한다. 분석·수정·보고 시에는 "관찰된 현상", "코드에서 확인한 사실", "사용자와 합의한 규칙" 을 구분해서 다룬다.
- **도메인 지식 동기화.** 사용자 요청의 도메인 의미가 코드 용어와 다를 수 있으므로, 로드아웃/쿨다운/장착/정지/세션 같은 핵심 용어는 작업 초기에 코드 기준 의미와 사용자 의도를 맞춘다. 이 합의 없이 내부 구현 편의만으로 모델을 바꾸지 않는다.
- **수치 하드코딩 지양.** 결과값을 임의로 하드코딩하기보다 수식·파라미터 조정으로 자연 도달하도록 설계한다. 부득이한 hard-code 값은 후속 재캘리브 대상으로 명시한다.
- **GitHub issue 기반 작업 트래킹.** 다수 plan 의 진행 상태는 GitHub issue 로 등록·추적한다. 본문에는 목적·근거·수행 방법·관련 문서·우선순위·의존을 기재한다. **체크박스 작성 기준** — PR·이슈 body 의 체크박스는 작성 시점에 실제 수행·검증을 마친 항목만 `[x]`, 미수행 항목은 `[ ]` 로 둔다.
- **GitHub 조회·PR 작업은 `gh` 우선.** 이슈/PR 본문 확인, 코멘트 작성, PR 목록·상태 조회, PR 생성·수정은 가능한 경우 GitHub MCP/API 보다 `gh` 명령어를 먼저 사용한다. 샌드박스 권한 문제로 실패하면 승인 요청 후 재시도한다.
- **커밋·PR 문구는 한국어 우선.** 커밋 메시지 제목·본문, PR 제목·본문은 기본적으로 한국어로 작성한다. 명령어, 파일 경로, 로그 마커, 테스트 이름처럼 원문 보존이 필요한 기술 문자열은 그대로 둔다.
- **한글 GitHub 본문은 UTF-8 파일 경유.** Windows PowerShell 에서 한글 본문을 `stdin` 파이프(`... | gh issue comment --body-file -`)로 넘기면 인코딩이 깨질 수 있다. 한글이 포함된 이슈/PR 본문·코멘트는 반드시 **UTF-8 파일로 저장한 뒤** `gh ... --body-file <path>` 또는 동등한 파일 기반 입력으로 업로드한다. 업로드 전 파일 내용을 검토하고, `stdin` 직접 파이프는 사용하지 않는다.

## 변경 작업 시 권장 절차

1. 관련 `P##` 플랜(`docs/plan/` 우선, 없으면 `docs/plan/done/`)을 검색·선독.
2. 수정 대상 함수/모델의 호출부·의존성을 grep 으로 확인.
3. 테스트가 있으면 변경 전 기준점을 확보 → 수정 후 재실행.
4. 비자명한 변경이면 착수 전 영향도(코드·데이터·테스트·문서)를 분석·보고한다.

PR 생성·머지는 skill `pr-workflow` 사용 (`git add .` 차단, base 자동 감지).

## Session handoff memory

세션 간 연속성을 위한 핸드오프 메모는 리포 루트 기준 `.Codex/handoffs/handoff_*.md` 경로에 두며, skill `session-handoff` 로 생성·조회한다. 핸드오프 메모는 **세션 연속성용 참고자료**이며 현재 리포 운영 방침에 따라 버전관리 추적 대상이 될 수 있다. AGENTS.md와 충돌 시 **AGENTS.md 우선**.

## Skills 맵

| 상황 | 리소스 |
|-|-|
| plan 문서(P##/S##/CPR) 신설·패치·suffix 이관 | skill `plan-doc` |
| PR 생성·머지 (base 자동 감지, `git add .` 차단) | skill `pr-workflow` |
| 세션 종료 시 핸드오프 메모 | skill `session-handoff` |
| PR/Issue/plan 체크박스 구현 검증·갱신 | skill `check-and-verify` |
| Unity Editor 상태 확인·UI 생성·검증·테스트 절차 | skill `unity-workflow` |
| plan·design 깊은 검토 (인터뷰식 grilling) | skill `grill-me` / `grill-with-docs` |
| plan/PRD 를 이슈로 분해 | skill `to-issues` |
| 대화 맥락을 PRD 로 발행 | skill `to-prd` |
| 이슈 트리아지 (상태 머신) | skill `triage` |
| 이슈 트래커·트리아지 라벨·도메인 문서 초기 설정 | skill `setup-matt-pocock-skills` |
| 코드 영역의 상위 맥락 파악 | skill `zoom-out` |
