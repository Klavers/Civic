---
name: pr-workflow
description: PR을 안전하게 생성·머지한다. "PR 생성", "merge 수행", "브랜치 만들고 푸시", "pull request" 같은 지시에 반드시 사용한다. Base branch는 기본 main 이며, worktree 작업 시 분기한 working branch 를 자동 감지(또는 사용자 확인)한다. Head는 현재 branch(HEAD)다. `git add .` / `-A` 를 차단하여 명시 경로 staging만 허용하고, `.Codex/settings.local.json`·`.env` 같은 영구 제외 경로를 자동 필터링한다. Commit 메시지 HEREDOC + Co-Authored-By 라인, PR body "## Summary / ## Test plan" 템플릿을 표준으로 적용한다. gh CLI 경로를 자동 해결한다. 사용자가 PR 관련 의도를 보이면(base 언급, 커밋 메시지 초안, push/merge 언급 등) 명시 지시가 없어도 이 스킬을 호출한다.
---

# pr-workflow

PR 생성·머지 표준을 강제한다. **scope 사고(`git add -A`로 제외 경로 포함)** 를 원천 차단한다.

## 왜 이 스킬이 필요한가

`.Codex/settings.local.json` / `.env` 등 일부 경로는 **추적 대상이 아니다**. 일반적 PR 관행대로 `git add .` 를 쓰면 이들이 우발적으로 포함된다. 이 스킬이 그 관행을 내재화한다.

## 언제 이 스킬을 사용하나

### 반드시 사용
- "PR 만들어" / "pull request 생성" / "PR 올려"
- "merge 수행" / "PR 머지해"
- "브랜치 만들고 푸시"
- "현재 작업을 PR로" / "세션 변경사항 PR화"
- 사용자가 `gh pr create` 커맨드를 초안으로 작성한 경우

### 사용하지 않음
- 로컬 커밋만 원할 때 (push 없이) — 일반 git 커밋 절차로 진행, 단 scope 룰은 동일 적용
- PR 조회만 하는 경우 (`gh pr list`, `gh pr view`)

## 핵심 규약 (비협상)

### 1. Base = main (기본), worktree 작업 시 분기 branch 자동 감지

기본 base 는 `main`. **worktree 에서 작업 중이면** 그 worktree 가 분기한 working branch 로 머지하는 것이 맞을 수 있으므로 `scripts/detect_base.sh` 로 자동 감지한다.

```bash
# 자동 감지 (메타 파일 → env → 휴리스틱 → main fallback)
BASE=$(bash .agents/skills/pr-workflow/scripts/detect_base.sh 2>/tmp/db.err)
EC=$?

if [ $EC -eq 2 ] || [ "$BASE" = "AMBIGUOUS" ]; then
    # confidence LOW/NONE — 사용자에게 AskUserQuestion 으로 문의
    # 후보 목록은 /tmp/db.err 의 candidates= 라인 참조
    # 결정 후: bash detect_base.sh --write <chosen>
    ...
fi

HEAD=$(git branch --show-current)
gh pr create --base "$BASE" --head "$HEAD" ...
```

**사용자 문의 절차** (detect_base 가 LOW/NONE 반환 시 필수):

1. Codex 가 `AskUserQuestion` 으로 후보 목록 제시 (옵션 라벨 = branch 이름)
2. 각 옵션 description 에 `git log -1 --oneline <branch>` 결과 포함
3. 사용자 선택 후 `bash detect_base.sh --write <chosen>` 으로 메타 파일 영구 기록
4. 이후 본 worktree 의 모든 PR 은 그 base 사용 (HIGH confidence)

자세한 가이드: [`references/base_branch_detection.md`](references/base_branch_detection.md)

### 2. `git add .` / `-A` 금지

스테이징은 **반드시 명시 경로**로:

```bash
git add path/to/file1 path/to/file2
```

이유: `.Codex/settings.local.json`, `.env` 같은 경로가 추적되지 않은 채 유지되며, 전체 staging은 이들을 우발적으로 포함시킨다.

사용자가 "전체 추가" 요청 시 **거부하고 대안 제시**:
1. `git status --porcelain` 으로 변경 목록 확인
2. 영구 제외 경로 필터링 (`scripts/safe_stage.sh` 참조)
3. 필터링 후 남은 파일 목록을 사용자에게 제시 → 확인 후 `git add <paths>`

