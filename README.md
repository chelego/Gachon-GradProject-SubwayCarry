# 가천대학교 졸업프로젝트 SubwayCarry

`SubwayCarry`는 2026학년도 2학기 가천대학교 졸업프로젝트로 개발하는 Unity 게임이다.

플레이어는 혼잡한 수도권 지하철에서 케이크를 목적지까지 운반한다. 배송마다 노선, 교통비, 물건 가치, 혼잡도와 손해 위험이 달라진다. 최종 배송을 완료하면 전액 장학금을 받고, 파산하면 해당 게임의 진행이 모두 초기화된다.

## 프로젝트 정보

| 항목 | 값 |
| --- | --- |
| Unity 프로젝트 | `SubwayCarry` |
| 템플릿 | `Universal 2D` |
| Unity Editor | `6000.3.7f1` |
| 개발 언어 | C# |
| 코드 편집기 | VSCode |
| 버전 관리 | Git / GitHub |
| 대용량 파일 | Git LFS |

## 문서

상세 문서는 모두 [`docs/`](docs/README.md)에 있다.

- [게임 기획서](docs/GAME_DESIGN.md)
- [개발 작업 분해](docs/DEVELOPMENT_BREAKDOWN.md)
- [프로젝트 구조](docs/PROJECT_STRUCTURE.md)
- [협업 규칙](docs/CONTRIBUTING.md)

## 저장소 구조

```text
graduation-game-project/
|-- SubwayCarry/        Unity 프로젝트
|-- docs/               프로젝트 문서
|-- .vscode/            VSCode 권장 설정
|-- .gitignore          Unity 제외 규칙
|-- .gitattributes      텍스트 및 Git LFS 규칙
`-- README.md           저장소 안내
```

## 브랜치

| 브랜치 | 용도 |
| --- | --- |
| `main` | 안정 버전 |
| `develop` | 개발 통합 브랜치 |
| `feature/*` | 기능 개발 |
| `fix/*` | 오류 수정 |

개발은 `develop`에서 분기하고 Pull Request를 통해 다시 `develop`에 합친다.
