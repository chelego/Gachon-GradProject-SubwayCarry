# 공통 Interface v1

## 기준

- Namespace: `SubwayCarry.Core.Contracts`
- 위치: `SubwayCarry/Assets/_Project/Scripts/Core/Contracts`
- 계약 파일: `InteractionContracts.cs`, `GameplayStateContracts.cs`, `CrowdContracts.cs`, `TransitContracts.cs`, `DeliveryContracts.cs`
- C# 선언이 문서보다 우선한다.
- Snapshot은 외부에서 읽는 상태값이다.
- Provider는 현재 Snapshot과 변경 Event를 제공한다.
- Service는 상태 조회 외에 기능 실행 요청을 받는다.
- 기능 구현체는 다른 역할의 구체 Component를 직접 참조하지 않고 계약을 통해 연결한다.

현재 계약 선언은 Unity에서 컴파일된다. Provider와 Service의 Runtime 구현체는 아직 `develop`에 연결되지 않았다. 현재 확인된 실제 사용은 `StationData`가 공통 `DoorOpeningSide`를 사용하는 부분이다. 계약 선언과 기능 구현 완료를 구분한다.

## 연결 기준

```text
Gameplay / AI / Transit / Delivery
-> 상태를 소유하고 Snapshot 생성
-> Provider Event 발생
-> UI와 다른 기능이 Snapshot 읽기

UI / 외부 기능
-> Service 또는 Receiver에 요청
-> 소유 기능이 결과 계산
-> 변경된 Snapshot과 Event 전달
```

- 상태값은 값을 실제로 소유한 기능만 변경한다.
- UI는 게임 상태를 직접 수정하지 않는다.
- Event는 값이 실제로 바뀐 시점에 한 번 발생시킨다.
- 구독한 Event는 Component 비활성화 또는 제거 시 해제한다.
- 동일한 의미의 enum, Snapshot과 Interface를 기능 폴더에 다시 만들지 않는다.
- `FindObjectOfType`나 Scene 이름으로 다른 역할의 구현체를 고정하지 않는다.
- 아직 구현체가 없는 계약은 필요한 기능을 만들 때 연결하고 Scene 또는 Prefab에서 확인한다.

## 계약 목록

| 파일 | 범위 | 상태 생산자 | 주요 소비자 |
| --- | --- | --- | --- |
| `InteractionContracts.cs` | 좌석, 손잡이, 벽, 문, 역 시설 상호작용 | 상호작용 대상 | 플레이어 상호작용 감지기, UI |
| `GameplayStateContracts.cs` | 운반 자세, 스테미너, 균형, 충돌과 내구도 | 플레이어와 운반물 | UI, 승객 충돌, 열차, 배송 정산 |
| `CrowdContracts.cs` | 현재 승객 수와 혼잡 단계 | 승객 AI | Stage, UI, 난이도 시스템 |
| `TransitContracts.cs` | 열차 움직임, 문, 현재 역과 노선 진행 | 열차와 역 시스템 | 플레이어, 승객 AI, UI, 배송 |
| `DeliveryContracts.cs` | 배송 진행, 현금과 정산 | 배송과 경제 시스템 | UI, 저장, Stage |

## 상호작용

파일: `InteractionContracts.cs`

### `InteractionKind`

- `Seat`
- `LeanSurface`
- `Support`
- `TrainDoor`
- `StationFacility`
- `Vendor`
- `Staff`

### `InteractionContext`

| 값 | 내용 |
| --- | --- |
| `Interactor` | 상호작용을 요청한 GameObject |
| `WorldPosition` | 요청 시점의 위치 |
| `FacingDirection` | 요청자가 바라보는 방향 |

### `InteractionResult`

`InteractionResultCode`를 포함한다.

- `Succeeded`
- `Unavailable`
- `Occupied`
- `Blocked`
- `InvalidState`

### `IInteractable`

```csharp
InteractionKind Kind { get; }
Transform InteractionAnchor { get; }
bool CanInteract(in InteractionContext context);
InteractionResult TryInteract(in InteractionContext context);
```

좌석, 기대는 벽, 손잡이, 문과 역 시설은 같은 입력 경로를 사용한다. 감지 단계에서는 `CanInteract`를 확인하고 실제 입력이 들어온 시점에 `TryInteract`를 호출한다. UI 표시 위치는 `InteractionAnchor`를 사용한다.

## 운반 자세

파일: `GameplayStateContracts.cs`

### `CarryPosture`

- `Standing`
- `Leaning`
- `Sitting`
- `HoldingSupport`
- `OverheadCarry`
- `Fallen`

### `PlayerCarryStateSnapshot`

