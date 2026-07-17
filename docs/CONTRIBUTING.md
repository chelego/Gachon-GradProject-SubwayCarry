# 협업 규칙

## 브랜치 작업 순서

1. 작업 전 `develop`을 최신 상태로 갱신한다.
2. `feature/<short-name>` 또는 `fix/<short-name>` 브랜치를 만든다.
3. 한 브랜치에는 하나의 기능이나 오류 수정만 포함한다.
4. 작업이 끝나면 `develop`을 대상으로 Pull Request를 만든다.
5. 기능 작업을 `main`에 직접 Push하지 않는다.

## 커밋 전 확인

- Unity `6000.3.7f1`로 프로젝트를 연다.
- Asset Import와 Compile이 끝날 때까지 기다린다.
- Unity Console에 Error가 없는지 확인한다.
- 변경한 Scene 또는 Test를 실행한다.
- `git status`에서 생성 파일이나 관계없는 파일이 포함되지 않았는지 확인한다.
- Asset을 추가, 이동, 삭제했다면 대응하는 `.meta` 파일도 함께 반영한다.

## 커밋 메시지

```text
feat: add player movement
fix: prevent duplicate passenger spawn
docs: update game design
chore: reorganize project assets
test: add package damage tests
```

커밋 유형인 `feat`, `fix`, `docs`, `chore`, `test`와 코드 식별자는 영어로 작성한다.

## Unity Asset 규칙

- 프로젝트 전용 Asset은 `SubwayCarry/Assets/_Project`에 저장한다.
- 공용 Scene이나 Prefab을 크게 수정하기 전에 팀에 알린다.
- 하나의 거대한 Scene보다 Prefab과 데이터 정의를 우선한다.
- `Library`, `Temp`, `Logs`, `UserSettings`, `Build`, `Builds`는 수정하거나 커밋하지 않는다.
- `.meta` 파일을 임의로 삭제하거나 다시 만들지 않는다.

## Pull Request 작성

Pull Request에는 다음 내용을 적는다.

- 변경한 기능
- 테스트 방법과 결과
- 변경된 Scene, Prefab, 데이터와 Project Settings
- 현재 제한 사항과 후속 작업
- 다른 팀원이 동시에 수정하면 충돌할 수 있는 파일
