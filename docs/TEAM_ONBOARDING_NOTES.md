# 팀원 설명용 개발 환경 정리

## 현재 진행 상태

2026년도 2학기 졸업프로젝트 게임 개발을 Unity 기준으로 진행하기로 했다.

현재 확인된 상태:

- Unity Hub 설치됨
- Unity Editor 설치됨
- Unity 프로젝트 생성 완료
- 프로젝트 이름: `SubwayCarry`
- 프로젝트 템플릿: `Universal 2D`
- 프로젝트 위치: `graduation-game-project/SubwayCarry`
- VSCode 사용 예정
- GitHub에 코드를 올리며 작업 예정
- Git 설치 확인됨
- Git LFS 설치 및 초기 설정 완료
- Unity용 `.gitignore` 작성 완료
- Unity용 `.gitattributes` 작성 완료
- VSCode 추천 확장 설정 작성 완료
- 세부 기획은 공개 저장소에 올리지 않고 별도 비공개 문서로 관리 예정

## Unity를 선택한 이유

이 프로젝트는 2D 쿼터뷰 게임이며, 플레이어 이동, 충돌 처리, 오브젝트 파손, NPC AI, UI, 스테이지 진행 시스템이 중요하다.

Unity를 사용하는 이유:

- 2D 쿼터뷰 게임을 만들기 적합함
- C# 기반이라 컴공 팀원들이 코드를 나눠 작성하기 좋음
- 충돌, 물리, 애니메이션, UI, 씬 관리 기능이 잘 갖춰져 있음
- NPC AI, 상태 관리, 경로 이동 같은 시스템을 구현하기 좋음
- GitHub 협업 사례와 참고 자료가 많음
- 졸업프로젝트 결과물과 포트폴리오로 설명하기 좋음

## 개발 도구

팀 기준 사용 도구:

- 게임 엔진: Unity
- 코드 에디터: VSCode
- 언어: C#
- 버전 관리: Git
- 원격 저장소: GitHub
- 대용량 파일 관리: Git LFS

## 팀원들이 맞춰야 할 것

팀원들은 아래 환경을 맞추면 된다.

1. Unity Hub 설치
2. 팀에서 정한 동일한 Unity Editor 버전 설치
3. VSCode 설치
4. Git 설치
5. Git LFS 설치
6. GitHub 계정 준비
7. VSCode 추천 확장 설치

VSCode 추천 확장:

- C# Dev Kit
- C#
- Unity
- GitLens

## Unity 프로젝트 생성 후 해야 할 설정

Unity 프로젝트를 생성한 뒤 아래 설정을 맞춘다.

`Edit > Project Settings > Editor`

- Version Control Mode: `Visible Meta Files`
- Asset Serialization Mode: `Force Text`

이 설정은 Unity 파일을 Git으로 관리할 때 중요하다. `.meta` 파일이 보여야 에셋 연결이 깨지지 않고, Force Text를 켜야 씬과 프리팹 변경 내용을 Git에서 비교하기 쉽다.

## GitHub 협업 방식

기본 브랜치 구조는 아래처럼 가져간다.

- `main`: 발표 또는 제출 가능한 안정 버전
- `develop`: 기능을 합치는 개발 버전
- `feature/...`: 개인별 기능 작업 브랜치

예시:

- `feature/player-movement`
- `feature/object-damage`
- `feature/npc-ai`
- `feature/train-system`
- `feature/ui`

작업 순서:

1. `develop`에서 자기 기능 브랜치를 만든다.
2. 맡은 기능을 구현한다.
3. 커밋한다.
4. GitHub에 push한다.
5. Pull Request를 만든다.
6. 팀원이 확인한 뒤 `develop`에 합친다.

## 커밋 메시지 예시

- `feat: add player movement`
- `feat: implement object damage`
- `feat: add npc state machine`
- `fix: prevent player collision bug`
- `docs: update setup guide`

## 공개 저장소 주의사항

아이디어 보호를 위해 세부 기획은 공개 저장소에 자세히 적지 않는다.

공개 저장소에 올려도 되는 것:

- 코드
- 개발 환경 문서
- 협업 규칙
- 추상적인 시스템 이름
- 공개 가능한 이슈와 작업 내역

공개 저장소에 올리지 않는 것:

- 상세 기획서 원문
- 핵심 아이디어를 그대로 드러내는 설명
- 구체적인 스테이지 구성
- 차별화 포인트 전체
- 비공개 회의 내용

세부 기획은 팀 합류가 확정된 사람에게 별도 비공개 문서로 공유한다.

## 다음 진행 단계

현재 다음 단계는 Unity 프로젝트 기본 설정 확인과 GitHub 연결이다.

이후 진행할 작업:

1. Unity 프로젝트 생성 확인
2. Unity Editor 설정 확인
3. Git 저장소 구조 확인
4. 첫 커밋 생성
5. GitHub 원격 저장소 연결
6. `main`, `develop` 브랜치 구성
7. 팀원용 clone 및 실행 방법 정리
