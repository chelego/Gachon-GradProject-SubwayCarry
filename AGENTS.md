# AGENTS.md - SubwayCarry 팀 AI 작업 기준

## 1. 판단 기준

충돌이 생기면 다음 순서로 판단한다.

1. 현재 사용자가 직접 요청한 내용
2. `docs/GAME_DESIGN.md`의 확정된 게임 규칙
3. `docs/DEVELOPMENT_BREAKDOWN.md`의 역할과 개발 범위
4. `docs/PROJECT_STRUCTURE.md`의 폴더와 코드 배치 기준
5. 현재 브랜치의 코드, Scene, Prefab과 Data Asset

작업 전 현재 브랜치와 `git status`를 확인한다. 기존 변경은 작성자를 추측해 되돌리지 않는다. 기획 문서와 구현이 다르면 임의로 한쪽을 확정하지 않고 차이를 먼저 보고한다.

### 팀 원본과 통합 작업 (2026-09-13 사용자 확정)

- `docs/TEAM_WORKFLOW.md`를 함께 읽는다. 팀원이 올린 원본 경로·씬·GUID·공개 계약을 그대로 채용한다.
- 팀원은 담당 폴더를 갱신하고, 통합 담당자는 `ArtMap_GameplaySlice`의 연결 코드와 통합 씬을 관리한다. 원본 구현을 `TeamReview` 등으로 복제하거나 별도 네임스페이스로 분리하지 않는다.
- 원본 자체의 오류는 임의 재구성하지 않고 담당자에게 증거를 전달한다. 통합 연결 수정과 원본 기능 수정을 구분한다.
- 공유 파일 변경은 담당자 간 계약을 확인한다. 원본 씬을 통합 씬으로 대체하지 않는다.
- 컴파일·정적 참조 검사는 사용자 플레이 승인과 다르다. 명시적으로 요청한 경우에만 커밋·푸시한다.

## 2. 프로젝트 기준

- 엔진: Unity `6000.3.7f1`
- 템플릿: Universal 2D
- 렌더링: Universal Render Pipeline `17.3.0`
- 입력: Unity Input System `1.18.0`
- 플랫폼: PC
- 시점: 2D 쿼터뷰
- Root Namespace: `SubwayCarry`
- Git 기준 브랜치: `main`, `develop`

`SubwayCarry`는 수도권 지하철에서 케이크를 운반하는 배송 게임이다. 플레이어는 이동, 위치 선정, 바라보는 방향과 운반 자세로 충돌과 압박을 피한다. NPC는 플레이어를 공격하는 적이 아니라 자신의 목적에 따라 탑승, 이동, 대기와 하차를 수행하는 승객이다.

핵심 진행:

```text
가천대역에서 배송 선택
-> 출발역과 승강장 이동
-> 열차 탑승
-> 케이크 보호
-> 필요한 환승 수행
-> 목적지역 출구 도착
-> 손상과 비용 정산
-> 가천대역 복귀
```

첫 완성 범위는 무환승 배송 한 건이다. 배송 선택부터 이동, 열차 플레이, 도착, 정산과 복귀까지 한 사이클을 먼저 연결한 뒤 역, 노선과 승객 종류를 확장한다.

## 3. 게임 규칙

### 플레이 구간

- 출발역의 필요한 구간은 직접 이동한다.
- 객차 안에서 이동, 자세 전환, 균형잡기와 충돌 회피를 수행한다.
- 일반 정차역에서는 열차에서 내리지 않고 승하차와 혼잡도 변화를 겪는다.
- 필수 환승역에서는 직접 내려 환승 통로를 지나 다음 열차에 탑승한다.
- 목적지역에서는 출구 도착 시 배송을 완료한다.
- 역 외부 이동은 구현하지 않는다.
- 실제 수도권 지하철을 참고하지만 중요도가 낮은 역과 긴 통로는 게임에 맞게 축소한다.

### 플레이어와 운반

