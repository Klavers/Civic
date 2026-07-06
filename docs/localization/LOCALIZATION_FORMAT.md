# Civic localization strict YAML·FORMAT 규약

## 1. 목적과 범위

게임 내부 `effectType`, resource/building ID와 플레이어 표시 문구를 분리한다. P05는 한국어 catalog만 제공하지만 parser와 resolver는 언어 catalog를 교체할 수 있는 구조다. 정본 경로는 `Civic/Assets/_Project/Resources/Localization/Korean/*.yaml`이다.

이 문법은 YAML 전체가 아니라 Paradox localization에서 영감을 받은 strict subset이다. Unity `TextAsset`으로 읽으며 UTF-8 BOM 유무를 모두 허용한다.

## 2. 지원 문법

```yaml
l_korean:
 concept_tax_rate:0 "세율"
 concept_tax_rate.desc:0 "GDP에서 국고 수입으로 전환되는 비율입니다."
 effect.taxRateAdd:0 "[concept_tax_rate] $VALUE|percent;sign=always;good=up$"
```

- 첫 유효 줄은 ASCII 언어 header와 `:`다.
- entry는 한 칸 이상 들여쓴 ASCII key, 선택적 `:version`, 따옴표 한 줄 값으로 구성한다.
- 값 안에서는 `\n`, `\\`, `\"`, `\[`, `\]` escape만 허용한다. `\[`와 `\]`는 concept link가 아닌 리터럴 대괄호 표기에 사용한다.
- 빈 줄과 줄 시작 `#` comment를 허용한다.
- 같은 key 중복, 서로 다른 언어 header 혼합, 지원하지 않는 escape·문법은 validation 실패다.
- Jomini 함수, expression, reflection 기반 호출, 복수 줄 따옴표 값은 지원하지 않는다.

## 3. 치환 token과 FORMAT

token 문법은 `$NAME|FORMAT;option=value$`다. `NAME`은 대문자 ASCII이며 호출자가 제공한다.

| FORMAT | 입력 의미 | 출력 예 |
|-|-|-|
| `text` | 문자열 | `밀농장` |
| `number` | 일반 수치 | `+1.25` |
| `integer` | 정수 | `+3` |
| `percent` | 비율 0.3 → 30 | `+30%` |
| `multiplier` | 가산 비율 0.3 → 배수 1.3 | `×1.3` |
| `duration` | 초 | `12.5초` |

지원 option:

- `sign=auto|always|never`: 양수 부호 표시 정책
- `decimals=0..4`: 최대 소수 자릿수
- `good=up|down|neutral`: 양수 또는 음수가 유리한지를 지정한다. rich text에서는 유리한 값은 녹색, 불리한 값은 붉은색으로 표시한다.

예시:

```yaml
 effect.taxRateAdd:0 "[concept_tax_rate] $VALUE|percent;sign=always;good=up$"
 effect.constructionCostMultiplier:0 "$TARGET$ 건설 비용 $VALUE|percent;sign=always;good=down$"
 condition.status_met:0 "<color=#62C982>\[O\]</color>"
```

## 4. concept link와 색상 tag

- `[concept_key]`는 표시 문구 `concept_key`와 설명 `concept_key.desc`를 사용한다.
- TMP Tooltip에서는 `<link>`로 변환되어 hover 시 자식 Tooltip을 연다.
- 일반 Text에서는 link markup 없이 표시 문구만 반환한다.
- 기본 색상 tag는 `#positive ...#!`, `#negative ...#!`, `#emphasis ...#!`만 지원한다.

concept 참조 깊이는 Tooltip에서 최대 12단계다. 순환 참조 검출은 P05 범위가 아니며 #46에서 추적한다.

## 5. effect key 규칙

별도 effect presentation CSV를 만들지 않는다. 모든 runtime effect는 `effect.<effectType>` key를 사용한다.

공통 argument:

- `VALUE`: effect amount
- `TARGET`: resource/building/technology/era 표시명 또는 fallback ID
- `DURATION`: 초 단위 기간
- 기술 효과 추가 argument: `BUILDING`, `INPUT`, `OUTPUT`

누락 key는 런타임에서 raw `effectType`을 표시해 데이터를 숨기지 않는다. `Tools > Civic > Data > Validate`는 누락 key와 잘못된 FORMAT을 오류로 처리한다.

## 6. 작성·검증 절차

1. runtime에 새 effectType을 추가한다.
2. 한국어 catalog에 `effect.<effectType>` entry를 추가한다.
3. 적절한 FORMAT과 `good` 방향을 지정한다.
4. 필요한 concept 표시명과 `.desc`를 추가한다.
5. Editor가 열려 있으면 `Tools > Civic > Data > Validate`, 닫혀 있으면 `scripts/Invoke-Unity.ps1 -Action ValidateData`를 실행한다.
6. 대표 패널과 Tooltip에서 raw ID가 노출되지 않는지 확인한다.

## 7. 실패와 fallback

- parser 문법·FORMAT·필수 effect key 오류: Data Validator 실패
- runtime 누락 key: raw ID 표시
- 누락 concept 표시명·설명: concept key 자체 표시
- 누락 argument: 원문 token을 남겨 오류가 보이게 함