영구 제외 경로 (기본값 — 프로젝트별로 `references/excluded_paths.md` 와 `scripts/safe_stage.sh` 에서 확장):
- `.Codex/settings.local.json` (Codex 개인 로컬 설정)
- `.env`, `.env.*` (비밀키·토큰)

> `.agents/skills/` 는 이 리포 워크플로우 스킬이므로 정상 tracked. `.Codex/settings.local.json` 만 개인 환경 설정으로 제외.

### 3. Commit 메시지 포맷

- 제목: 70자 이하, 대문자 시작, 명령형
- 본문: 1줄 공백 후 변경 요약 (bullet)
- 마지막 줄: `Co-Authored-By: Codex <noreply@anthropic.com>`
- HEREDOC 사용:

```bash
git commit -m "$(cat <<'EOF'
제목 한 줄

- 변경 1
- 변경 2

Co-Authored-By: Codex <noreply@anthropic.com>
EOF
)"
```

### 4. PR body 포맷

```markdown
## Summary

- 핵심 변경 1~3개 (bullet)
- 기술적 맥락·근거 필요 시 한 줄 추가

## Test plan

- [ ] 테스트 항목 1
- [ ] 테스트 항목 2

🤖 Generated with [Codex](https://Codex.com/Codex)
```

**체크박스 표기 기준** — Test plan 의 체크박스는 작성 시점에 실제 수행·검증을 마친 항목만 `[x]`, 미수행은 `[ ]` 로 둔다.

문서 PR인 경우 "Test plan"은 doc 검증 체크리스트로 대체 가능. 템플릿 변형은 [`references/pr_body_template.md`](references/pr_body_template.md) 참조.

> 프로젝트가 정본 문서 동기화(Doc Impact) 같은 추가 필수 섹션을 두면, `references/pr_body_template.md` 에 등록하고 본 포맷을 확장한다.

### 5. gh CLI 경로

Windows Git Bash 환경에서 `gh` 가 PATH에 없을 수 있음. 전체 경로 사용:

```bash
"/c/Program Files/GitHub CLI/gh.exe" pr create --base "$BASE" --head <branch> ...
```

PowerShell 환경이면 `gh` 로 단축 가능. 스크립트는 두 환경 모두 지원. 상세: [`references/gh_cli_paths.md`](references/gh_cli_paths.md)

### 5-1. 한글 본문 업로드 인코딩 규칙

Windows PowerShell 에서 한글 PR 본문·코멘트·설명문을 `stdin` 파이프로 `gh` 에 직접 넘기면 인코딩이 깨질 수 있다. 따라서 한글이 포함된 GitHub 텍스트는 반드시 UTF-8 파일을 만든 뒤 `--body-file <path>` 로 업로드한다.

금지:

```powershell
@'
한글 본문
'@ | gh pr create --body-file -
```

권장:

```powershell
$path = 'Z:\example-repo\.Codex\tmp\gh-body.md'
[System.IO.File]::WriteAllText($path, $body, [System.Text.UTF8Encoding]::new($false))
gh pr create --body-file $path
```

임시 파일을 쓴 경우 업로드 전 내용을 검토하고, 필요 시 같은 파일을 재사용해 PR 수정/코멘트 추가를 수행한다.

## 워크플로우 (전체 흐름)

> **토큰 효율 원칙**: 본 스킬의 git/gh 호출은 LLM 컨텍스트로 직접 들어간다. 인간 친화 정보(diff stat, fast-forward 메시지, LF→CRLF warning, "Already up to date") 는 토큰 손실 — `-q`/`--quiet` 옵션과 stderr 리다이렉션을 적극 활용한다.

### (1) 사전 확인
1. 현재 브랜치 확인: `git branch --show-current` (이것이 PR head 가 됨)
2. **base 결정**: `bash scripts/detect_base.sh 2>/tmp/db.err` — 자동 감지. exit code 2 (LOW/NONE) 시 `AskUserQuestion` 으로 사용자 문의 → 결정 값 `--write` 로 영구 기록.
3. base 가 최신인지: `git fetch -q origin "$BASE"`
4. `git status --short -uno` 로 변경 파악 — `-uno` 는 untracked 파일을 숨겨 노이즈 차단.
5. gh auth 상태: `gh auth status` (1회/세션)

### (2) 작업 branch 준비
적절한 feature branch 에 있지 않다면 생성:
```bash
git checkout -b <descriptive-branch-name>
```

