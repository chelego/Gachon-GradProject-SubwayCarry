# SubwayCarry 개발 작업 분해

## 1. 문서 목적

게임 구현을 큰 개발 분야와 세부 작업으로 나눈다. 팀원은 `GAME_DESIGN.md`와 이 문서를 읽고 주 담당 1개와 보조 담당 1개를 선택한다.

아래 클래스와 함수 이름은 초기 권장안이다. 구현 과정에서 이름이나 내부 구조는 변경할 수 있지만 담당 영역 사이의 입력과 출력은 합의해서 유지한다.

## 2. 개발 분야

| 분야 | 담당 내용 |
| --- | --- |
| A. 핵심 Gameplay와 Physics | Player 조작, 운반 상태, 충돌, 압박, 내구도, 열차 움직임, 균형잡기 |
| B. 승객 AI와 혼잡도 Simulation | 승객 판단, 이동, 좌석, 승하차, 행동 모듈과 인원 관리 |
| C. 지하철 World와 Stage | 역, 열차, 문, 환승, Route Data, Scene 전환과 Map 콘텐츠 |
| D. 배송 System과 UI | 배송 선택, 경제, 정산, 강화, 보험, Save, HUD와 Menu |
| E. Integration과 Quality | 공용 Data, Test, 성능, Build, 콘텐츠 조립과 통합 |

각 분야는 주 담당자를 정하되 기능 경계를 넘어가는 작업은 Interface를 합의한 뒤 함께 처리한다.

## 3. 필요한 Scene

| Scene | 용도 |
| --- | --- |
| `Bootstrap.unity` | 초기화, 공용 Service 등록, Save Load, 첫 Scene 이동 |
| `MainMenu.unity` | 새 게임, 이어하기, 설정, 나가기 |
| `GachonHub.unity` | 배송 선택, 학교 서비스, 출발 준비 |
| `StationGameplay.unity` | 출발역, 환승역과 목적지역을 Data와 Prefab으로 교체해 사용하는 공용 역 Scene |
| `TrainGameplay.unity` | 노선, 객차, 혼잡도와 Route Data를 불러오는 공용 열차 Scene |
| `Prototype_Core.unity` | 이동, 파손, NPC와 균형잡기를 독립적으로 시험하는 Scene |

역마다 별도 Scene을 만들기보다 `StationGameplay.unity`에서 Station Prefab과 Data를 바꿔 사용한다. 정산은 `GachonHub.unity` 위에 UI로 표시하거나 필요하면 별도 Scene으로 분리한다.

```text
Bootstrap
  -> MainMenu
  -> GachonHub
  -> StationGameplay (출발)
  -> TrainGameplay
  -> StationGameplay (환승)
  -> TrainGameplay
  -> StationGameplay (목적지)
  -> Settlement UI
  -> GachonHub
```

## 4. 공용 Data 정의

권장 `ScriptableObject` 또는 직렬화 Data:

| 이름 | 내용 |
| --- | --- |
| `DeliveryDefinition` | 물건, 가치, 수수료, 시간대, 난이도, Route와 개방 조건 |
| `RouteDefinition` | 승차 구간과 환승 구간의 순서 |
| `StationDefinition` | 역 정보, 연결 노선, 승하차 수치와 Prefab |
| `LineDefinition` | 노선 색상, 열차 종류와 승객 가중치 |
| `PassengerArchetype` | 속도, 좌석 선호도, 행동 가중치와 소지품 |
| `CrowdProfile` | 역과 시간대에 따른 목표 인원과 승객 구성 |
| `UpgradeDefinition` | 강화 종류, 단계, 가격과 적용 값 |
| `UniversityServiceDefinition` | 학교 서비스 가격, 보장 범위와 소모 규칙 |
| `BalanceEventDefinition` | 입력 순서, 제한시간, 충격과 열차 움직임 종류 |

## 5. 공용 Prefab

