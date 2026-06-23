# Base Branch 결정 — `detect_base.sh` 사용 가이드

## 정책

PR base 의 기본값은 `main`. 단 **worktree 에서 작업 중이면** 그 worktree 가 분기한 working branch 로 머지하는 것이 맞을 수 있다. `scripts/detect_base.sh` 가 이를 자동 판별한다.

- 메인 레포에서 작업: 보통 `main` 으로 머지
- worktree 에서 작업: worktree 가 분기한 branch 를 휴리스틱으로 감지
- 감지 모호 시: 사용자에게 문의 → 메타 파일에 영구 기록

## 우선순위 + 신뢰도

| 순위 | 출처 | confidence | 출력 |
|---|---|---|---|
| 1 | `--base <branch>` 명시 인자 | HIGH | branch 그대로 |
| 2 | 메타 파일 `$GIT_DIR/claude-pr-base` 첫 줄 | HIGH | branch 그대로 |
| 3 | 환경변수 `CLAUDE_PR_BASE_BRANCH` | HIGH | branch 그대로 |
| 4 | 휴리스틱 — worktree 신설 시 첫 commit 을 포함하는 local branch (단일 후보 또는 우선순위 매치) | MEDIUM | branch |
| 5 | 휴리스틱 다중 매치 (모호) | LOW | `AMBIGUOUS` + stderr 후보 |
| 6 | 감지 실패 (reflog 정보 없음 등) | NONE | `AMBIGUOUS` |

종료 코드:
- `0` — HIGH 또는 MEDIUM (그대로 사용 가능)
- `2` — LOW 또는 NONE (사용자 확인 필요)
- `1` — 일반 오류

## 사용 패턴

### Pattern A — 신규 worktree 진입 직후 (메타 1회 기록)

```bash
RAW=$(bash .agents/skills/pr-workflow/scripts/detect_base.sh --candidates 2>/tmp/db.err)
EC=$?
CONF=$(grep -oE 'confidence=[A-Z]+' /tmp/db.err | head -1 | cut -d= -f2)

if [ $EC -eq 0 ]; then
    BASE="$RAW"
fi

if [ $EC -eq 2 ]; then
    # claude: AskUserQuestion 호출 → 사용자 선택 결과 = $CHOSEN
    bash .agents/skills/pr-workflow/scripts/detect_base.sh --write "$CHOSEN"
fi
```

### Pattern B — 이후 호출 (메타 파일 자동 사용)

```bash
BASE=$(bash .agents/skills/pr-workflow/scripts/detect_base.sh)
# → 메타 파일 있으면 즉시 그것 반환 (HIGH)
```

### Pattern C — 명시 override

```bash
BASE=$(bash detect_base.sh --base main)
```

## 휴리스틱 알고리즘 (요약)

```
1. $GIT_DIR/logs/HEAD 의 첫 줄에서 첫 commit hash 추출 (= worktree 신설 시점 HEAD)
2. git branch --contains <hash> 로 그 commit 을 포함하는 모든 local branch 수집 (현재 branch 제외)
3. 후보 수에 따라:
   - 0개 → NONE (감지 실패)
   - 1개 → MEDIUM (단일 후보)
   - 2개 이상 → 우선순위 매치 (main) 시도
     - 매치 1개 → MEDIUM
     - 매치 0개 또는 2개 이상 → LOW (사용자 문의 필요)
```

우선순위 branch 목록은 `detect_base.sh` 의 `PRIORITY_BRANCHES` 에서 조정한다 (기본: `main`). 프로젝트가 `develop` 등 다른 통합 branch 를 쓰면 여기에 추가한다.

## 한계

- **시간 경과로 후보 증가**: worktree 가 오래되어 다른 branch 들이 같은 ancestor commit 을 포함하게 되면 후보가 늘어난다.
- **메타 파일 권장**: 1회 사용자 확인 후 `--write` 로 영구 기록하면 이후 자동.

## 사용자 문의 절차 (claude 책임)

`detect_base.sh` 가 LOW/NONE 반환 시 claude 는:

1. `AskUserQuestion` 으로 후보 목록 제시
2. 옵션 라벨: 후보 branch 이름 그대로
3. 옵션 description: 각 branch 의 최근 commit 1줄 요약 (`git log -1 --oneline <branch>`)
4. 사용자 선택 후 `bash detect_base.sh --write <chosen>` 으로 메타 파일 기록
5. 이후 PR 생성·머지에 그 base 사용

## 관련 파일

- `scripts/detect_base.sh` — 검출 헬퍼
- `scripts/create_pr.sh` — `--base` 미지정 시 detect_base 호출
- `scripts/merge_and_sync.sh` — 머지 후 sync 대상도 detect_base
- `SKILL.md` §"Base 결정" 섹션
