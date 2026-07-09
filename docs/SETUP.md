# 개발 환경 구축 가이드

## 1. 엔진 선택

이 프로젝트는 Unity 기준으로 진행하는 것을 추천한다.

이유:

- 2D 쿼터뷰 게임 구현에 충분히 적합함
- C# 기반이라 컴공 팀원이 역할을 나누기 좋음
- 충돌, 물리, UI, 애니메이션, 타일맵, 씬 관리가 무난함
- 졸업프로젝트 포트폴리오로 설명하기 좋음
- GitHub 협업 자료와 예제가 많음

Godot도 가능하지만, 팀원 모집과 협업 안정성을 생각하면 Unity가 더 무난하다.

## 2. 설치할 프로그램

필수:

- Git
- Git LFS
- VSCode
- Unity Hub
- Unity Editor LTS

추천 VSCode 확장:

- C# Dev Kit
- C#
- Unity
- GitLens

## 3. Unity 설치 방식

1. Unity Hub 설치
2. Unity Hub 로그인
3. `Installs`에서 Unity Editor LTS 버전 설치
4. 설치 모듈은 일단 아래만 선택
   - Microsoft Visual Studio Community는 선택하지 않아도 됨
   - Windows Build Support는 선택 권장
   - Documentation은 선택 사항

팀원 전원이 같은 Unity Editor 버전을 맞춰야 한다.

## 4. Unity 프로젝트 생성 설정

Unity Hub에서 새 프로젝트 생성:

- Template: `2D` 또는 `Universal 2D`
- Project name: 팀에서 정한 이름
- Location: 이 저장소 폴더 안

현재 프로젝트는 아래 설정으로 생성했다.

- Template: `Universal 2D`
- Project name: `SubwayCarry`
- Location: `graduation-game-project`
- Actual project path: `graduation-game-project/SubwayCarry`

프로젝트 생성 후 Unity에서 아래 설정 권장:

- `Edit > Project Settings > Editor`
  - Version Control Mode: `Visible Meta Files`
  - Asset Serialization Mode: `Force Text`

이 설정은 Git 충돌을 줄이는 데 중요하다.

## 5. GitHub 협업 규칙

브랜치 예시:

- `main`: 안정 버전
- `develop`: 통합 개발 버전
- `feature/player-movement`: 기능 개발
- `feature/npc-ai`: NPC AI 개발
- `feature/ui`: UI 개발

작업 방식:

1. `develop`에서 feature 브랜치 생성
2. 기능 구현
3. 커밋
4. GitHub에 push
5. Pull Request 생성
6. 확인 후 `develop`에 병합

커밋 메시지 예시:

- `feat: add player movement`
- `feat: implement object damage state`
- `fix: prevent npc overlap`
- `docs: update setup guide`

## 6. Git LFS 사용

이미지, 사운드, 영상, 원본 아트 파일은 용량이 커질 수 있으므로 Git LFS로 관리한다.

처음 한 번 실행:

```powershell
git lfs install
```

이 저장소에는 `.gitattributes`가 포함되어 있어서 주요 이미지/사운드 파일은 Git LFS 대상으로 잡혀 있다.

## 7. 공개 저장소 주의사항

기획 핵심 아이디어는 공개 저장소에 자세히 적지 않는다.

공개해도 되는 것:

- 개발 환경
- 코드
- 공개 가능한 작업 규칙
- 구현 이슈
- 추상적인 시스템 이름

공개하지 않는 것이 좋은 것:

- 게임 핵심 콘셉트 전체
- 상세 스테이지 구조
- 차별화 포인트를 그대로 드러내는 문서
- 비공개 기획서 원문

비공개 기획은 별도 문서나 private 저장소, Notion, Google Drive 등으로 관리한다.