- `Player.prefab`
- `CakePackage.prefab`
- `CakeBox.prefab`
- `Cake.prefab`
- `PassengerBase.prefab`
- `TrainCar.prefab`
- `TrainDoor.prefab`
- `Seat.prefab`
- `HandleInteraction.prefab`
- `LeanPoint.prefab`
- `StationPlatformModule.prefab`
- `TransferCorridorModule.prefab`
- `StationExitTrigger.prefab`
- `PassengerSpawnPoint.prefab`

## 6. A 분야: 핵심 Gameplay와 Physics

### A1. Player 이동

권장 클래스: `PlayerMovementController`

- `Move(Vector2 input)`: WASD 이동 적용
- `UpdateFacing(Vector2 worldPosition)`: 마우스 위치 방향으로 회전
- `SetMovementEnabled(bool enabled)`: 상태 전환 중 이동 잠금
- `ApplyAgilityModifier(float value)`: 민첩성 강화 적용
- 역과 열차 Collider 충돌 처리

### A2. 운반 상태

권장 클래스: `CarryStateMachine`, `ICarryState`

권장 상태:

- `StandingCarryState`
- `LeaningCarryState`
- `SeatedCarryState`
- `SupportHoldCarryState`
- `OverheadCarryState`

주요 함수:

- `TryChangeState(CarryStateId nextState)`
- `CanEnterState(CarryStateId nextState)`
- `BeginTransition(float duration)`
- `CompleteTransition()`
- `CancelTransition()`

### A3. 스테미너

권장 클래스: `StaminaController`

- `Consume(float amount)`
- `BeginRecoveryDelay()`
- `Recover(float deltaTime)`
- `ApplyStaminaUpgrade(int level)`
- 0 도달 시 `OverheadCarryState` 강제 해제

### A4. 케이크 충돌과 내구도

권장 클래스:

- `PackageDamageController`
- `CakeBoxDurability`
- `CakeDurability`
- `PackageCollisionSensor`

주요 함수:

- `ApplyImpactDamage(ImpactData impact)`
- `CalculateImpactDamage(float relativeSpeed)`
- `TransferOverflowDamage(float damage)`
- `SetVisualDamageStage(DamageStage stage)`
- `HandleDestroyed()`

### A5. 압박 피해

권장 클래스: `CompressionDetector`

- `EvaluateCompression()`
- `CalculateAvailableGap(Collider2D left, Collider2D right)`
- `ApplyCompressionDamage(float depth, float deltaTime)`
- `DrawCompressionDebugGizmos()`
- 떨림으로 피해가 반복 적용되지 않도록 Cooldown 또는 누적 방식을 사용

### A6. 열차 움직임과 균형잡기

권장 클래스:

- `TrainMotionController`
- `BalanceMinigameController`
- `BalanceInputSequence`

주요 함수:

- `TriggerMotionEvent(TrainMotionType type)`
- `CanStartBalanceEvent(CarryStateId state)`
- `StartSequence(BalanceEventDefinition definition)`
- `SubmitInput(KeyCode input)`
- `CompleteBalance()`
- `FailBalance()`
- `ApplyBalanceUpgrade(int level)`

### A 분야 완료 기준

임시 객차에서 이동, 방향 전환, 모든 운반 상태, 열차 움직임 한 종류, 넘어짐, 상자와 케이크 피해를 확인할 수 있어야 한다.

## 7. B 분야: 승객 AI와 혼잡도 Simulation

### B1. 승객 Runtime Data

권장 클래스: `PassengerAgent`, `PassengerRuntimeData`

Data 항목:

- `PassengerArchetype archetype`
- `PassengerPersonality personality`
- `StationId destinationStation`
- `PassengerGoal currentGoal`
- `Transform currentTarget`
- 행동 Cooldown과 소지품

### B2. 공통 상태 흐름

권장 클래스: `PassengerStateMachine`, `IPassengerState`

권장 상태:

- `WaitingToBoardState`
- `BoardingState`
- `SelectingPositionState`
- `MovingToPositionState`
- `RidingState`
- `PreparingToExitState`
- `AlightingState`