- 기본 자세는 케이크 상자를 몸 앞에서 두 손으로 들고 서 있는 상태다.
- 앉기, 기대기와 지지물 잡기는 이동할 수 없다.
- 머리 위 운반은 이동 가능하지만 느려지고 스테미너를 소모한다.
- 자세 전환에는 시간이 필요하며 전환 중에는 이동할 수 없다.
- 열차 출발, 정차와 속도 변화 시 서 있기와 머리 위 운반 상태에서 균형잡기가 발생한다.
- 케이크 상자와 케이크는 별도 내구도를 가진다.
- 약한 충격은 상자를 먼저 손상시키고 보호 한계를 넘은 피해는 케이크에 전달한다.
- 피해는 운반 상태 이름이 아니라 실제 충돌 속도, Hitbox 접촉과 물체 사이 압박을 기준으로 계산한다.

### 승객 AI

- 승객은 하차역, 성격, 현재 목표와 주변 상황을 기준으로 행동한다.
- 기본 흐름은 대기, 탑승, 위치 선택, 객차 행동, 하차 준비와 하차다.
- 좌석, 기대는 위치, 손잡이와 서 있는 위치를 혼잡도에 따라 선택한다.
- 플레이어와 다른 승객의 위치를 이동 판단에 포함한다.
- 실제 승객처럼 회피, 양보, 대기와 좁은 공간 통과를 수행한다.
- 플레이어를 추적하거나 공격하는 행동은 넣지 않는다.

AI 적용 기술:

- `A* Pathfinding`: 목적지까지 기본 경로 계산
- `Dynamic Cost Map`: 혼잡과 장애물에 따른 경로 비용 반영
- `Finite State Machine`: 탑승부터 하차까지 상태 관리
- `Utility AI`: 성격, 좌석, 혼잡도와 하차 시점을 점수화
- `Local Avoidance / Steering`: 다른 승객과 플레이어 회피
- `Minimax`: 좌석 경쟁과 좁은 통로의 일부 경쟁 판단에만 사용 예정

문서에 적힌 기술을 모두 구현된 것으로 간주하지 않는다. 현재 코드에서 실제 사용 여부를 먼저 확인한다.

### 경제와 실패

- 화폐는 현금으로 통일한다.
- 배송 수입은 배달 수수료에서 배상금과 실제 부담 교통비를 뺀 값이다.
- 상자와 케이크가 모두 무손상이면 복귀 교통비를 지원한다.
- 손상 배송은 복귀 교통비를 플레이어가 부담한다.
- 배송 실패 시 배상금과 가천대역 복귀 비용을 부담한다.
- 현금이 부족하면 파산 처리하고 해당 게임의 진행을 초기화한다.

## 4. 역할 경계

| 역할 | 담당 범위 |
| --- | --- |
| 역할 1 | 플레이어 이동, 운반 자세, 상호작용, 충돌, 파손과 균형잡기 |
| 역할 2 | 승객 탑승·이동·좌석·하차 AI, 돌발행동과 혼잡도 |
| 역할 3 | 역, 열차, 문, 환승, 노선 진행, Scene과 맵 구성 |
| 역할 4 | 배송 선택, HUD, 정산, 경제, 강화, 보험, 저장과 진행 상태 |

담당 기능은 각 역할 폴더와 Prototype Scene에서 먼저 검증한다. 다른 역할의 코드가 필요하면 구체 구현을 복사하거나 직접 수정하기 전에 전달할 데이터와 호출 경계를 확인한다.

주요 연동:

- 역할 1 <-> 역할 2: 플레이어와 승객 Collider, 충돌 속도와 이동 정보
- 역할 1 <-> 역할 3: 열차 움직임, 문, 좌석, 벽과 손잡이
- 역할 1 <-> 역할 4: 내구도, 스테미너와 균형잡기 HUD
- 역할 2 <-> 역할 3: 승하차 지점, 이동 가능 영역과 혼잡 구역
- 역할 2 <-> 역할 4: 실제 혼잡도와 승객 상태
- 역할 3 <-> 역할 4: 현재 역, Route 진행, 환승과 배송 도착 여부

## 5. 폴더와 Namespace

프로젝트 전용 Asset은 `SubwayCarry/Assets/_Project` 아래에 둔다.

```text
Assets/_Project
|-- Art
|-- Audio
|-- Data
|-- Prefabs
|-- Scenes
|-- Scripts
|   |-- AI
|   |-- Core
|   |-- Delivery
|   |-- Gameplay
|   |-- Transit
|   `-- UI
|-- Tests
`-- UI
```

Namespace:

- `SubwayCarry.Core`
- `SubwayCarry.Gameplay`
- `SubwayCarry.AI`
- `SubwayCarry.Transit`
- `SubwayCarry.Delivery`
- `SubwayCarry.UI`

Runtime 코드는 담당 기능과 같은 `Scripts` 하위 폴더에 둔다. 배송, 노선, 승객 구성과 난이도 수치는 코드에 직접 적지 않고 Data Asset으로 관리한다.

## 6. Scene과 Prefab

사용 Scene:

- `Bootstrap.unity`: 공용 데이터와 첫 화면 준비
- `MainMenu.unity`: 새 게임, 이어하기, 설정과 종료
- `GachonHub.unity`: 배송 선택과 학교 서비스
- `StationGameplay.unity`: 출발역, 환승역과 목적지역
- `TrainGameplay.unity`: 객차, 승객, 혼잡도와 운반 Gameplay
- `Prototype_Core.unity`: 플레이어, 파손과 균형잡기 시험
- `Prototype_AI.unity`: 일반 승객 행동 시험

아직 없는 Scene은 해당 작업을 시작할 때 `_Project/Scenes` 아래에 만든다. 기능 시험을 위해 Unity 기본 `Assets/Settings/Scenes/URP2DSceneTemplate.unity`를 수정하지 않는다.

공용 Prefab 후보:

- Player
- 케이크 상자와 케이크
- 공통 승객
- 객차, 문, 좌석과 손잡이
- 역 모듈과 승하차 지점

Scene이나 Prefab을 수정하면 연결된 Script와 `.meta`를 함께 확인한다. 한 Scene의 대규모 변경은 다른 담당자의 작업과 겹치는지 먼저 확인한다.

## 7. 공용 Data

- `DeliveryData`: 목적지, 물건 가치, 수수료, 시간대, 난이도와 Route
- `RouteData`: 정차역과 환승 구간 순서
- `StationData`: 역, 승강장, 문 방향, 시설 배치와 시간대별 승하차 기준
- `PassengerData`: 승객 유형, 성격과 행동 성향
- `CrowdData`: 역과 시간대별 승객 수와 구성
- `UpgradeData`: 강화 종류, 단계, 가격과 효과
- `ServiceData`: 보험과 교통비 지원의 가격과 적용 범위

현재 `StationData`는 `SubwayCarry.Transit`의 `ScriptableObject`다.

```text
StationData
-> 역 ID, 이름, 환승 여부와 연결 노선
-> 승강장, 노선, 진행 방향과 문 열림 방향
-> 계단, 에스컬레이터, 엘리베이터, 환승 통로와 출구 배치
-> 시간대
-> 출입문별 혼잡 가중치와 승하차 인원
-> 시간대별 승객 구성
```

`StationData`는 역 자체의 설정이고 배송 번호, 보상, 물건 가치와 배송 개방 조건은 넣지 않는다. `CrowdData`를 별도 구현할 때는 `StationData`의 시간대별 승객 정보와 중복 저장하지 말고 먼저 소유 범위를 정한다.

## 8. 공통 Interface v1

공통 계약 기준 버전은 `v1`이다. 공통 계약은 각 역할의 기능을 대신 구현하지 않고 Gameplay, AI, Transit, Delivery와 UI가 같은 상태와 요청 형식을 사용하도록 맞춘다.

계약별 필드, 생산자, 소비자와 현재 구현 상태는 `docs/COMMON_INTERFACES.md`를 함께 확인한다. 문서와 코드가 다르면 `Assets/_Project/Scripts/Core/Contracts`의 C# 선언을 기준으로 판단하고 차이를 보고한다.

위치:

```text
Assets/_Project/Scripts/Core/Contracts
```

### 상호작용

공통 타입:

- `InteractionKind`
- `InteractionContext`
- `InteractionResult`
- `IInteractable`

좌석, 기대는 벽, 손잡이, 문과 역 시설은 `IInteractable`을 구현한다. 플레이어는 대상의 구체 클래스 대신 `CanInteract`와 `TryInteract`를 호출한다. 상호작용 위치는 `InteractionAnchor`를 사용한다.

### 플레이어 자세와 스테미너

공통 타입:

- `CarryPosture`
- `PlayerCarryStateSnapshot`
- `IPlayerCarryStateProvider`
- `StaminaStateSnapshot`
- `IStaminaStateProvider`

