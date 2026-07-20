# 프로젝트 구조

프로젝트 전용 Unity Asset은 `SubwayCarry/Assets/_Project`에 저장한다.

```text
_Project/
|-- Art/                 그래픽과 원본 이미지
|-- Audio/               음악과 효과음
|-- Data/                ScriptableObject와 게임 데이터
|-- Prefabs/             재사용 GameObject
|-- Scenes/              실행 Scene과 Prototype Scene
|-- Scripts/
|   |-- AI/              승객 판단과 혼잡도 Simulation
|   |-- Core/            Bootstrap, 공용 Service, Save, Scene 전환
|   |-- Delivery/        배송, 경제, 성장과 Route 상태
|   |-- Gameplay/        Player, Package, 열차 움직임과 상호작용
|   |-- Transit/         역, 열차, 문과 Route Runtime
|   `-- UI/              Menu, HUD, 노선도, 정산과 Feedback
|-- Tests/               Edit Mode와 Play Mode Test
`-- UI/                  UI 문서, Sprite와 Prefab
```

Unity 템플릿과 Package가 소유한 Asset은 `_Project` 밖에 남아 있어도 된다.

## Scene

```text
Assets/_Project/Scenes/Bootstrap.unity
Assets/_Project/Scenes/MainMenu.unity
Assets/_Project/Scenes/GachonHub.unity
Assets/_Project/Scenes/StationGameplay.unity
Assets/_Project/Scenes/TrainGameplay.unity
Assets/_Project/Scenes/Prototype_Core.unity
Assets/_Project/Scenes/Prototype_AI.unity
```

현재 `Bootstrap.unity`와 AI 이동 시험용 `Prototype_AI.unity`를 사용한다. 나머지 Scene은 해당 마일스톤을 시작할 때 만든다.

## Namespace

Root Namespace는 `SubwayCarry`를 사용한다.

- `SubwayCarry.Core`
- `SubwayCarry.Gameplay`
- `SubwayCarry.AI`
- `SubwayCarry.Transit`
- `SubwayCarry.Delivery`
- `SubwayCarry.UI`

## 코드 배치 기준

- Runtime 코드는 담당 기능과 일치하는 `Scripts` 하위 폴더에 둔다.
- 여러 분야에서 함께 사용하는 Interface와 Service만 `Core`에 둔다.
- 배송, 노선, 승객, 난이도 수치는 코드에 직접 적지 않고 Data Asset으로 관리한다.
- Test 전용 Scene과 Data는 이름으로 구분하고 Release 콘텐츠와 분리한다.