주요 함수:

- `ChangeState(PassengerStateId nextState)`
- `EvaluateNextGoal()`
- `SelectRidePosition()`
- `PrepareToExit(StationId station)`
- `HandleTrainStopped(StationId station)`

AI 내부 구조는 Prototype 이후 팀에서 정한다. 외부에는 이동 목표와 행동 결과만 노출한다.

### B3. 이동과 회피

권장 클래스: `PassengerNavigationController`, `LocalAvoidanceController`

- `SetDestination(Vector2 target)`
- `UpdatePath()`
- `CalculateAvoidanceVelocity()`
- `ResolveDeadlock()`
- 열차 내부와 역 승하차 경로
- 개인 공간과 회피 강도

### B4. 좌석

권장 클래스: `SeatManager`, `SeatSlot`

- `TryReserveSeat(PassengerAgent passenger)`
- `ConfirmOccupancy(PassengerAgent passenger)`
- `ReleaseSeat()`
- `FindAvailableSeat()`
- 빈 좌석 인식 지연과 자리 경쟁

### B5. 행동 모듈

권장 Interface: `IPassengerBehavior`

- `PhoneWalkingBehavior`
- `HurryBehavior`
- `EarlyBoardingBehavior`
- `SuddenStopBehavior`
- `DoorBlockingBehavior`
- `LateExitBehavior`
- `GroupFollowBehavior`
- `SeatCompetitionBehavior`

공통 함수:

- `CanExecute(PassengerContext context)`
- `GetWeight(PassengerContext context)`
- `Execute(PassengerContext context)`

### B6. 혼잡도 관리

권장 클래스: `CrowdManager`

- `InitializeCrowd(CrowdProfile profile)`
- `SpawnPassengers(int count)`
- `ProcessStop(StationDefinition station)`
- `RemoveAlightingPassengers()`
- `SpawnBoardingPassengers()`
- `RecalculateCrowdLevel()`
- Object Pool과 최대 NPC 수 관리

### B 분야 완료 기준

NPC가 플레이어를 추적하지 않고 탑승, 위치 선택, 좌석 이용, 하차 흐름을 수행해야 한다. 정차할 때마다 실제 인원이 바뀌고 혼잡도 단계가 갱신돼야 한다.

## 8. C 분야: 지하철 World와 Stage

### C1. 역 Runtime

권장 클래스: `StationRuntimeController`

- `LoadStation(StationDefinition definition)`
- `ConfigurePlatform()`
- `ConfigureTransferRoute()`
- `ConfigureDestinationExit()`
- `SetOpeningSide(DoorSide side)`
- NPC Spawn과 Exit Point 배치

### C2. 열차 Runtime

권장 클래스: `TrainRuntimeController`, `TrainDoorController`

- `LoadTrain(LineDefinition line)`
- `ArriveAtStation(StationId station)`
- `OpenDoors(DoorSide side)`
- `CloseDoors()`
- `Depart()`
- 좌석, 손잡이, 봉, 기대는 위치와 객차 연결

### C3. Route 진행

권장 클래스: `RouteController`, `RouteSegmentController`

- `StartRoute(RouteDefinition route)`
- `AdvanceStop()`
- `GetNextRequiredStation()`
- `ValidateExit(StationId station)`
- `BeginTransfer()`
- `CompleteTransfer()`
- `HandleMissedStop()`

### C4. 노선도

권장 클래스: `RailMapController`

- `LoadRailMap()`
- `HighlightRoute(RouteDefinition route)`
- `SetRouteLocked(bool locked)`
- `ShowDeliveryDestination(DeliveryDefinition delivery)`
- 끊어진 Route Data 검증

### C5. 콘텐츠 제작 순서

1. 가천대역 Hub와 승강장
2. 무환승 목적지역 1개
3. 환승역 1개와 목적지역 1개
4. 공용 중간 승강장 모듈
5. 노선별 열차 변형
6. 일정에 따라 배송 6~8개까지 확장

