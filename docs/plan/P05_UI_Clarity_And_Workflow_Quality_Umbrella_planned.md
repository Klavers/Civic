# P05 UI 가독성·상호작용 안정화 (총괄)

## §0 Context

P04에서 모듈형 문명 시스템과 플레이어용 HUD를 [PR #37](https://github.com/Klavers/Civic/pull/37)로 병합했다. 기능 범위가 넓어지면서 공용 Tooltip 수명주기, 내부 효과 ID 노출, 반복 건설 입력, 이벤트 modal과 좌측 패널 상태 충돌, `MainMenu`의 카메라 부재 안내가 후속 문제로 확인됐다.

P05는 다음 이슈를 단일 umbrella에서 관리한다.

- [#38 Tooltip 표시 안정성과 내용 기반 자동 크기 조절](https://github.com/Klavers/Civic/issues/38)
- [#39 UI 효과 문자열 로컬라이제이션과 게임 용어 매핑 시스템](https://github.com/Klavers/Civic/issues/39)
- [#40 건설 수량 토글과 일괄 건설 기능](https://github.com/Klavers/Civic/issues/40)
- [#41 이벤트 팝업 최상위 레이어와 좌측 패널 상태 보존](https://github.com/Klavers/Civic/issues/41)
- [#42 MainMenu 씬에 월드 카메라 배치](https://github.com/Klavers/Civic/issues/42)
- [#44 이벤트 선택지 인라인 효과 요약](https://github.com/Klavers/Civic/issues/44)

비차단 후속 이슈는 §8에서 관리한다.

## §1 목표 (Goals)

1. 효과 표시를 strict `.yaml` 로컬라이제이션과 FORMAT 규칙으로 통일해 내부 ID를 게임 용어로 변환한다.
2. Tooltip 본문만 TMP로 전환하고 안정적인 hover 수명주기, 자동 크기, 화면 경계 보정과 최대 12단계 nested 설명을 제공한다.
3. 이벤트 popup을 독립 modal layer로 분리해 배경 입력을 차단하면서 좌측 패널과 선택 탭 상태를 보존한다.
4. 건설 패널에 `1/5/10/25/Max` 단일 선택 모드를 제공하고 preview와 실행을 같은 원자적 batch 판정으로 처리한다.
5. `MainMenu`에 활성 MainCamera와 단일 AudioListener를 생성·검증한다.
6. 모든 UI 구조 변경을 Base Prefab 생성기·Validator·EditMode·PlayMode 검증에 포함한다.

## §2 비목표 (Non-goals)

- P05에서 기존 HUD 전체를 TMP로 전환하지 않는다.
- Tooltip concept 순환 참조 검출은 구현하지 않는다. 깊이 12 제한만 적용한다.
- TMP를 사용하지 않는 대체 Tooltip backend를 구현하지 않는다.
- 인구생산 건물의 보유 수 비례 건설비 공식은 도입하지 않는다.
- 건설 대기열·시간 기반 건설·사용자 지정 수량 입력을 추가하지 않는다.
- localization은 우선 한국어 catalog만 제공한다.
- 이벤트 콘텐츠·확률·보상 및 P04 모듈 밸런스를 재설계하지 않는다.
- 월드 배경·카메라 이동·줌·후처리를 구현하지 않는다.

## §3 적용 이슈와 의존 관계

| 이슈 | 분류 | 완료 결과 | 의존 관계 |
|-|-|-|-|
| #39 | 공통 presentation 기반 | strict YAML parser, key resolver, FORMAT, effect 용어 매핑, 누락 key 검증 | 최우선 구현. #38·#40·#44가 formatter 사용 |
| #38 | 공통 UI 기반 | TMP Tooltip, 지연·grace·중첩·자동 크기·경계 보정 | #39 결과로 실제 장문·concept 링크 검증 |
| #41 | modal/UI state | 전용 modal layer, 배경 입력 차단, 좌측 패널 상태 보존 | #38 Tooltip sorting과 함께 검증 |
| #44 | 이벤트 presentation | 선택지 localized 효과 요약 한 줄, 상세 Tooltip | #39·#38·#41과 함께 완료 |
| #40 | gameplay action/UI | 수량 토글, batch quote/execute, 최소 건설비 1 | #39·#38을 비용·불가 사유 표시에 사용 |
| #42 | scene quality | `MainMenu` MainCamera/AudioListener 계약 | 독립 수행 가능 |

## §4 확정 설계

### 4.1 Localization strict subset

- Unity `TextAsset`으로 읽을 수 있는 `.yaml`을 사용한다. BOM 유무를 모두 허용한다.
- 언어 header, ASCII key, 선택적 숫자 version, 따옴표 한 줄 값, `\n`, `$NAME|FORMAT$`, `[concept_key]`, 기본 색상 tag만 지원한다.
- Jomini 함수, reflection, 동적 expression은 지원하지 않는다.
- 별도 effect presentation CSV는 만들지 않고 `effect.<effectType>` key를 단일 규칙으로 사용한다.
- 대표 문법은 `$VALUE|percent;sign=always;good=up$`이다.
- FORMAT은 `number`, `integer`, `percent`, `multiplier`, `duration`과 `sign`, `decimals`, `good` 옵션을 지원한다.
- `[concept_key]`는 TMP link로 변환하고 연결 설명은 localization key에서 해석한다.
- 누락 key는 런타임에서 raw ID를 표시하고 `ValidateData`는 실패시킨다.
- 문법·FORMAT·effect 작성법은 `docs/localization/LOCALIZATION_FORMAT.md`를 정본으로 둔다.

### 4.2 Tooltip 수명주기와 중첩

- Tooltip TMP 기본 글꼴은 `NanumGothic.ttf` 기반 Dynamic·Multi Atlas `NanumGothic SDF`로 고정한다. 원본 OFL 1.1 전문은 `StreamingAssets/ThirdPartyNotices`에 포함하여 빌드 배포본에도 동봉한다.
- 기존 HUD Text는 유지하고 Tooltip 본문만 `TextMeshProUGUI`로 전환한다.
- hover 대상에서 포인터가 0.25초간 거의 정지하면 표시하고, 대상·부모·자식 사이 이동에는 0.25초 grace를 둔다.
- 부모 link에서 자식 Tooltip으로 이동할 때 즉시 닫지 않고 grace 동안 보존한다. 같은 chain을 1.5초 hover하거나 가운데 버튼을 누르면 고정하며, 한 번에 하나의 고정 chain만 유지한다.
- 고정 준비 중에는 외곽선이 회색에서 금색으로 변하고, 고정 완료 후에는 청색 외곽선을 유지한다. 가운데 버튼 재클릭, Tooltip 우선 ESC, 외부 클릭, 소유 패널 종료, 씬 전환으로 해제한다.
- 런타임 계층 생성 없이 생성기가 최대 12개의 card pool을 Base Prefab에 만든다.
- 13단계 진입은 차단하고 현재 Tooltip footer에 `최대 12단계까지 열 수 있습니다`를 표시한다.
- 장문은 스크롤하지 않고 의미 단위로 나눈 뒤 `설명 계속 보기` hover 링크로 자식 Tooltip을 연다. 반복 목록은 페이지당 최대 10개다.
- 최신 부모·자식 두 열을 우선 표시한다. 공간이 부족하면 최신 Tooltip을 오래된 조상 위에 겹칠 수 있지만 직접 부모와 활성 link는 가리지 않는다.
- Tooltip은 cursor 추적이 아니라 source anchor 기준으로 배치하며 safe area 안으로 제한한다.
- 기준 해상도 1920×1080에서 일반 폭 360~560, 한 줄 축약 임계 `min(480, safe width 42%)`, 확장 설명 최대 `min(760, safe width 70%)`, 최대 높이 safe area 82%를 사용한다.
- 한 줄 항목이 임계 폭을 넘으면 ellipsis 링크로 표시하고 hover 시 전체 내용을 자식 Tooltip에 표시한다.

### 4.3 이벤트 modal과 표시 순서

- Canvas sorting order는 `Tooltip 200 > 이벤트 modal 100 > 기타 overlay 80 > HUD 0`으로 고정한다.
- 이벤트 modal은 full-screen blocker로 배경 pointer 입력을 차단한다.
- popup 표시·닫기·선택·알림을 통한 재열기는 좌측 패널과 선택 탭을 변경하지 않는다.
- 선택지에는 localized 효과 요약 한 줄을 표시한다. 조건·기간·출처는 Tooltip에 둔다.

### 4.4 일괄 건설

- 패널 공용 단일 선택 토글 `1/5/10/25/Max`를 둔다. 새 런 시작 시 `1`로 초기화하고 런 중 패널 전환에는 유지한다.
- 고정 수량은 전체 수량을 건설할 수 있을 때만 활성화하며 부분 건설하지 않는다.
- `Max`는 현재 상태에서 가능한 최대 정수 수량을 계산하고 0이면 비활성화한다.
- preview와 실행은 같은 batch evaluator를 사용한다.
- 비용·건물 수·인구·module notification은 한 번에 원자적으로 반영하고 snapshot은 최종 상태에서 한 번 갱신한다.
- modifier 적용 후 모든 buildable 건물의 개당 최소 건설비는 1이다.
- 인구생산 건물의 기존 인구 제한 예외와 기술 해금 조건을 유지한다.

### 4.5 MainMenu 카메라

- #42의 기존 `SampleScene` 대상은 잘못된 진단이다. `SampleScene`에는 이미 MainCamera가 있다.
- 대상은 `MainMenu`이며 활성 `MainCamera` tag 카메라와 활성 AudioListener를 정확히 1개 둔다.
- generator는 재실행 시 중복 생성하지 않고 validator는 존재·활성·tag·중복을 검사한다.
- Screen Space Overlay 메뉴 UI 동작은 변경하지 않는다.

## §5 구현 단계 (Steps)

1. P05 결정·이슈 번호 동기화와 계획 확정 커밋
2. Localization parser·catalog·FORMAT·effect presentation·Data Validator·작성 문서
3. TMP Tooltip card pool·trigger lifecycle·nested link·생성기·Validator
4. 이벤트 modal Canvas·blocker·패널 상태 보존·선택지 inline summary
5. batch build quote/execute·수량 toggle·최소 비용·module notification
6. MainMenu camera 생성·검증
7. 단위·EditMode·PlayMode·Feature Matrix·Unity 통합 검증

## §6 검증 (Verification)

- [ ] localization parser, FORMAT, concept link, BOM 유무, 누락 key raw fallback을 검증한다.
- [ ] 기술·모듈·국가 modifier·이벤트 선택지에서 내부 effect ID가 노출되지 않는다.
- [ ] Tooltip 정지 지연, 이동 grace, owner 비활성화, 12단계 제한, 계속 보기, ellipsis link와 화면 경계를 검증한다.
- [ ] 부모·자식 이동 grace, 1.5초 자동 고정, 가운데 버튼 고정·해제, 외부 클릭·ESC·패널 종료 해제와 고정 외곽선을 검증한다.
- [ ] Tooltip 카드와 TMP 기본 설정이 `NanumGothic SDF`를 사용하고 `U+CD9C` 한글 글리프와 OFL 라이선스 동봉을 검증한다.
- [ ] 이벤트 표시·닫기·선택·재열기에서 좌측 패널·탭 상태가 유지되고 배경 입력이 차단된다.
- [ ] `1/5/10/25/Max`, 최소 비용 1, 인구 제한, 인구생산 예외, 원자적 batch를 검증한다.
- [ ] `MainMenu`에 활성 MainCamera와 단일 AudioListener가 존재하고 재생성 시 중복되지 않는다.
- [ ] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action Compile`
- [ ] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action GenerateUI`
- [ ] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action GenerateMainMenu`
- [ ] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action ValidateData`
- [ ] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action ValidateUI`
- [ ] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action ValidateMainMenu`
- [ ] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action FeatureMatrix`
- [ ] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action TestEditMode`
- [ ] `powershell -ExecutionPolicy Bypass -File scripts/Invoke-Unity.ps1 -Action TestPlayMode`
- [ ] `dotnet msbuild Civic/Civic.sln /p:RestorePackages=false /p:RestoreIgnoreFailedSources=true`
- [ ] 사용자 Editor에서 장문 nested Tooltip, 이벤트 popup, 건설 수량, MainMenu 카메라 안내 제거를 확인한다.

## §7 리스크와 완화

| 리스크 | 영향 | 완화 |
|-|-|-|
| localization key와 effect ID 결합 | 데이터 ID 변경 시 누락 가능 | raw fallback과 Validator 실패를 함께 제공 |
| TMP layout 반복 계산 | 프레임 저하 가능 | 내용·card 변경 시에만 preferred size 계산, pool 재사용 |
| 12단계 Tooltip 입력 경로 복잡화 | hover flicker·잔존 가능 | 단일 session owner, delay/grace 상태 머신, pool 기반 테스트 |
| Max preview와 commit 조건 불일치 | 일부 차감·잘못된 수량 | 동일 evaluator와 원자적 result 적용 |
| modal sorting 변경 | ESC·옵션 overlay 충돌 | sorting order 상수화, 배경 blocker와 상태 보존 PlayMode 테스트 |
| 카메라·AudioListener 중복 | Unity 경고·오디오 중복 | generator idempotency와 정확히 1개 Validator |

## §8 비차단 Follow-up

- [#45 인구생산 건물 보유 수 비례 건설비 밸런스](https://github.com/Klavers/Civic/issues/45)
- [#46 Tooltip concept 순환 참조 검출](https://github.com/Klavers/Civic/issues/46)
- [#47 Tooltip TMP 미사용 backend 검토](https://github.com/Klavers/Civic/issues/47)
- [#48 전체 UI TextMeshPro 전환 검토](https://github.com/Klavers/Civic/issues/48)

이 네 항목은 P05 완료를 차단하지 않는다. #44만 P05 필수 하위 이슈로 구현·검증한다.

## §9 P06 후속 계획 연결

- 개발자·AI용 용어사전은 runtime strict YAML을 대체하지 않으며 [P06a](./P06a_Localization_Glossary_planned.md)에서 관리한다.
- 전체 GUI 배율은 Legacy `Text`와 TMP 혼재 상태를 지원하며 #48의 전체 TMP 전환을 선행조건으로 삼지 않는다. 상세 계약은 [P06b](./P06b_UI_Scale_Accessibility_planned.md)를 따른다.
- 모듈 도메인 허브 이관은 P05의 Tooltip·modal sorting·ESC 수명주기를 보존해야 한다.