| 값 | 내용 |
| --- | --- |
| `Posture` | 현재 운반 자세 |
| `IsTransitioning` | 자세 전환 진행 여부 |
| `CanMove` | 현재 이동 가능 여부 |

### `IPlayerCarryStateProvider`

```csharp
PlayerCarryStateSnapshot CurrentCarryState { get; }
event Action<PlayerCarryStateSnapshot> CarryStateChanged;
```

플레이어 기능이 자세와 전환 시간을 소유한다. 승객 AI, 균형잡기와 UI는 플레이어 구현 클래스 대신 Snapshot을 읽는다.

## 스테미너

### `StaminaStateSnapshot`

| 값 | 내용 |
| --- | --- |
| `Current` | 현재 스테미너 |
| `Maximum` | 최대 스테미너 |
| `RecoveryLocked` | 소진 후 회복 불가 시간 여부 |
| `Ratio` | `Current / Maximum`의 0~1 값 |

### `IStaminaStateProvider`

```csharp
StaminaStateSnapshot CurrentStaminaState { get; }
event Action<StaminaStateSnapshot> StaminaStateChanged;
```

머리 위 운반이 스테미너를 소비한다. 소비 중단, 회복 잠금과 회복 시작은 플레이어 기능이 처리하고 HUD는 Snapshot만 표시한다.

## 균형잡기

### `BalanceInputDirection`

- `None`
- `Up`
- `Left`
- `Down`
- `Right`

### `BalanceStateSnapshot`

| 값 | 내용 |
| --- | --- |
| `SequenceId` | 같은 균형 이벤트를 구분하는 번호 |
| `IsActive` | 균형잡기 진행 여부 |
| `RequiredInput` | 현재 요구 방향 |
| `RemainingSeconds` | 입력 제한시간 |
| `HasFailed` | 실패 확정 여부 |

### `IBalanceStateProvider`

```csharp
BalanceStateSnapshot CurrentBalanceState { get; }
event Action<BalanceStateSnapshot> BalanceStateChanged;
```

열차 움직임은 `ITrainMotionProvider`로 전달하고 균형 시스템이 요구 입력을 결정한다. UI는 `W!`, `A!`, `S!`, `D!` 표시와 남은 시간을 갱신한다. 실패 시 넘어짐과 운반물 피해는 Gameplay에서 처리한다.

## 케이크 충돌과 내구도

### `PackageImpactData`

| 값 | 내용 |
| --- | --- |
| `Source` | 충돌이나 압박을 발생시킨 GameObject |
| `ContactPoint` | 접촉 위치 |
| `RelativeSpeed` | 상대 충돌 속도 |
| `CompressionRatio` | 물체 사이 압박 정도 0~1 |

### `IPackageImpactReceiver`

```csharp
void ApplyImpact(in PackageImpactData impact);
```

### `IPackageImpactSource`

```csharp
Vector2 ImpactVelocity { get; }
```

운반물에 직접 충돌 피해를 줄 수 있는 승객이 이 계약을 구현한다. 운반물 충돌 감지기는 이 계약으로 승객 여부와 이동 속도를 읽으며, 벽, 문, 좌석, 바닥과 일반 사물은 직접 충돌 피해원으로 취급하지 않는다.

승객 충돌 또는 승객이 관여한 압박을 감지하면 `PackageImpactData`를 전달한다. 균형 시스템도 실패가 확정된 시점에 한 번의 `PackageImpactData`를 전달한다. 운반물 기능이 상자 보호량과 케이크 피해를 계산한다.

### `PackageDurabilitySnapshot`

| 값 | 내용 |
| --- | --- |
| `BoxDurability` | 케이크 상자 내구도 0~100 |
| `CakeDurability` | 케이크 내구도 0~100 |
| `DeliveryFailed` | 배송 실패 확정 여부 |

### `IPackageDurabilityProvider`

```csharp
PackageDurabilitySnapshot CurrentDurability { get; }
event Action<PackageDurabilitySnapshot> DurabilityChanged;
```

HUD와 배송 정산은 Collider나 피해 계산 코드를 직접 읽지 않고 내구도 Snapshot을 사용한다.

### `IPackageDurabilityResetter`

```csharp
void ResetToFull();
```

배송 수락을 소유한 시스템은 새 운반물을 지급하는 시점에 이 계약으로 내구도를 초기화한다. 이후 출발 교통비 결제가 실패하더라도 같은 운반물의 내구도를 다시 초기화하지 않는다. HUD와 정산 코드는 초기화를 호출하지 않고 `IPackageDurabilityProvider`만 읽는다.

### `IPackageAvailabilityController`

```csharp
bool HasPackage { get; }
void SetPackageAvailable(bool available);
```

