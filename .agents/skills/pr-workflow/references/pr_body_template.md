# PR Body 표준 템플릿

## 기본 (코드 변경)

```markdown
## Summary

- 핵심 변경 1
- 핵심 변경 2
- 기술적 맥락 (선택)

## Test plan

- [ ] 관련 단위 테스트 통과
- [ ] 빌드 / 린트 통과
- [ ] 수동 검증 항목 (선택)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

## 문서 전용 (docs/plan)

```markdown
## Summary

- 문서 N건 신설·패치
- 주요 변경 요약

## Test plan

- [ ] Doc-only change, no code touched
- [ ] 영구 제외 경로(`.claude/settings.local.json` 등)가 포함되지 않음
- [ ] 내부 링크 렌더링 확인

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

## 체크리스트 일반

PR body 작성 시 모든 템플릿 공통:
- [ ] Summary 는 bullet 3개 이내가 이상적
- [ ] Test plan 은 실제로 수행·검증한 것만 `[x]`, 미수행은 `[ ]`
- [ ] base branch 가 올바른지 확인 (`detect_base.sh` 결과 또는 명시 override)
- [ ] "🤖 Generated with..." 라인 포함

## 프로젝트별 필수 섹션

프로젝트가 정본 문서 동기화(Doc Impact), CI 게이트 같은 추가 필수 섹션을 두면 본 파일에 템플릿 변형을 등록한다. 예시:

```markdown
## Doc Impact

- [ ] <정본 문서 A> — 영향 없음 / 갱신: §X
- [ ] <정본 문서 B> — 영향 없음 / 갱신: §Y
```

## 커밋 메시지와의 관계

- **Commit subject**: 한 줄 요약 (70자 이내). PR title로도 사용 가능.
- **PR title**: Commit subject와 같거나, 여러 커밋일 때는 상위 개념 요약.
- **PR body**: Commit 본문의 확장판. Test plan 추가 필수.
