using System;
using SubwayCarry.AI.V2;
using SubwayCarry.Core.Contracts;
using SubwayCarry.Delivery;
using SubwayCarry.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Integration owns the journey, not the player, crowd simulation or money implementation.
    public sealed class SliceJourneyController : MonoBehaviour, IDeliveryService,
        IDeliverySettlementProvider, ITrainMotionProvider, ITrainDoorStateProvider, ITransitProgressProvider
    {
        public SliceDeliveryCatalog Catalog { get; private set; }
        public SliceGameController World { get; private set; }
        public EconomyService Economy { get; private set; }
        public DeliveryPhase Phase { get; private set; }
        public DeliveryFailureReason Failure { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public int UnlockedCount { get; private set; } = 1;
        public int StopIndex { get; private set; }
        public int Insurance { get; private set; }
        public int FareSupport { get; private set; }
        public int StaminaLevel { get; private set; }
        public int BalanceLevel { get; private set; }
        public int AgilityLevel { get; private set; }
        public bool MapOpen { get; private set; }
        public bool Paused { get; private set; }
        public bool PresentationPaused { get; private set; }
        public bool InputBlocked => SlicePresentationRules.BlocksSimulation(MapOpen, Paused, PresentationPaused, IsResult);
        public bool IsResult => Phase == DeliveryPhase.Completed || Phase == DeliveryPhase.Failed;
        public bool AllowPassengerArrival => World.CurrentMapIndex != 1 || CurrentDoorState.State == TrainDoorState.Open;
        SliceDeliveryOrder resumedOrder;
        public SliceDeliveryOrder Order => resumedOrder ?? (SelectedIndex >= 0 ? Catalog.orders[SelectedIndex] : null);
        public SliceRouteStop Stop => Order != null ? Order.stops[StopIndex] : null;
        public string Notice { get; private set; } = "배달 앱에서 첫 배송을 선택하세요.";
        public string ServiceReceipt { get; private set; } = "";
        public float SecondsRemaining => Mathf.Max(0, timer);
        public bool OnTrain => World.CurrentMapIndex == 1;
        public int ApproachingStopIndex => StopIndex + (step == ServiceStep.Travelling ? 1 : 0);
        public bool IsRequiredStop => Order != null && (StopIndex == Order.stops.Length - 1 || (Stop.transfer && !transferred));
        public DeliveryStateSnapshot CurrentDeliveryState => new DeliveryStateSnapshot(Order?.id ?? "", Order == null ? "" : Order.stops[Order.stops.Length - 1].id, Phase, Failure);
        public DeliverySettlementSnapshot LastSettlement { get; private set; }
        public TrainMotionSnapshot CurrentTrainMotion { get; private set; }
        public TrainDoorSnapshot CurrentDoorState { get; private set; }
        public TransitProgressSnapshot CurrentTransitProgress { get; private set; }
        public event Action<DeliveryStateSnapshot> DeliveryStateChanged;
        public event Action<DeliverySettlementSnapshot> DeliverySettled;
        public event Action<TrainMotionSnapshot> TrainMotionChanged;
        public event Action<TrainDoorSnapshot> TrainDoorStateChanged;
        public event Action<TransitProgressSnapshot> TransitProgressChanged;

        enum ServiceStep { Waiting, ArrivalDelay, Opening, Open, Closing, DepartureDelay, Travelling }
        ServiceStep step;
        float timer, travelled;
        int sequence, paidFare;
        bool transferred, middleEvent, initialized;
        bool restoring;
        bool gateOpen, departureGatePassed, exitGatePassed;
        float gateClosesAt;
        float departureHold;
        public bool ScholarshipAwarded { get; private set; }
        public bool GateOpen => gateOpen;
        PlayerPackageCarrier carrier;
        SliceDoorPresentation doors;

        public void Initialize(SliceGameController world, SliceDeliveryCatalog catalog)
        {
            World = world; Catalog = catalog;
            if (!catalog.IsValid(out string error)) { Debug.LogError(error, catalog); enabled = false; return; }
            Economy = gameObject.AddComponent<EconomyService>();
            carrier = world.Player.GetComponent<PlayerPackageCarrier>();
            carrier.SetPackageAvailable(false);
            world.Player.GetComponent<PlayerController>().PrototypePostureInputEnabled = false;
            world.Player.GetComponent<PlayerSpriteAnimator>().MouseRelativeLocomotion = true;
            world.Player.GetComponent<SlicePlayerBoundary>().journey = this;
            foreach (var map in world.maps)
            {
                var gate = map.GetComponent<SliceStationGate>(); if (gate != null) gate.Bind(this);
                map.gameObject.AddComponent<SliceInteractionMarkers>().Initialize(map, this);
            }
            // Replace the prototype's key-triggered train motion with this real service clock.
            var debugMotion = world.Player.GetComponent<DebugTrainMotionProvider>();
            if (debugMotion != null) debugMotion.enabled = false;
            world.Balance.Configure(this, world.Posture, world.Package);
            world.Package.DurabilityChanged += OnDurability;
            doors = gameObject.AddComponent<SliceDoorPresentation>();
            doors.Initialize(world, this);
            step = ServiceStep.Waiting; timer = catalog.travelSeconds;
            initialized = true;
            RestoreProgress();
            gameObject.AddComponent<SliceDamageFeedback>().Initialize(this);
            MapOpen = Phase == DeliveryPhase.None;
            gameObject.AddComponent<SlicePixelHud>().Initialize(this);
            SetControl();
        }

        public void ToggleMap()
        {
            if (IsResult || Paused || PresentationPaused || World.IsChangingMap) return;
            MapOpen = !MapOpen; SetControl();
        }
        public void TogglePause() { Paused = !Paused; ApplyPause(); }
        public void SetPresentationPaused(bool value) { PresentationPaused = value; ApplyPause(); }
        void ApplyPause() { Time.timeScale = Paused || PresentationPaused ? 0 : 1; SetControl(); }
        void SetControl()
        {
            Time.timeScale = InputBlocked ? 0 : 1;
            World.SetInputBlocked(InputBlocked);
            World.Balance.InputSuspended = InputBlocked;
        }
        public void Select(int index)
        {
            if (Phase != DeliveryPhase.None || index < 0 || index >= UnlockedCount || index >= Catalog.orders.Length) return;
            resumedOrder = null;
            SelectedIndex = index; StopIndex = 0; transferred = false; Failure = DeliveryFailureReason.None;
            World.ResetPassengerJourney();
            World.Package.ResetToFull(); carrier.SetPackageAvailable(true);
            World.maximumPassengers = Order.passengers;
            paidFare = 0; ServiceReceipt = ""; MapOpen = false; departureGatePassed = exitGatePassed = false;
            SetPhase(DeliveryPhase.Selected);
            Notice = "배송 수락 완료. 입구의 교통카드 단말기에서 E를 누르세요.";
            SetControl();
            SaveProgress();
        }
        public bool TryStartDelivery(string id)
        {
            if (Phase != DeliveryPhase.Selected || Order == null || Order.id != id || !AtStationFacility()) return false;
            if (!SpendFare(Order.outboundFare, out paidFare))
            { Notice = "교통비 부족. 배송과 물품은 유지됩니다. 학교 지원을 확인하세요."; return false; }
            step = ServiceStep.Waiting; timer = Catalog.travelSeconds;
            SetPhase(DeliveryPhase.TravellingToDeparture); PublishStop();
            OpenGate();
            Notice = "플랫폼에서 열차를 기다리세요. 문이 열리면 가까이 가서 E로 탑승합니다.";
            SaveProgress(); return true;
        }
        bool AtStationFacility() => !OnTrain && (World.PlayerPosition - (World.CurrentMap.hasFareGate ? World.CurrentMap.fareGate : World.CurrentMap.entry)).sqrMagnitude < 4;
        public bool TryGetInteractionHint(out Vector2 position, out string caption, out string key)
        {
            position = World.PlayerPosition; caption = ""; key = "E";
            if (InputBlocked || World.IsChangingMap || World.Balance.IsActive) return false;
            var map = World.CurrentMap;
            if (map.hasFareGate && AtStationFacility()) { position = map.fareGate; caption = GateOpen ? "지나가세요" : "교통카드 찍기"; return true; }
            float distance = 2.56f; bool found = false;
            foreach (var portal in map.portals)
            {
                if (OnTrain && portal.side != DoorOpeningSide.Right) continue;
                float d = (portal.position - World.PlayerPosition).sqrMagnitude;
                if (d >= distance) continue;
                distance = d; position = portal.position;
                caption = portal.stationConnection ? (World.CurrentMapIndex >= 3 ? "승강장으로" : "대합실로") : World.DoorsOpen ? (OnTrain ? "내리기" : "탑승하기") : "열차 기다리기";
                found = true;
            }
            if (found) return true;
            distance = 1.5625f;
            foreach (var spot in map.interests)
            {
                float d = (spot.position - World.PlayerPosition).sqrMagnitude; if (d >= distance) continue;
                distance = d; position = spot.position; found = true;
                caption = spot.kind == PassengerAiV2InteriorSpotKind.Seat ? "앉기" : spot.kind == PassengerAiV2InteriorSpotKind.Lean ? "기대기" : "잡기";
                key = spot.kind == PassengerAiV2InteriorSpotKind.Seat ? "3" : spot.kind == PassengerAiV2InteriorSpotKind.Lean ? "2" : "4";
            }
            return found;
        }
        void OpenGate() { gateOpen = true; gateClosesAt = Time.time + 4; World.Posture.TryTransition(CarryPosture.Standing); }
        public bool AllowsGateCrossing(Vector2 from, Vector2 to)
        {
            if (!World.CurrentMap.hasFareGate) return true;
            float a = from.x * .5f + from.y - 6, b = to.x * .5f + to.y - 6;
            if (a * b > 0 || Mathf.Approximately(a, b)) return true;
            float across = to.y - to.x * .5f;
            return gateOpen && across > 1.8f && across < 4.2f;
        }

        void Update()
        {
            if (!initialized) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
                if (!Paused && !PresentationPaused && keyboard.tabKey.wasPressedThisFrame) ToggleMap();
                if (!InputBlocked && !World.IsChangingMap && !World.Balance.IsActive)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) RequestPosture(CarryPosture.Standing);
                    if (keyboard.digit2Key.wasPressedThisFrame) RequestPosture(CarryPosture.Leaning);
                    if (keyboard.digit3Key.wasPressedThisFrame) RequestPosture(CarryPosture.Sitting);
                    if (keyboard.digit4Key.wasPressedThisFrame) RequestPosture(CarryPosture.HoldingSupport);
                    if (keyboard.digit5Key.wasPressedThisFrame && carrier.HasPackage) RequestPosture(CarryPosture.OverheadCarry);
                }
                if (!InputBlocked && !World.IsChangingMap && keyboard.eKey.wasPressedThisFrame && AtStationFacility())
                {
                    if (Phase == DeliveryPhase.Selected) TryStartDelivery(Order.id);
                    else if (World.CurrentMap.hasFareGate && (Phase == DeliveryPhase.Arrived || Phase == DeliveryPhase.TravellingToDeparture)) OpenGate();
                }
            }
            if (gateOpen && !Paused)
            {
                float along = World.PlayerPosition.x * .5f + World.PlayerPosition.y;
                if (World.CurrentMapIndex == 3 && along > 6.6f) { departureGatePassed = true; gateOpen = false; }
                if (World.CurrentMapIndex == 4 && along < 5.4f) { exitGatePassed = true; gateOpen = false; }
                if (Time.time >= gateClosesAt) gateOpen = false;
            }
            if (!Paused && Phase == DeliveryPhase.Arrived && World.CurrentMapIndex == 4 && exitGatePassed &&
                (World.PlayerPosition - World.CurrentMap.streetExit).sqrMagnitude < 2.25f) Settle(DeliveryFailureReason.None);
            if (InputBlocked || World.IsChangingMap || Phase == DeliveryPhase.None || Phase == DeliveryPhase.Selected ||
                Phase == DeliveryPhase.Arrived || Phase == DeliveryPhase.Transferring) return;
            timer -= Time.deltaTime;
            if (step == ServiceStep.Travelling)
            {
                travelled += Time.deltaTime;
                if (!middleEvent && travelled >= Catalog.travelSeconds * 0.48f)
                {
                    middleEvent = true;
                    bool emergency = Order.stars >= 3 && StopIndex % 2 == 1;
                    EmitMotion(emergency ? TrainMotionPhase.EmergencyBraking : TrainMotionPhase.SpeedChanging, emergency ? Vector2.up : Vector2.right, emergency ? 1.2f : .7f);
                }
                if (timer <= 4 && CurrentTrainMotion.Phase != TrainMotionPhase.Arriving)
                {
                    EmitMotion(TrainMotionPhase.Arriving, Vector2.up, 0.8f);
                    Notice = "이번 역: " + Order.stops[StopIndex + 1].label + " / 오른쪽 문이 열립니다.";
                    World.NotifyService(PassengerAiV2ServicePhase.Approaching, true);
                }
            }
            if (timer <= 0) AdvanceService();
        }

        void AdvanceService()
        {
            switch (step)
            {
                case ServiceStep.Waiting:
                    step = ServiceStep.ArrivalDelay; timer = Catalog.arrivalDelaySeconds;
                    World.NotifyService(PassengerAiV2ServicePhase.Approaching, false); break;
                case ServiceStep.Travelling:
                    StopIndex++; transferred = false; PublishStop();
                    EmitMotion(TrainMotionPhase.Stopped, Vector2.zero, 0);
                    step = ServiceStep.ArrivalDelay; timer = Catalog.arrivalDelaySeconds; break;
                case ServiceStep.ArrivalDelay:
                    step = ServiceStep.Opening; timer = Catalog.doorAnimationSeconds; SetDoor(TrainDoorState.Opening); break;
                case ServiceStep.Opening:
                    step = ServiceStep.Open; timer = Catalog.doorsOpenSeconds; departureHold = 0; SetDoor(TrainDoorState.Open);
                    World.NotifyService(PassengerAiV2ServicePhase.DoorsOpen, OnTrain && StopIndex > 0);
                    Notice = IsRequiredStop ? Stop.label + " 도착! 문으로 이동해 E로 내리세요." : "승하차 중. 문 가운데를 비워 주세요.";
                    break;
                case ServiceStep.Open:
                    if (World.HasDoorPassage && departureHold < 3) { timer = .25f; departureHold += .25f; break; }
                    step = ServiceStep.Closing; timer = Catalog.doorAnimationSeconds; SetDoor(TrainDoorState.Closing); break;
                case ServiceStep.Closing:
                    step = ServiceStep.DepartureDelay; timer = Catalog.arrivalDelaySeconds; SetDoor(TrainDoorState.Closed); break;
                case ServiceStep.DepartureDelay:
                    if (OnTrain && IsRequiredStop) { Settle(DeliveryFailureReason.MissedRequiredStop); return; }
                    World.NotifyService(PassengerAiV2ServicePhase.Departed, false);
                    if (OnTrain)
                    {
                        World.ForgetPreviousStationAlighting();
                        step = ServiceStep.Travelling; timer = Catalog.travelSeconds; travelled = 0; middleEvent = false;
                        EmitMotion(TrainMotionPhase.Departing, Vector2.down, 1);
                        Notice = Order.stops[StopIndex + 1].label + " 방면 운행 중";
                    }
                    else { step = ServiceStep.Waiting; timer = Catalog.travelSeconds; Notice = "다음 열차를 기다립니다."; }
                    break;
            }
        }

        public bool TryUseDoor(SlicePortal portal)
        {
            if (InputBlocked || World.IsChangingMap) return false;
            if (!World.Posture.CanMove || World.Balance.IsActive) { Notice = "서 있는 자세로 전환한 뒤 승하차하세요."; return false; }
            if (portal.guidedTraversal && (portal.position - World.PlayerPosition).sqrMagnitude > 1)
            { Notice = "계단 입구로 조금 더 가까이 가세요."; return false; }
            if (portal.stationConnection) return UseStationConnection(portal);
            if (CurrentDoorState.State != TrainDoorState.Open) { Notice = "열차가 정차하고 문이 완전히 열린 뒤 이용하세요."; return false; }
            if (!OnTrain)
            {
                if (Phase != DeliveryPhase.TravellingToDeparture) { Notice = "교통카드 단말기를 먼저 이용하세요."; return false; }
                SetPhase(DeliveryPhase.InTransit);
                // Match the current station's boarding side; station destination is never encoded by left/right door.
                var train = World.maps[1];
                train.EnsureGrid();
                Vector2 arrival = train.portals.Length > 0 ? train.portals[0].position : train.entry;
                World.TravelTo(1, train.Grid.Nearest(arrival + new Vector2(-.5f, .5f)));
                Notice = "탑승했습니다. 좌석이나 지지물을 찾아 케이크를 보호하세요.";
            }
            else
            {
                if (portal.side != DoorOpeningSide.Right) { Notice = "이번 역은 오른쪽 문이 열립니다."; return false; }
                if (!IsRequiredStop) { Notice = "아직 내릴 역이 아니다. 열차 안에서 기다리자."; return false; }
                bool destination = StopIndex == Order.stops.Length - 1;
                SetPhase(destination ? DeliveryPhase.Arrived : DeliveryPhase.Transferring);
                int map = Mathf.Clamp(Stop.platformMap, 0, World.maps.Length - 1);
                if (map == 1) map = 2;
                World.maps[map].displayName = Stop.label + "역";
                Vector2 arrival = World.maps[map].portals.Length > 0 ? World.maps[map].portals[0].position : World.maps[map].entry;
                World.TravelTo(map, arrival);
                Notice = destination ? "출구 단말기로 이동해 E를 누르면 배송이 완료됩니다." : "환승 안내를 따라 단말기로 이동하세요. 이 역에서 능력치를 강화할 수 있습니다.";
            }
            return true;
        }
        bool UseStationConnection(SlicePortal portal)
        {
            int target = portal.targetMap; Vector2 arrival = portal.arrival;
            if (World.CurrentMapIndex == 3)
            {
                if (Phase != DeliveryPhase.TravellingToDeparture || !departureGatePassed) { Notice = "배송 수락 후 교통카드를 찍고 개찰구를 통과하세요."; return false; }
                target = 0; arrival = World.maps[0].entry;
            }
            else if (World.CurrentMapIndex == 2)
            {
                target = Phase == DeliveryPhase.Transferring ? 5 : 4;
                arrival = Phase == DeliveryPhase.Transferring ? World.maps[5].entry : SliceTrainLayout.Project(11, 3);
            }
            else if (World.CurrentMapIndex == 5)
            {
                if (Phase != DeliveryPhase.Transferring) return false;
                transferred = true; step = ServiceStep.Waiting; timer = Catalog.travelSeconds;
                SetDoor(TrainDoorState.Closed); SetPhase(DeliveryPhase.TravellingToDeparture);
                target = 2; arrival = World.maps[2].entry;
                Notice = "환승 완료. " + Order.stops[StopIndex + 1].line + " 승강장에서 열차를 기다리세요.";
            }
            else if (World.CurrentMapIndex == 4) { target = 2; arrival = World.maps[2].entry; }
            gateOpen = false;
            portal.targetMap = target; portal.arrival = arrival;
            World.TravelVia(portal); return true;
        }
        public void MapActivated()
        {
            if (!OnTrain && StopIndex > 0 && Stop != null) World.CurrentMap.displayName = Stop.label + "역";
            doors.Rebind();
            World.NotifyService(CurrentDoorState.State == TrainDoorState.Open ? PassengerAiV2ServicePhase.DoorsOpen : PassengerAiV2ServicePhase.Waiting, OnTrain && StopIndex > 0);
            SetControl();
            SaveProgress();
        }
        void PublishStop()
        {
            CurrentTransitProgress = new TransitProgressSnapshot(Stop.id, StopIndex + 1 < Order.stops.Length ? Order.stops[StopIndex + 1].id : "",
                StopIndex, Order.stops.Length, Stop.transfer, StopIndex == Order.stops.Length - 1, DoorOpeningSide.Right);
            TransitProgressChanged?.Invoke(CurrentTransitProgress);
        }
        void SetDoor(TrainDoorState state)
        {
            CurrentDoorState = new TrainDoorSnapshot(Stop?.id ?? "gachon", DoorOpeningSide.Right, state);
            TrainDoorStateChanged?.Invoke(CurrentDoorState);
        }
        void EmitMotion(TrainMotionPhase phase, Vector2 inertia, float intensity)
        {
            CurrentTrainMotion = new TrainMotionSnapshot(++sequence, phase, inertia, intensity);
            TrainMotionChanged?.Invoke(CurrentTrainMotion);
        }
        void SetPhase(DeliveryPhase phase) { Phase = phase; DeliveryStateChanged?.Invoke(CurrentDeliveryState); }
        void OnDurability(PackageDurabilitySnapshot snapshot)
        {
            if (!restoring && carrier.HasPackage && snapshot.CakeDurability <= 0 && !IsResult && Order != null) Settle(DeliveryFailureReason.PackageDestroyed);
        }
        bool SpendFare(int fare, out int actual)
        {
            actual = 0;
            if (fare <= 0) return true;
            if (FareSupport > 0) { FareSupport--; ServiceReceipt += "교통비 지원 1회 적용\n"; return true; }
            if (!Economy.TrySpend(fare)) return false;
            actual = fare; return true;
        }
        public void BuyService(bool insurance)
        {
            if (World.CurrentMapIndex != 3 || (Phase != DeliveryPhase.None && Phase != DeliveryPhase.Selected)) return;
            int price = insurance ? Catalog.insurancePrice : Catalog.fareSupportPrice;
            if (!Economy.TrySpend(price)) { Notice = "현금이 부족합니다."; return; }
            if (insurance) Insurance++; else FareSupport++;
            Notice = "학교 서비스 구매 완료"; SaveProgress();
        }
        void RequestPosture(CarryPosture posture)
        {
            if (!World.TryUseFacility(posture)) Notice = "가까운 빈 좌석·벽·지지물이 필요합니다. 사용 중인 시설은 이용할 수 없습니다.";
        }
        public int UpgradePrice(int level) => Catalog.upgradeBasePrice * (level + 1);
        public void BuyUpgrade(int stat)
        {
            if (Phase != DeliveryPhase.Transferring || World.CurrentMapIndex != 5 || !AtStationFacility() || stat < 0 || stat > 2) { Notice = "환승 통로의 강화 단말기 가까이 이동하세요."; return; }
            int level = stat == 0 ? StaminaLevel : stat == 1 ? BalanceLevel : AgilityLevel;
            if (level >= Catalog.maximumUpgradeLevel) { Notice = "최대 강화입니다."; return; }
            if (!Economy.TrySpend(UpgradePrice(level))) { Notice = "강화 비용이 부족합니다."; return; }
            if (stat == 0) StaminaLevel++; else if (stat == 1) BalanceLevel++; else AgilityLevel++;
            ApplyUpgrades(); Notice = "능력치 강화가 즉시 적용되었습니다."; SaveProgress();
        }
        void ApplyUpgrades()
        {
            World.Posture.ConfigureStaminaMultiplier(1 + StaminaLevel * Catalog.staminaPerLevel);
            World.Balance.Assistance = BalanceLevel * Catalog.balancePerLevel;
            World.Player.GetComponent<SlicePlayerBoundary>().moveSpeed = 2.2f * (1 + AgilityLevel * Catalog.agilityPerLevel);
        }
        void Settle(DeliveryFailureReason reason)
        {
            if (IsResult || Order == null) return;
            World.Balance.CancelCurrentChallenge();
            var state = World.Package.CurrentDurability;
            var result = SliceDeliveryRules.Evaluate(state.BoxDurability, state.CakeDurability, reason != DeliveryFailureReason.None,
                Order.value, Order.fee, paidFare, Order.returnFare, Insurance > 0, FareSupport > 0);
            bool success = result.Success;
            int fee = result.Fee, compensation = result.Compensation, returnFare = result.ReturnFare;
            if (result.UsedInsurance) { Insurance--; ServiceReceipt += "배송 보험 1회 적용\n"; }
            if (result.UsedFareSupport) { FareSupport--; ServiceReceipt += "복귀 교통비 지원 1회 적용\n"; }
            Economy.ApplyDelta(fee - compensation - returnFare);
            Failure = reason;
            if (!success && reason == DeliveryFailureReason.None) Failure = DeliveryFailureReason.PackageDestroyed;
            if (Economy.CurrentEconomyState.CurrentCash < 0)
            {
                Failure = DeliveryFailureReason.Bankrupt; success = false;
                Economy.ResetToInitial(); UnlockedCount = 1; Insurance = FareSupport = 0;
                StaminaLevel = BalanceLevel = AgilityLevel = 0; ApplyUpgrades();
                ScholarshipAwarded = false;
                PlayerPrefs.DeleteKey(SaveKey);
            }
            else if (success)
            {
                UnlockedCount = Mathf.Min(Catalog.orders.Length, Mathf.Max(UnlockedCount, SelectedIndex + 2));
                if (SelectedIndex == Catalog.orders.Length - 1) ScholarshipAwarded = true;
            }
            LastSettlement = new DeliverySettlementSnapshot(fee, compensation, paidFare, returnFare, fee - compensation - paidFare - returnFare);
            SetPhase(success ? DeliveryPhase.Completed : DeliveryPhase.Failed);
            DeliverySettled?.Invoke(LastSettlement);
            Notice = Failure == DeliveryFailureReason.Bankrupt ? "파산했습니다. 해당 게임의 진행을 초기화했습니다." : success ? "배송 완료! 가천대역으로 복귀합니다." : "배송 실패. 다음 배송을 준비하세요.";
            if (success && ScholarshipAwarded) Notice = "최종 배송 완료! 다음 학기 전액 장학금을 받았습니다. (임시 최종 배송 기준)";
            SetControl(); SaveProgress();
        }
        public void ReturnToHub()
        {
            if (!IsResult) return;
            carrier.SetPackageAvailable(false);
            World.ResetPassengerJourney();
            World.Posture.TryTransition(CarryPosture.Standing);
            SetPhase(DeliveryPhase.None); SelectedIndex = -1; StopIndex = 0; resumedOrder = null;
            SetDoor(TrainDoorState.Closed); World.TravelTo(3, World.maps[3].entry);
            Notice = "가천대역 / 다음 배송을 선택하려면 TAB";
            SaveProgress();
        }

        // Versioned, isolated key: never overwrites the team's other prototype save slots.
        const string SaveKey = "SubwayCarry.ArtSlice.Progress.v1";
        [Serializable] sealed class Progress
        {
            public int version = 3, cash, unlocked, insurance, fareSupport, staminaLevel, balanceLevel, agilityLevel;
            public string orderId;
            public SliceDeliveryOrder orderSnapshot;
            public DeliveryPhase phase;
            public int stop, serviceStep, map, paidFare;
            public bool transferred, middleEvent, departureGatePassed, exitGatePassed, scholarship;
            public float timer, travelled, x, y, box = 100, cake = 100, stamina = 1;
            public DeliveryFailureReason failure;
            public int fee, compensation, returnFare, net;
        }
        public void SaveProgress()
        {
            if (!initialized) return;
            if (World.IsChangingMap || restoring) return;
            var durability = World.Package.CurrentDurability;
            var save = new Progress { cash = Economy.CurrentEconomyState.CurrentCash, unlocked = UnlockedCount, insurance = Insurance, fareSupport = FareSupport,
                staminaLevel = StaminaLevel, balanceLevel = BalanceLevel, agilityLevel = AgilityLevel, orderId = Order?.id ?? "", orderSnapshot = Order, phase = Phase,
                stop = StopIndex, serviceStep = (int)step, map = World.CurrentMapIndex, paidFare = paidFare, transferred = transferred,
                middleEvent = middleEvent, departureGatePassed = departureGatePassed, exitGatePassed = exitGatePassed, timer = timer, travelled = travelled, x = World.PlayerPosition.x, y = World.PlayerPosition.y,
                box = durability.BoxDurability, cake = durability.CakeDurability, stamina = World.Posture.StaminaRatio, failure = Failure,
                fee = LastSettlement.DeliveryFee, compensation = LastSettlement.Compensation, returnFare = LastSettlement.ReturnFare, net = LastSettlement.NetIncome };
            save.scholarship = ScholarshipAwarded;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save)); PlayerPrefs.Save();
        }
        void RestoreProgress()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            try
            {
                var save = JsonUtility.FromJson<Progress>(PlayerPrefs.GetString(SaveKey));
                if (save == null || save.version < 1 || save.version > 3 || save.cash < 0 || save.unlocked < 1 || save.insurance < 0 || save.fareSupport < 0) return;
                Economy.ApplyDelta(save.cash - Economy.CurrentEconomyState.CurrentCash);
                UnlockedCount = Mathf.Clamp(save.unlocked, 1, Catalog.orders.Length); Insurance = save.insurance; FareSupport = save.fareSupport;
                ScholarshipAwarded = save.scholarship;
                StaminaLevel = Mathf.Clamp(save.staminaLevel, 0, Catalog.maximumUpgradeLevel);
                BalanceLevel = Mathf.Clamp(save.balanceLevel, 0, Catalog.maximumUpgradeLevel);
                AgilityLevel = Mathf.Clamp(save.agilityLevel, 0, Catalog.maximumUpgradeLevel); ApplyUpgrades();
                if (save.version < 2 || save.phase == DeliveryPhase.None) return;
                int index = Array.FindIndex(Catalog.orders, order => order.id == save.orderId);
                // A saved delivery keeps its original stops/prices when the design catalog changes.
                SliceDeliveryOrder savedOrder = save.version >= 3 ? save.orderSnapshot :
                    Array.Find(Catalog.legacyOrders ?? Array.Empty<SliceDeliveryOrder>(), order => order.id == save.orderId);
                if (savedOrder == null && index >= 0) savedOrder = Catalog.orders[index];
                if (index < 0 || savedOrder?.stops == null || save.stop < 0 || save.stop >= savedOrder.stops.Length || !Enum.IsDefined(typeof(ServiceStep), save.serviceStep) ||
                    !Enum.IsDefined(typeof(DeliveryPhase), save.phase) || float.IsNaN(save.timer) || float.IsInfinity(save.timer) ||
                    float.IsNaN(save.x) || float.IsInfinity(save.x) || float.IsNaN(save.y) || float.IsInfinity(save.y)) return;
                restoring = true;
                resumedOrder = savedOrder;
                SelectedIndex = index; StopIndex = save.stop; Phase = save.phase; Failure = save.failure;
                transferred = save.transferred; middleEvent = save.middleEvent; step = (ServiceStep)save.serviceStep;
                departureGatePassed = save.departureGatePassed; exitGatePassed = save.exitGatePassed;
                timer = Mathf.Max(0.5f, save.timer); travelled = Mathf.Max(0, save.travelled); paidFare = save.paidFare;
                carrier.SetPackageAvailable(true); World.Package.RestoreDurability(save.box, save.cake);
                World.Posture.RestorePosture(CarryPosture.Standing, save.stamina);
                LastSettlement = new DeliverySettlementSnapshot(save.fee, save.compensation, save.paidFare, save.returnFare, save.net);
                World.TravelTo(Mathf.Clamp(save.map, 0, World.maps.Length - 1), new Vector2(save.x, save.y));
                SetDoor(step == ServiceStep.Open ? TrainDoorState.Open : step == ServiceStep.Opening ? TrainDoorState.Opening : step == ServiceStep.Closing ? TrainDoorState.Closing : TrainDoorState.Closed);
                PublishStop(); Notice = "저장한 배송을 이어갑니다."; restoring = false;
            }
            catch (ArgumentException) { Notice = "저장 데이터를 읽지 못했습니다. 새 진행으로 시작합니다."; }
            finally { restoring = false; }
        }
        void OnApplicationQuit() { SaveProgress(); }
        void OnDestroy()
        {
            if (World != null && World.Package != null) World.Package.DurabilityChanged -= OnDurability;
            if (InputBlocked) Time.timeScale = 1;
        }
    }
}