플레이어 구현이 현재 자세, 전환 여부, 이동 가능 여부와 스테미너를 소유한다. HUD와 다른 역할은 플레이어 컴포넌트를 직접 조회하지 않고 Provider의 현재 Snapshot과 변경 이벤트를 사용한다.

### 균형잡기

공통 타입:

- `BalanceInputDirection`
- `BalanceStateSnapshot`
- `IBalanceStateProvider`

균형잡기 구현은 현재 요구 입력, 남은 시간과 실패 여부를 제공한다. UI는 Snapshot을 받아 `W!`, `A!`, `S!`, `D!` 표시를 갱신한다. 열차 출발과 정차는 `ITrainMotionProvider`로 균형 시스템에 전달한다.

### 케이크 충돌과 내구도

공통 흐름:

```text
NPC·벽·문·좌석·바닥과 충돌 또는 압박
-> PackageImpactData 생성
-> IPackageImpactReceiver.ApplyImpact
-> 상자 보호와 케이크 피해 계산
-> PackageDurabilitySnapshot 변경
-> HUD와 배송 결과 반응
```

공통 타입:

- `PackageImpactData`
- `IPackageImpactReceiver`
- `PackageDurabilitySnapshot`
- `IPackageDurabilityProvider`

충돌을 감지한 쪽은 상대 속도, 접촉 위치와 압박 비율을 전달한다. 실제 상자와 케이크 피해 계산은 운반물 구현이 소유한다. UI와 정산은 내구도 Snapshot만 읽는다.

### 승객 혼잡도

공통 타입:

- `CrowdLevel`
- `CrowdStateSnapshot`
- `ICrowdStateProvider`

승객 AI는 실제 승객 수와 사용 가능한 공간을 기준으로 여유, 보통, 혼잡과 극심한 혼잡을 계산한다. UI와 Stage는 개별 승객 배열을 직접 세지 않고 혼잡도 Provider를 사용한다.

### 열차와 노선 진행

공통 타입:

- `DoorOpeningSide`
- `TrainDoorState`
- `TrainDoorSnapshot`
- `ITrainDoorStateProvider`
- `TrainMotionPhase`
- `TrainMotionSnapshot`
- `ITrainMotionProvider`
- `TransitProgressSnapshot`
- `ITransitProgressProvider`

역과 열차 시스템은 현재 역, 다음 역, Route 순서, 환승·목적지역 여부와 문 방향을 제공한다. 열차 움직임은 관성 방향과 강도를 제공하고 플레이어 균형 시스템이 이를 사용한다. `StationData`도 같은 `DoorOpeningSide`를 사용한다.

### 배송과 경제

공통 타입:

- `DeliveryPhase`
- `DeliveryFailureReason`
- `DeliveryStateSnapshot`
- `IDeliveryStateProvider`
- `IDeliveryService`
- `EconomyStateSnapshot`
- `IEconomyStateProvider`
- `DeliverySettlementSnapshot`
- `IDeliverySettlementProvider`

배송 시스템은 선택, 이동, 환승, 도착, 완료와 실패 상태를 소유한다. UI는 `IDeliveryService`에 배송 시작을 요청하고 상태 Provider와 경제 Provider를 구독한다. 정산 결과는 배달 수수료, 배상금, 출발·복귀 교통비와 최종 손익을 한 Snapshot으로 전달한다.

### 소유 범위

- 역할 1: 플레이어 자세, 스테미너, 균형잡기, 충돌 전달과 내구도
- 역할 2: 실제 승객 수와 혼잡도
- 역할 3: 열차 움직임, 문 상태, 현재 역과 Route 진행
- 역할 4: 배송 상태, 경제와 정산

Interface 변경은 이름만 바꾸지 않고 구현체, 소비 코드, Scene 연결과 테스트를 같은 작업에서 확인한다. 아직 구현체가 없는 계약은 구현된 기능으로 보고하지 않는다.

## 9. 현재 승객 Prototype

관련 코드:

