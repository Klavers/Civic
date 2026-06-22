# 영구 제외 경로 (PR scope 밖)

이 경로들은 **PR에 포함되지 않는다**. staging 단계에서 필터링 필수.

## 기본 목록

| 경로 | 사유 | .gitignore? |
|-|-|-|
| `.claude/settings.local.json` | Claude Code 개인 로컬 설정 (모델·테마·권한 등). 환경별 다름 | 미기재 (untracked 유지) |
| `.env`, `.env.*` | 비밀키·토큰 | 명시적, 절대 금지 |

> `.agents/skills/` 는 제외 대상 아님 — 이 리포의 워크플로우 스킬이므로 정상 tracked. `.Codex/settings.local.json` 만 개인 환경 설정으로 제외.

## prefix 매칭 규칙

위 경로는 **prefix 매칭**으로 필터링:
- `.claude/settings.local.json` → 제외 (정확 경로)
- `.env`, `.env.local` → 제외
- `.agents/skills/foo.md` → **포함** (prefix 다름)

## 프로젝트별 확장

프로젝트가 추적하지 않을 경로(대용량 데이터 디렉토리, 빌드 산출물, 로컬 DB 등)가 생기면 본 목록에 추가:
1. 이 파일의 표에 항목 추가
2. `scripts/safe_stage.sh` 의 `EXCLUDE_PATTERNS` 배열 업데이트
3. 가능하면 `.gitignore` 에도 반영 (add 자체가 실패하도록)

## 특수 케이스

### 영구 제외 경로에 의도적 추가 발생 시
사용자가 명시적으로 제외 경로 내부 파일 추가를 요청하면 예외 허용 가능. 단:
1. 추가 사유를 사용자에게 재확인
2. `.gitignore` 수정 여부 판단
3. PR body에 예외 사유 명기

## 검증

staging 후 확인:
```bash
git diff --cached --name-only
```

위 목록의 경로가 하나라도 포함되면:
```bash
git reset HEAD <path>
```