### C 분야 완료 기준

실제 역마다 전체 Scene을 새로 만들지 않고도 무환승 배송과 환승 배송을 각각 한 번 완주할 수 있어야 한다.

## 9. D 분야: 배송 System과 UI

### D1. 게임 진행과 Save

권장 클래스:

- `RunState`
- `SaveService`
- `RunResetService`

주요 함수:

- `SaveRun(RunState state)`
- `LoadRun()`
- `HasSaveData()`
- `ResetRun()`
- 현금, 배송 진행, 강화, 보험, 현재 배송과 Route 구간 저장

### D2. 배송 선택

권장 클래스: `DeliverySelectionController`, `DeliveryService`

- `LoadAvailableDeliveries()`
- `SelectDelivery(DeliveryId deliveryId)`
- `PreviewRoute(RouteDefinition route)`
- `CanStartDelivery(DeliveryDefinition delivery)`
- `AcceptDelivery(DeliveryDefinition delivery)`
- 출발 교통비 결제

### D3. 배송 진행

권장 클래스: `DeliveryRuntimeController`

- `StartDelivery()`
- `HandlePackageDamage(DamageEvent damage)`
- `HandleMissedStation(StationId station)`
- `CompleteDelivery()`
- `FailDelivery(DeliveryFailureReason reason)`

### D4. 경제와 정산

권장 클래스: `SettlementCalculator`, `EconomyService`

- `Calculate(SettlementInput input)`
- `CalculateCompensation(float itemValue, float durability)`
- `CalculateTransportCost(RouteDefinition route)`
- `ApplyInsurance(SettlementResult result)`
- `ApplySettlement(SettlementResult result)`
- `CheckBankruptcy()`

### D5. 강화와 학교 서비스

권장 클래스: `UpgradeService`, `UniversityService`

- `PurchaseUpgrade(UpgradeDefinition upgrade)`
- `ApplyUpgrade(UpgradeType type, int level)`
- `PurchaseService(UniversityServiceDefinition service)`
- `ConsumeInsurance()`
- `ConsumeTransportSupport()`

### D6. HUD와 Menu

권장 클래스:

- `GameplayHudController`
- `PackageStatusView`
- `StaminaView`
- `BalancePromptView`
- `InteractionPromptView`
- `StationDisplayView`
- `SettlementView`
- `MainMenuController`

주요 함수:

- `SetBoxDurability(float value)`
- `SetCakeDurability(float value)`
- `SetStamina(float value)`
- `ShowBalanceInput(KeyCode input)`
- `ShowInteraction(string localizationKey)`
- `ShowSettlement(SettlementResult result)`

### D 분야 완료 기준

새 게임 시작, 배송 선택, 배송 완료, 정산, 강화 또는 서비스 구매, 저장과 이어하기, 파산 초기화가 한 흐름으로 작동해야 한다.

## 10. E 분야: Integration과 Quality

### E1. 공용 기반

권장 클래스:

- `GameBootstrap`
- `SceneFlowService`
- `GameEventBus`
- `GameConfig`

주요 함수:

- `GameBootstrap.Initialize()`
- `SceneFlowService.LoadSceneAsync(SceneId sceneId)`
- `SceneFlowService.LoadStationAsync(StationId stationId)`
- `SceneFlowService.LoadTrainAsync(LineId lineId)`
- `GameEventBus.Publish<TEvent>(TEvent gameEvent)`

### E2. Data 검증과 개발 도구

- `DeliveryDataValidator.Validate()`
- `RouteDataValidator.ValidateContinuity()`
- `PassengerDataValidator.Validate()`
- `EconomyDataValidator.Validate()`
- Prototype 상태를 즉시 만드는 Debug Menu

### E3. Test