- `GeneralPassengerPrototype.cs`: 승객 상태와 행동 선택
- `PassengerDoorway.cs`: 탑승·하차 문과 이동 지점
- `PassengerSeatPrototype.cs`: 좌석 점유와 착석 위치
- `PassengerActivityPoint.cs`: 손잡이, 기대기와 대기 위치
- `GridNavigation2D.cs`: 이동 가능 영역과 A* 경로
- `PassengerIntentCoordinator.cs`: 승객 등록, 근접 조회와 이동 의도 전달
- `TrainDoorCyclePrototype.cs`: 문 개폐와 승하차 주기
- `AiPrototypeSceneBuilder.cs`: 승객 AI 시험 Scene 구성

현재 Scene 기준:

- `Prototype_AI.unity`는 `SubwayCarry/Prototype/Build Passenger AI Scene` 메뉴로 다시 생성한다.
- 객차는 두 칸이며 승객 60명과 플레이어는 첫 번째 객차에서 시험한다.
- 첫 번째 객차 좌석은 일반 좌석 42칸과 노약자석 6칸으로 총 48칸이다.
- 승객 60명은 혼잡 상황을 확인하기 위한 스트레스 테스트 값이다.
- 승객 수를 바꿀 때 행동 가중치, 회피, 착석과 객차 크기를 같이 조정하지 않는다.
- Builder가 생성하는 벽, 문, 좌석과 활동 지점은 Scene에서만 고치지 않는다. 다시 생성해도 남아야 하는 변경은 `AiPrototypeSceneBuilder.cs`에 반영한다.

현재 행동 기준:

- 문이 열리면 하차 승객이 먼저 나가고 탑승 승객이 한쪽 승강장에서 들어온다.
- 입장 중에는 좌석 판단이나 승객 회피 때문에 출입문 앞에서 멈추지 않는다.
- 열리는 쪽 좌석을 고른 승객은 문을 통과한 뒤 바로 옆으로 빠져 앉는다.
- 선택한 좌석 앞이 붐비면 선택 좌석 기준 좌우 두 칸 안의 빈 좌석으로 바꿔 앉을 수 있다.
- 문이 열린 동안 자유롭게 서는 승객은 좌석과 벽 쪽 여유 공간을 우선 사용한다.
- 중앙 통로에 자리를 잡는 행동은 열차 운행 중에만 허용한다.
- 양쪽 손잡이 사용자가 있는 통로는 승객 한 명이 몸을 좁혀 통과할 정도의 폭을 기준으로 한다.
- 좌석, 손잡이와 기대는 위치가 점유되면 다른 위치를 찾거나 가까운 빈 공간에 선다.
- 하차 의도와 통과 의도는 `PassengerIntentCoordinator`로 주변 승객에게 전달한다.
- 좌석 점유가 확정되면 같은 좌석을 향하던 승객에게 즉시 전달한다.
- 하차를 마친 승객 GameObject는 승강장 밖에서 제거한다.
- 플레이어 위치도 승객의 혼잡, 경로와 회피 판단에 포함한다.

이 상태의 탑승, 착석, 양보, 충돌과 객차 간격은 현재 승객 AI의 기준 동작이다. 승객 종류나 성격을 추가할 때 이 흐름을 통째로 교체하지 않고 가중치와 추가 행동으로 확장한다. 60명에서 문제가 생기면 승객 수를 먼저 낮춰 비교하고 기존 행동 기준을 되돌리지 않는다.

승객 코드는 `mapRoot` 아래의 문, 좌석과 활동 지점을 찾아 동작한다. 특정 Prototype Scene 좌표나 승객 번호를 Runtime 규칙에 직접 고정하지 않는다.

새 승객 행동은 공통 흐름을 유지하고 성향값이나 작은 행동 모듈로 확장한다. 매 승객이 매 프레임 전체 Scene을 검색하는 구조는 추가하지 않는다. 근접 승객 조회와 공용 의도 전달은 현재 `PassengerIntentCoordinator`를 우선 확인한다.

`PassengerIntentCoordinator`는 승객 등록, 공간 Hash 기반 근접 조회, 좌석 점유와 이동 의도 전달을 맡는다. 인원 증가로 성능 문제가 생기면 감지, 판단과 경로 갱신 주기를 중앙에서 나누는 방향으로 확장하고 승객마다 별도 Manager나 전체 Scene 검색을 만들지 않는다. 최적화 결과는 Unity Profiler 측정값으로 확인한다.

## 10. Unity와 생성 파일

