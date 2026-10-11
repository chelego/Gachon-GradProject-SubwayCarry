# 가천대학교 졸업프로젝트 SubwayCarry

`SubwayCarry`는 2026학년도 2학기 가천대학교 졸업프로젝트로 개발하는 Unity 게임이다.

플레이어는 혼잡한 수도권 지하철에서 케이크를 목적지까지 운반한다. 배송마다 노선, 교통비, 물건 가치, 혼잡도와 손해 위험이 달라진다. 최종 배송을 완료하면 전액 장학금을 받고, 파산하면 해당 게임의 진행이 모두 초기화된다.

## 프로젝트 정보

| 항목 | 값 |
| --- | --- |
| Unity 프로젝트 | `SubwayCarry` |
| 현재 개발 | 3D 1인칭 지하철 배송 게임 (개발 중) |
| 기존 프로젝트 | Universal 2D 기반 씬·에셋·설정 보존 |
| Unity Editor | `6000.3.7f1` |
| 개발 언어 | C# |
| 코드 편집기 | VSCode |
| 버전 관리 | Git / GitHub |
| 대용량 파일 | Git LFS |

## 문서

- 팀 공유 기준은 `main`이며 3D 작업을 우선한다. 기존 `develop`의 커밋된 통합 작업과 `feature-station`의 역·객차 원본을 포함한다. 개인 PC의 미커밋 수정까지 배포된 것은 아니다.
- **3D 통합 게임:** `Assets/_Project/Scenes/Gameplay3D/SubwayCarry_3D_FirstPerson_Delivery.unity`
- **3D 게임 작업 폴더:** `Assets/_Project/Scenes/Gameplay3D`의 `Scripts`, `Data`, `Prefabs`, `Art`, Blender 원본 `Source~`
- **팀원 3D 맵 원본:** `Assets/_Project/Scenes/GachonUniv_3D.unity`, `Subway_3D.unity`, `Wangsimni_3D.unity`
- 팀원 맵 원본은 공간 제작·검토용이며, 통합 게임의 배송·승객 AI가 자동으로 연결된 씬은 아니다. 연결은 별도로 검증한다.
- **2D 비교용 통합 씬:** `Assets/_Project/Scenes/Prototype/ArtMap_GameplaySlice/ArtMap_01_Gachon_Train_Wangsimni_Playable.unity`

Unity `6000.3.7f1`에서 `SubwayCarry` 프로젝트와 위 씬을 직접 연다. 기존 씬을 Builder로 재생성하지 않는다. 2D 씬·스프라이트·프리팹·GUID·원래 경로와 팀원의 `Scenes/Legacy` 비교 씬은 보존한다.

기존 시작 씬·Build Settings·공용 2D 렌더 설정은 유지하고 3D 씬은 전용 파이프라인을 사용한다. Git 통합과 정적 검사는 완주·시각 품질·성능의 최종 검증을 뜻하지 않는다.

미커밋 변경이 없다면 기존 저장소에서 아래 순서로 받는다. 변경이 있다면 먼저 본인 작업을 보존하고, 갈라진 이력을 강제 덮어쓰지 않는다.

```sh
git fetch origin
git switch main
git pull --ff-only origin main
git lfs pull
```

새 작업은 최신 `main`에서 시작한다. 같은 씬·공유 파일의 동시 편집은 담당자를 먼저 정한다.

- [팀 원본 보존·역할별 작업·통합 테스트 방식](docs/TEAM_WORKFLOW.md)
- [버티컬 슬라이스 실행 안내](docs/VERTICAL_SLICE.md)
- [게임 기획서](docs/GAME_DESIGN.md)
- [개발 작업 분해](docs/DEVELOPMENT_BREAKDOWN.md)
- [프로젝트 구조](docs/PROJECT_STRUCTURE.md)

## 저장소 구조

```text
graduation-game-project/
|-- SubwayCarry/        Unity 프로젝트
|-- docs/               프로젝트 문서
|-- .vscode/            VSCode 권장 설정
|-- SubwayCarry/.gitignore Unity 제외 규칙
|-- .gitattributes      텍스트 및 Git LFS 규칙
`-- README.md           저장소 안내
```
