# gh CLI 경로 해결

`gh` 가 PATH에 없을 수 있다 (특히 Windows + Git Bash).

## 경로 탐색 순서

1. `command -v gh` — PATH 등록돼 있으면 이 경로 사용
2. `/c/Program Files/GitHub CLI/gh.exe` — Windows 기본 설치 경로 (Git Bash)
3. `C:\Program Files\GitHub CLI\gh.exe` — CMD/PowerShell
4. `/usr/local/bin/gh` — macOS / Linux

## Git Bash (Windows)

```bash
GH="$(command -v gh || echo '/c/Program Files/GitHub CLI/gh.exe')"
"$GH" pr create --base main ...
```

## PowerShell

```powershell
gh pr create --base main ...
# 또는
& "C:\Program Files\GitHub CLI\gh.exe" pr create --base main ...
```

## 인증 확인

```bash
gh auth status
```

결과가 `not logged into any GitHub hosts` 이면 `gh auth login`.

**주의**: `gh auth login` 은 interactive prompt 발생. 세션이 non-interactive 환경이면 **사용자에게 위임**하고 "로그인 완료 후 알려달라"고 요청.

## 자주 쓰는 명령

| 작업 | 명령 |
|-|-|
| PR 목록 | `gh pr list --state all --limit 10` |
| PR 상세 | `gh pr view NN --json state,mergeable,mergeStateStatus` |
| PR 생성 | `gh pr create --base main --title ... --body ...` |
| PR base 수정 | `gh pr edit NN --base <branch>` |
| PR 머지 | `gh pr merge NN --merge` (브랜치 보존 — `--delete-branch` 미사용) |
| 이슈 목록 | `gh issue list` |

## 세션 내 경로 캐싱

```bash
GH="$(command -v gh || echo '/c/Program Files/GitHub CLI/gh.exe')"
```

스크립트(`scripts/create_pr.sh`, `scripts/merge_and_sync.sh`)는 이 로직을 내장.