- `PackageDamageTests`: 상자 보호와 케이크 피해 전달
- `DeliveryResultTests`: 20% 실패 기준
- `SettlementCalculatorTests`: 정산 공식
- `BankruptcyTests`: 전체 진행 초기화
- `RouteControllerTests`: 역과 환승 진행
- `PassengerStateMachineTests`: 승객 상태 전환
- 전체 배송 1회를 확인하는 Play Mode Smoke Test

### E4. 성능

- NPC Update Budget
- Passenger와 소지품 Object Pool
- Physics Layer Matrix
- Collider 수와 Fixed Update 비용 확인
- 극심한 혼잡 상태 Profiler 측정

### E5. Build

- Windows Build Profile
- Build Scene 목록
- Save Data Version
- 깨끗한 Clone 상태에서 Build 확인
- 입력, 해상도와 Audio 최종 점검

## 11. 마일스톤

### 1단계: 핵심 Prototype

- `Prototype_Core.unity`
- Player 이동과 방향 전환
- 기본 운반과 머리 위 운반
- 상자와 케이크 피해
- 단순 이동 NPC
- 균형잡기 1종

목표: 혼잡한 객차에서 케이크를 보호하는 행동이 이해하기 쉽고 재미있는지 확인한다.

### 2단계: 무환승 배송 완성

- Main Menu와 Gachon Hub
- 배송 1개 선택
- 출발역, 열차, 목적지역
- 정산과 자동 복귀

### 3단계: 승객과 혼잡도

- 공통 승객 흐름
- 좌석과 위치 선택
- 승하차
- 우선 승객 유형 5개
- 혼잡도 4단계

### 4단계: 환승과 게임 진행

- 환승역과 두 번째 열차 구간
- 능력치 강화
- 학교 서비스
- 저장, 이어하기와 파산

### 5단계: 콘텐츠 확장과 마무리

- 일정에 따라 배송 6~8개
- 역, 노선, 승객과 행동 추가
- 그래픽, Audio, UI, 밸런싱, 최적화와 Build QA

## 12. 권장 역할

### 역할 1: Gameplay와 Physics

주 담당: A 분야

보조 작업: 열차 상호작용, Gameplay HUD, 파손 Test, Integration

### 역할 2: 승객 AI와 Simulation

주 담당: B 분야

보조 작업: Crowd Data, 역 Spawn Point, AI Debug Tool, 성능

### 역할 3: World와 Transit

주 담당: C 분야

보조 작업: 열차 상호작용, Route Data, Scene 전환, 콘텐츠 조립

### 역할 4: 배송 System과 UI

주 담당: D 분야

보조 작업: Save Data, 경제 Test, Delivery Data, Integration

E 분야는 공동 작업이다. 한 명은 전체 Integration, 브랜치 검토, 마일스톤 Build와 작업 의존성을 관리한다.

## 13. 첫 작업 분배

1. `PlayerMovementController`와 `CarryStateMachine`
2. `PackageDamageController`와 내구도 Debug Tool
3. `PassengerAgent` 임시 이동과 `CrowdManager` Spawn Test
4. `Prototype_Core.unity`, 임시 열차 구조, HUD와 Integration

네 작업을 `Prototype_Core.unity`에 합치고 플레이테스트한 뒤 Physics와 NPC 가정을 수정한다.

## 14. 주요 위험

| 위험 | 대응 |
| --- | --- |
| 압박 Physics가 불안정함 | 단순 Overlap과 간격 계산, Debug Gizmo 사용 |
| NPC가 서로 막히거나 성능이 낮음 | 회피 한계, Object Pool, Update Budget과 인원 제한 |
| Scene과 Prefab 충돌 | 임시 소유자 지정, 큰 공용 Scene보다 Prefab 사용 |
| 역과 승객 콘텐츠가 지나치게 많음 | 공용 시스템을 먼저 완성하고 콘텐츠 수만 줄임 |
| 충돌과 균형잡기가 불공정함 | 사전 행동 표시, Test Profile과 플레이테스트로 조정 |
| 경제 때문에 진행 불가능 상태가 발생함 | 필수 교통비, 배상 한도, 보험과 수수료를 Test Data로 검증 |