배송 확인 화면만 연 단계에는 운반물을 표시하거나 충돌 피해 대상으로 취급하지 않는다. 배송 수락 시 운반물을 활성화하고, 배송 정산 또는 허브 복귀 흐름이 끝날 때 비활성화한다. 플레이어 애니메이션은 `HasPackage`에 따라 빈손 또는 운반 자세를 선택하며, 케이크 상자는 별도 SpriteRenderer에서 같은 방향·프레임으로 재생한다.

```text
승객 충돌, 승객이 관여한 압박 또는 균형 실패
-> PackageImpactData
-> IPackageImpactReceiver.ApplyImpact
-> 상자와 케이크 피해 계산
-> PackageDurabilitySnapshot
-> HUD와 배송 결과
```

## 혼잡도

파일: `CrowdContracts.cs`

### `CrowdLevel`

- `Relaxed`
- `Normal`
- `Crowded`
- `Severe`

### `CrowdStateSnapshot`

| 값 | 내용 |
| --- | --- |
| `PassengerCount` | 현재 유효 승객 수 |
| `PracticalCapacity` | Gameplay 기준 수용 인원 |
| `Level` | 계산된 혼잡 단계 |

### `ICrowdStateProvider`

```csharp
CrowdStateSnapshot CurrentCrowdState { get; }
event Action<CrowdStateSnapshot> CrowdStateChanged;
```

혼잡도는 승객 AI가 소유한다. UI와 Stage는 승객 GameObject를 직접 검색하거나 세지 않는다. 현재 60명 Prototype 수치는 스트레스 테스트 값이며 실제 Stage 인원은 `StationData`와 추후 Crowd 설정에서 결정한다.

## 열차 움직임

파일: `TransitContracts.cs`

### `TrainMotionPhase`

- `Stopped`
- `Departing`
- `Cruising`
- `SpeedChanging`
- `Arriving`
- `EmergencyBraking`

### `TrainMotionSnapshot`

| 값 | 내용 |
| --- | --- |
| `SequenceId` | 움직임 이벤트 구분 번호 |
| `Phase` | 현재 운행 상태 |
| `InertiaDirection` | 플레이어에게 적용되는 관성 방향 |
| `Intensity` | 관성 강도 |

### `ITrainMotionProvider`

```csharp
TrainMotionSnapshot CurrentTrainMotion { get; }
event Action<TrainMotionSnapshot> TrainMotionChanged;
```

열차 시스템이 출발, 정차, 속도 조절과 급정거를 생산한다. 균형잡기, 승객 반응과 연출은 같은 Snapshot을 사용한다.

## 열차 문

### `DoorOpeningSide`

- `Left`
- `Right`
- `Both`

`StationData.StationPlatformData`도 이 enum을 사용한다.

### `TrainDoorState`

- `Closed`
- `Opening`
- `Open`
- `Closing`

### `TrainDoorSnapshot`

| 값 | 내용 |
| --- | --- |
| `StationId` | 문 상태가 적용되는 역 ID |
| `OpeningSide` | 열리는 방향 |
| `State` | 현재 문 상태 |

### `ITrainDoorStateProvider`

```csharp
TrainDoorSnapshot CurrentDoorState { get; }
event Action<TrainDoorSnapshot> TrainDoorStateChanged;
```

승객 AI는 문 Controller를 임의로 열지 않고 문 상태를 읽어 탑승과 하차를 시작한다. 플레이어 하차 제한과 UI도 같은 상태를 사용한다.

## 노선 진행

### `TransitProgressSnapshot`

| 값 | 내용 |
| --- | --- |
| `CurrentStationId` | 현재 역 ID |
| `NextStationId` | 다음 역 ID |
| `RouteIndex` | 현재 Route 순번 |
| `RouteStopCount` | 전체 정차 수 |
| `IsTransferStop` | 필수 환승역 여부 |
| `IsDestination` | 목적지역 여부 |
| `OpeningSide` | 현재 역 문 방향 |

### `ITransitProgressProvider`

```csharp
TransitProgressSnapshot CurrentTransitProgress { get; }
event Action<TransitProgressSnapshot> TransitProgressChanged;
```

배송 시스템은 역 이름이나 Scene 이름을 비교하지 않고 Route 진행 Snapshot으로 환승과 도착을 판단한다.

## 배송 진행

파일: `DeliveryContracts.cs`

### `DeliveryPhase`

- `None`
- `Selected`
- `TravellingToDeparture`
- `InTransit`
- `Transferring`
- `Arrived`
- `Completed`
- `Failed`

### `DeliveryFailureReason`

