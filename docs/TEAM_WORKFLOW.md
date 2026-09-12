# 팀 원본 + 통합 테스트 작업 방식

## 원칙

각 팀원이 올린 원본 폴더, 씬 이름, 프리팹과 GUID를 그대로 채용한다. 통합 담당자가 팀원 구현을 별도 폴더로 복제하거나 `TeamReview` 네임스페이스로 다시 만들지 않는다. 원본을 참조하는 통합 씬과 연결 코드만 별도로 관리한다.

| 구분 | 담당 | 기준 브랜치 | 기존 원본/작업 위치 |
| --- | --- | --- | --- |
| 플레이어·운반·균형·파손 | 김준 | `role/player-gameplay` | `Assets/_Project/Scripts/Gameplay`, `Prefabs/Gameplay`, `Art/Concepts/Characters`, `Scenes/Prototype_Core.unity` |
| 역·객차 아트 및 맵 | 정준호 | `feature-station` | `Assets/_Project/Art/Sprites`, `Scenes/GachonUniv.unity`, `Scenes/Subway.unity`, `Scenes/Wangsimni.unity`, 해당 문 프리팹 |
| 배송·경제·강화·UI | 조재형 | `role/delivery-economy`, 기존 `stage-economy-test` | `Assets/_Project/Scripts/Delivery`, `Data/Delivery`, 관련 UI 및 기존 프로토타입 코드 |
| 승객 AI와 회귀 테스트 | 신찬욱·조재형 | `role/passenger-ai` | `Assets/_Project/Scenes/Ai_v2`, `Scripts/AI` |
| 전체 연결과 통합 테스트 | 신찬욱 | `develop` 통합 | `Assets/_Project/Scenes/Prototype/ArtMap_GameplaySlice` |
| 공통 계약 | 관련 담당자 공동 | `develop` 기준 합의 | `Assets/_Project/Scripts/Core/Contracts` |

위 표는 소유 범위를 설명한다. 파일을 이 표에 맞춰 새 경로로 옮기라는 지시가 아니다. 경로 앞의 `Assets`는 저장소의 `SubwayCarry/Assets`이다.

## 통합 폴더

현재 `ArtMap_GameplaySlice`가 통합 테스트 폴더다. 테스트를 위해 이름을 바꾸거나 새 버전 폴더로 복제하지 않는다.

- 진입 씬: `ArtMap_01_Gachon_Train_Wangsimni_Playable.unity`
- `Scripts`: 원본 플레이어와 AI를 연결하는 어댑터, 이동 경계, 맵 전환 및 상황 제공 코드
- `Player_Slice.prefab`: 통합용 속도·윤곽·크기 등 테스트 설정. 스크립트와 이미지 원본은 공용 자산을 참조
- 통합 씬의 맵 배치는 테스트용 인스턴스/데이터다. 팀원 원본 씬을 덮어쓰거나 자동 재생성하지 않는다.
- 소스 맵 갱신은 자동으로 기존 통합 배치에 전파되는 것이 아니다. 통합 담당자가 변경을 비교하고 통합 씬에 반영·검증한다.
- `Apply Player Sitting Sprites` 통합 메뉴는 공용 이미지/프리팹을 읽고 통합 프리팹만 갱신한다. 원본 이미지 임포트와 원본 프리팹 생성은 플레이어 담당 도구의 역할이다.

## 반복 작업 순서

1. 팀원은 자기 작업 브랜치에서 최신 `develop`을 확인한다. 로컬 수정과 충돌을 보존하며 수신한다.
2. 자신이 담당하는 원본 폴더의 기능을 수정하고 담당 씬에서 시험한다.
3. 해당 파일과 필요한 `.meta`만 커밋하여 자기 브랜치에 올린다. 다른 담당자의 파일, 통합 씬, 개인 설정을 섞지 않는다.
4. 통합 담당자가 변경 내용을 확인하고 `develop`에 병합한다. 연결 수정은 통합 폴더에서 처리한다.
5. 공통 인터페이스 변경이 필요하면 생산자·소비자를 함께 확인하고 관련 담당자와 맞춘다. 각자 다른 계약 복사본을 만들지 않는다.
6. 정적 참조·컴파일 검사와 사용자 플레이 확인을 구분한다. 문제를 팀원 원본으로 되돌려 고쳐야 한다면 담당자에게 파일과 증상을 전달한다.

`Scenes/Prototype/GameplayFlow_Prototype.unity`처럼 여러 역할이 이미 함께 사용하는 파일은 각자 독립 소유 파일로 취급하지 않는다. 편집 전에 담당자를 정하고, 변경이 겹치면 통합 담당자가 3-way로 병합한다. 이 예외를 해결하려고 원본을 역할별 복사본으로 늘리지 않는다.

## 공통 환경

이번 수신 커밋, 정적 검사 범위 및 원본 담당자 확인 사항은 [INTEGRATION_REVIEW.md](INTEGRATION_REVIEW.md)에 기록한다.

- Unity 버전은 `ProjectSettings/ProjectVersion.txt`, 패키지는 `Packages/manifest.json`과 잠금 파일을 기준으로 한다.
- 폴더를 받기 위해 Unity 버전, 입력/렌더 설정, 시작 씬, Build Settings를 임의로 교체하지 않는다.
- 개인 `SceneTemplateSettings.json`, `_Recovery`, IDE/Library/Temp/빌드 파일은 공유하지 않는다.
- 다른 팀원의 작업 브랜치나 `main`을 강제 갱신·삭제하지 않는다. 이 구조를 받는 팀원도 자신의 기존 수정과 이력을 보존해야 한다.