- Asset Serialization은 `Force Text`를 유지한다.
- Version Control Mode는 `Visible Meta Files`를 유지한다.
- Unity Asset을 추가, 이동하거나 삭제하면 대응하는 `.meta`를 함께 반영한다.
- `Library`, `Temp`, `Logs`, `Obj`, 빌드 결과와 IDE 캐시는 커밋하지 않는다.
- `Assets/Screenshots`, `Assets/_Recovery`와 개인 `SceneTemplateSettings.json`은 기능 Asset으로 사용하기로 합의하지 않는 한 제외한다.
- Package 추가와 Unity 버전 변경은 다른 팀원의 환경에 영향을 주므로 이유와 검증 결과를 함께 남긴다.

## 11. Git 반영 기준

- 기능 통합 대상은 `develop`이다.
- `main` 반영은 별도 요청이나 통합 확인 뒤 진행한다.
- 하나의 커밋에는 같은 기능에 필요한 코드, Asset과 `.meta`만 포함한다.
- 다른 담당자의 변경과 생성 파일을 같은 커밋에 섞지 않는다.
- 작업 중인 기능이 실제 Scene에서 검증되기 전에는 완료로 표현하지 않는다.
- 커밋과 Push는 사용자가 명시적으로 요청한 범위에서만 진행한다.

## 12. AI 에이전트 작업 순서

1. 이 파일과 관련 `docs`를 확인한다.
2. 현재 브랜치, 변경 파일과 관련 코드의 실제 사용처를 찾는다.
3. Scene, Prefab, Data Asset과 `.meta` 연결을 확인한다.
4. 담당 범위 안에서 필요한 만큼만 수정한다.
5. 기존 사용자 변경과 다른 담당자의 작업은 보존한다.
6. C# 컴파일과 Unity Console을 확인한다.
7. 기능이 연결된 Scene에서 실제 동작을 확인한다.
8. 변경 파일, Inspector 연결, 검증 결과와 남은 작업을 보고한다.

설치, 코드 작성, Scene 연결과 Runtime 검증을 구분해서 보고한다. C# 파일이 컴파일됐다는 이유만으로 플레이 가능한 기능이 완성됐다고 말하지 않는다.

## 13. 완료 기준

- 코드가 Unity `6000.3.7f1`에서 컴파일된다.
- Unity Console에 새 Error가 없다.
- 필요한 Component가 Scene 또는 Prefab에 실제로 연결됐다.
- 기능을 담당 Prototype 또는 통합 Scene에서 실행했다.
- 다른 역할이 읽을 데이터와 상태가 확인됐다.
- 새 Asset과 `.meta`가 함께 포함됐다.
- 생성 파일과 관계없는 변경이 커밋에서 제외됐다.
- 검증하지 못한 항목은 검증하지 못했다고 명시한다.

## 14. 작업 결과 보고

```text
변경한 내용
- ...

추가·수정한 파일
- ...

Scene·Prefab·Data 연결
- ...

검증 결과
- ...

남은 작업
- ...
```

## 15. PassengerAI V2 작업

전체 역사와 열차 여정을 인식하는 새 승객 AI 작업은 다음 문서를 필수 지침으로 사용한다.

```text
SubwayCarry/Assets/_Project/Scenes/Prototype/PassengerAI_V2/AGENTS.md
SubwayCarry/Assets/_Project/Scenes/Prototype/PassengerAI_V2/PASSENGER_AI_V2_GUIDE.md
```

- PassengerAI V2 관련 파일을 만들거나 수정하기 전에 두 문서를 모두 읽는다.
- V1 `UtilityJourney_AI`는 비교 기준으로 보존하고, 명시적 요청 없이 V2 작업과 함께 수정하거나 재생성하지 않는다.
- HTN 여정 계획, Utility 세부 선택, Smart Object 실행, Social Navigation과 Motor의 역할을 한 클래스에 다시 합치지 않는다.
- 전체 승객 순회, 매 프레임 Scene 검색, 반복 A*와 Hot Path 할당을 추가하지 않는다.
- 최적화 완료 주장은 동일 조건의 Unity Profiler 전후 측정이 있을 때만 한다.
- 자연스러운 움직임과 시각 배치는 사용자가 Play Mode에서 최종 판단한다.