### (3) 안전 staging (LF/CRLF warning 억제)
`scripts/safe_stage.sh` 실행 또는 수동:
1. `git status --porcelain` → 변경 파일 목록
2. 영구 제외 경로 필터링
3. 사용자에게 최종 목록 보여주고 확인
4. `git add <filtered paths> 2>/dev/null` — Windows `core.autocrlf=true` 환경의 LF→CRLF warning 폐기

**주의 — 금지 패턴**: `git add .` / `git add -A` — 영구 제외 경로가 우발적으로 staging 됨. 본 스킬 핵심 룰 §2 위반.

### (4) Commit
위 "Commit 메시지 포맷" 준수. HEREDOC 사용.

### (5) Push
```bash
git push -u --quiet origin <branch-name>
```

### (6) PR 생성
`scripts/create_pr.sh` (자동 base 감지) 또는 수동:
```bash
HEAD=$(git branch --show-current)
BASE=$(bash .agents/skills/pr-workflow/scripts/detect_base.sh 2>/tmp/db.err) \
  || { echo "base detection failed — see /tmp/db.err"; exit 1; }

gh pr create --base "$BASE" --head "$HEAD" --title "..." --body "$(cat <<'EOF'
## Summary
...
EOF
)"
```

### (7) Merge (사용자 요청 시) — 헬퍼 스크립트 권장
```bash
bash .agents/skills/pr-workflow/scripts/merge_and_sync.sh <NN>
# OK pr=<NN> merged synced base=<branch>
```

이 스크립트는 mergeStateStatus 사전 확인 → PR 의 baseRefName 조회 → 머지 → 로컬 base branch 동기화까지 1회 호출로 처리.

머지 전 확인:
- `mergeStateStatus == "CLEAN"`
- CI 체크가 있다면 통과

> **브랜치 보존 (비협상).** 머지 시 head branch 와 worktree 를 **자동 삭제하지 않는다**. `gh pr merge` 의 `--delete-branch` 금지, worktree 의 `git worktree remove` 금지. 헬퍼 스크립트도 `--delete-branch` 를 쓰지 않는다. 브랜치·worktree 정리는 사용자가 명시적으로 요청할 때만 수행한다.

### (8) 보고
사용자에게 PR URL 을 보고. 머지된 경우 `(merged)` 추가.

## 에러 복구

### ".gitignore 로 무시된 파일이 스테이징 됨"
→ 영구 제외 경로가 `-A` 로 끌려들어간 경우. 언스테이징: `git reset HEAD <path>`

### Pre-commit hook 실패
**amend 금지**. 문제 수정 후 **새 커밋** 생성.

### "gh: command not found"
→ 전체 경로 사용: `/c/Program Files/GitHub CLI/gh.exe` (Windows) 또는 PowerShell `gh`.

## 번들 리소스

- `scripts/safe_stage.sh` — 영구 제외 경로 필터링 staging 래퍼
- `scripts/detect_base.sh` — base branch 결정 (메타 → env → 휴리스틱 → main, confidence 라벨)
- `scripts/create_pr.sh` — PR 생성 헬퍼 (자동 base 감지 + gh 경로 + body 템플릿)
- `scripts/merge_and_sync.sh` — PR 머지 + base branch 로컬 동기화 1라인 헬퍼
- `references/pr_body_template.md` — PR body 표준 템플릿 모음
- `references/gh_cli_paths.md` — OS·환경별 gh 경로 해결
- `references/excluded_paths.md` — 영구 제외 경로 목록 + 사유
- `references/base_branch_detection.md` — base 자동 감지 정책·휴리스틱·사용자 문의 절차

## Do / Don't 요약

### DO
- Base: main 기본. worktree 작업 시 `detect_base.sh` 자동 감지. 모호하면 사용자 문의 + 메타 영구 기록.
- Head: 현재 branch (`git branch --show-current`)
- Stage: 명시 경로만
- Commit: HEREDOC + Co-Authored-By
- gh 경로: PATH 의 `gh` 또는 `/c/Program Files/GitHub CLI/gh.exe`

### DON'T
- `git add -A` / `git add .`
- `git commit --amend` (pre-commit hook 실패 후)
- `--no-verify` / `--no-gpg-sign` (사용자 명시 요청 없이)
- Force push to base branch
- `gh pr merge --delete-branch` / `git worktree remove` — 머지 후 브랜치·worktree 임의 삭제 금지 (사용자 명시 요청 시에만)
- detect_base 의 LOW/NONE 결과를 무시하고 임의 fallback — 반드시 사용자 문의