- `None`
- `PackageDestroyed`
- `MissedRequiredStop`
- `Bankrupt`

### `DeliveryStateSnapshot`

| 값 | 내용 |
| --- | --- |
| `DeliveryId` | 배송 Data 식별자 |
| `DestinationStationId` | 목적지역 ID |
| `Phase` | 현재 배송 단계 |
| `FailureReason` | 실패 사유 |

### `IDeliveryStateProvider`

```csharp
DeliveryStateSnapshot CurrentDeliveryState { get; }
event Action<DeliveryStateSnapshot> DeliveryStateChanged;
```

### `IDeliveryService`

```csharp
bool TryStartDelivery(string deliveryId);
```

배송 선택 UI는 배송 수락 시 케이크 상자를 먼저 지급하고 운반 애니메이션으로 전환한다. 개찰구 상호작용은 `TryStartDelivery`로 출발을 요청하며, 배송 가능 여부, 교통비 지불과 현재 진행 상태는 배송 기능이 결정한다. 반환값이 `false`이면 개찰구 통과만 중단하고 이미 수락한 배송과 케이크 상자는 유지한다.

## 경제와 정산

### `EconomyStateSnapshot`

| 값 | 내용 |
| --- | --- |
| `CurrentCash` | 현재 현금 |

### `IEconomyStateProvider`

```csharp
EconomyStateSnapshot CurrentEconomyState { get; }
event Action<EconomyStateSnapshot> EconomyStateChanged;
```

### `DeliverySettlementSnapshot`

| 값 | 내용 |
| --- | --- |
| `DeliveryFee` | 배달 수수료 |
| `Compensation` | 물건 손상 배상금 |
| `OutboundFare` | 출발 교통비 |
| `ReturnFare` | 복귀 교통비 |
| `NetIncome` | 최종 손익 |

### `IDeliverySettlementProvider`

```csharp
DeliverySettlementSnapshot LastSettlement { get; }
event Action<DeliverySettlementSnapshot> DeliverySettled;
```

정산 UI는 개별 값을 다시 계산하지 않고 `DeliverySettlementSnapshot`을 그대로 표시한다. 파산 판정은 정산 반영 뒤 현재 현금을 기준으로 배송과 경제 기능에서 처리한다.

## 역할별 생산 데이터

| 범위 | 생산할 계약 | 읽을 계약 |
| --- | --- | --- |
| 플레이어·운반 | `IPlayerCarryStateProvider`, `IStaminaStateProvider`, `IBalanceStateProvider`, `IPackageDurabilityProvider` | `ITrainMotionProvider`, `IInteractable` |
| 승객 AI | `ICrowdStateProvider` | `ITrainDoorStateProvider`, `ITransitProgressProvider`, 플레이어 위치와 Collider |
| 역·열차·맵 | `ITrainMotionProvider`, `ITrainDoorStateProvider`, `ITransitProgressProvider` | `StationData` |
| 배송·UI·경제 | `IDeliveryService`, `IEconomyStateProvider`, `IDeliverySettlementProvider` | `IPackageDurabilityProvider`, `ITransitProgressProvider`, `ICrowdStateProvider` |

## 현재 구현 상태

| 항목 | 상태 |
| --- | --- |
| 공통 enum, Snapshot, Interface 선언 | 구현됨, Unity 컴파일 확인 |
| `StationData`와 `DoorOpeningSide` 연결 | 구현됨 |
| 각 Provider와 Service Runtime 구현체 | 아직 연결되지 않음 |
| UI Event 구독 | 아직 연결되지 않음 |
| 승객 Prototype의 `ICrowdStateProvider` 연결 | 아직 연결되지 않음 |
| 열차 Prototype의 Transit Provider 연결 | 아직 연결되지 않음 |
| 플레이어와 운반물 Provider 연결 | 아직 연결되지 않음 |
| 배송과 경제 Service 연결 | 아직 연결되지 않음 |

Prototype의 구체 코드가 동작하더라도 공통 Interface에 연결되기 전에는 다른 역할에서 사용할 수 있는 통합 기능으로 보지 않는다.

## 계약 변경 시 같이 확인할 것

- `Assets/_Project/Scripts/Core/Contracts`의 선언
- 해당 Interface 구현체
- Snapshot 또는 Event 소비 코드
- `StationData`를 포함한 공용 Data
- Scene과 Prefab의 Component 연결
- 관련 `.meta`
- Unity Console과 실제 Prototype 실행 결과

계약 이름이나 필드를 바꾸면 일부 파일만 맞추지 않고 사용처를 한 작업에서 함께 갱신한다. 구현이 아직 없는 계약은 문서의 상태를 `아직 연결되지 않음`으로 유지한다.
