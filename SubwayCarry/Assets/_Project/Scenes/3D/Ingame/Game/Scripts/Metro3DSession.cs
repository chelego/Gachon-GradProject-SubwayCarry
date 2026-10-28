using System;
using SubwayCarry.Core.Contracts;
using SubwayCarry.Delivery;
using SubwayCarry.Prototype.ArtMapSlice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DSession : MonoBehaviour
    {
        public Metro3DWorld World {get;private set;}
        internal void ConfigureWalkingReview(Metro3DWorld owner){World=owner;enabled=false;}
        public EconomyService Economy {get;private set;}
        public SliceDeliveryOrder Order {get;private set;}
        SliceDeliveryOrder activeRoute;
        public SliceDeliveryOrder Route => activeRoute??Order??World.settings.deliveries.orders[SelectedIndex];
        public int SelectedIndex {get;private set;}
        public int StopIndex {get;private set;}
        public int StaminaLevel {get;private set;}
        public int BalanceLevel {get;private set;}
        public int AgilityLevel {get;private set;}
        public int Unlocked {get;private set;}=1;
        public bool Insurance {get;private set;}
        public bool FareSupport {get;private set;}
        public bool HasPackage {get;private set;}
        public bool PhoneOpen {get;private set;}=true;
        public bool Paused {get;private set;}
        public bool ResultOpen {get;private set;}
        public bool Blocked => PhoneOpen||SimulationPaused||ResultOpen||Time.unscaledTime<damageUntil;
        public bool SimulationPaused => Paused||(World!=null&&World.Observer!=null&&World.Observer.SimulationFrozen);
        public bool CanPassengersBoard => step==ServiceStep.Dwell&&timer>7&&!TransferAtCurrentStop;
        public bool TransferAtCurrentStop => Route.stops[StopIndex].transfer&&!transferNeeded;
        public float DamageRemaining => Mathf.Max(0,damageUntil-Time.unscaledTime);
        public float Timer => Mathf.Max(0,timer);
        public string Notice {get;private set;}="배송을 선택하세요.";
        public string StationLabel => Route.stops[Mathf.Clamp(StopIndex,0,Route.stops.Length-1)].label;
        public string NextLabel => StopIndex+1<Route.stops.Length?Route.stops[StopIndex+1].label:"종착역";
        public SliceDeliveryRules.Settlement Settlement {get;private set;}
        public string ServiceLabel => step==ServiceStep.Travel?"다음 역 · "+NextLabel:step==ServiceStep.Approach?"열차 진입 중":step==ServiceStep.Dwell?"출입문 열림":"출입문 닫힘";
        enum ServiceStep { Off, Approach, Opening, Dwell, Closing, Travel, TransferDeparture, TransferWait }
        ServiceStep step;
        float timer,damageUntil,lastDamageTime,gateTime;
        float previousBox=100,previousCake=100;
        int outboundPaid;
        Metro3DFacility openGate;
        Metro3DTrain departing;
        bool gatePaid,transferNeeded,middleBalanceTriggered;
        const string SaveKey="SubwayCarry.3D.Progress.v1";
        [Serializable] class Save { public int cash,unlocked=1,stamina,balance,agility; }

        public void Initialize(Metro3DWorld world)
        {
            World=world; Economy=GetComponent<EconomyService>(); if(Economy==null) Economy=gameObject.AddComponent<EconomyService>();
            Load();activeRoute=world.settings.deliveries.orders[SelectedIndex];world.player.package.DurabilityChanged+=OnDamage;
            world.EnsureRoute(); world.SpawnPassengers(0,true); world.SpawnPassengers(0,false);
            StartIncoming(0); SetCursor();
        }
        public bool Select(int index)
        {
            if(HasPackage||index<0||index>=World.settings.deliveries.orders.Length||index>=Unlocked) return false;
            SelectedIndex=index; return true;
        }
        public void Accept()
        {
            if(HasPackage) { PhoneOpen=false; SetCursor(); return; }
            int revision=World.RouteRevision;
            Order=World.settings.deliveries.orders[SelectedIndex];activeRoute=Order;HasPackage=true;gatePaid=false;outboundPaid=0;middleBalanceTriggered=false;
            World.player.package.ResetToFull(); previousBox=previousCake=100; World.EnsureRoute();
            if(revision!=World.RouteRevision) { StopIndex=0; transferNeeded=false; World.SpawnPassengers(0,true); World.SpawnPassengers(0,false); StartIncoming(0); }
            else World.Transit?.HandOverPrimary();
            PhoneOpen=false; Notice="개찰구를 통과해 승강장으로 이동하세요."; SetCursor();
        }
        public void TogglePhone() { PhoneOpen=!PhoneOpen; SetCursor(); }
        internal void SetCursor() { bool free=Blocked||World.Observer!=null&&World.Observer.Active; Cursor.lockState=free?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=free; }
        public void ObservationInput(bool active)
        {
            // Observer input ownership does not change phone, pause or timetable.
            SetCursor();
        }
        public void ResumeService(Metro3DTrain service,float remaining)
        {
            StopIndex=service.CurrentStop;transferNeeded=false;
            if(service.Moving&&StopIndex>0){StopIndex--;step=ServiceStep.Travel;timer=service.MovementRemaining;}
            else if(service.Moving){step=ServiceStep.Approach;timer=service.MovementRemaining;}
            else {service.SetDoors(true);step=ServiceStep.Opening;timer=Mathf.Max(7,remaining);}
        }
        void Update()
        {
            var key=Keyboard.current;
            bool observing=World.Observer!=null&&World.Observer.Active;
            if(!observing&&key!=null&&key.tabKey.wasPressedThisFrame&&!ResultOpen) TogglePhone();
            if(!observing&&key!=null&&key.escapeKey.wasPressedThisFrame) { if(PhoneOpen) PhoneOpen=false; else Paused=!Paused; SetCursor(); }
            // Opening the player's phone or a damage overlay does not stop the city.
            if(SimulationPaused) return;
            timer-=Time.deltaTime;
            if(openGate!=null&&Time.time>gateTime) {
                if(openGate.gateDoor!=null) openGate.gateDoor.Close();
                else {openGate.blocker.enabled=true; openGate.anchor.localScale=Vector3.one;}
                openGate=null;
            }
            if(!HasPackage&&World.Transit!=null)return;
            switch(step) {
                case ServiceStep.Approach:
                    if(!World.train.Moving) { World.train.SetDoors(true); step=ServiceStep.Opening; timer=World.settings.doorSeconds; }
                    break;
                case ServiceStep.Opening:
                    if(World.train.DoorsOpen) { step=ServiceStep.Dwell; timer=World.settings.dwellSeconds; Notice=StopIndex==0?"열차에 탑승하세요.":"이번 역 · "+StationLabel; }
                    break;
                case ServiceStep.Dwell:
                    if(timer<=0) {
                        if(World.Traffic.HasDoorCrossing(World.train)||World.Traffic.HasAlighters(World.train)) {timer=1;break;}
                        World.train.SetDoors(false); step=ServiceStep.Closing; timer=World.settings.doorSeconds+1;
                    }
                    break;
                case ServiceStep.Closing:
                    if(timer>0) break;
                    bool required=StopIndex==Route.stops.Length-1||Route.stops[StopIndex].transfer;
                    if(required&&HasPackage&&World.player.Riding!=null&&!ResultOpen) Finish(true);
                    if(StopIndex==Route.stops.Length-1) { BeginReplacement(false); break; }
                    if(required&&!transferNeeded) { transferNeeded=true; BeginReplacement(true); break; }
                    World.EnsureStation(StopIndex+1);
                    middleBalanceTriggered=false;
                    if(HasPackage&&World.player.Riding!=null) World.player.TriggerBalanceMotion(TrainMotionPhase.Departing,Vector2.down,1f);
                    World.train.MoveTo(StopIndex+1,World.settings.travelSeconds); step=ServiceStep.Travel; timer=World.settings.travelSeconds;
                    break;
                case ServiceStep.Travel:
                    if(HasPackage&&World.player.Riding!=null&&World.player.UsesPersonalBalance) {
                        if(!middleBalanceTriggered&&timer<=World.settings.travelSeconds*.52f) {
                            middleBalanceTriggered=true;
                            bool emergency=Route.stars>=3&&StopIndex%2==1;
                            World.player.TriggerBalanceMotion(emergency?TrainMotionPhase.EmergencyBraking:TrainMotionPhase.SpeedChanging,
                                emergency?Vector2.up:Vector2.right,emergency?1.2f:.7f);
                        }
                        if(timer<4&&timer+Time.deltaTime>=4)
                            World.player.TriggerBalanceMotion(TrainMotionPhase.Arriving,Vector2.up,.8f);
                    } else if(HasPackage&&World.player.Riding!=null&&timer<2.5f&&timer+Time.deltaTime>=2.5f)
                        World.player.TriggerBalanceMotion(TrainMotionPhase.Arriving,Vector2.up,.8f);
                    if(!World.train.Moving) { StopIndex++; transferNeeded=false; World.train.SetDoors(true); step=ServiceStep.Opening; timer=World.settings.doorSeconds; }
                    break;
                case ServiceStep.TransferDeparture:
                    if(timer<=0) {
                        if(World.player.Riding==departing) { timer=1; break; }
                        World.Traffic.RetireTrain(departing);
                        World.facilities.RemoveAll(f=>f==null||f.transform.IsChildOf(departing.transform));
                        Destroy(departing.gameObject);
                        World.train=World.CreateTrain(World.ServiceSide,StopIndex); World.SpawnPassengers(StopIndex,true);
                        StartIncoming(StopIndex);
                    }
                    break;
            }
        }
        void BeginReplacement(bool transfer)
        {
            departing=World.train; departing.DepartOutOfRoute(10);
            if(!transfer) {StopIndex=0;transferNeeded=false;}
            step=ServiceStep.TransferDeparture; timer=12;
            Notice=transfer?"환승 열차를 기다리세요.":"다음 열차가 들어옵니다.";
        }
        void StartIncoming(int stop) {World.train.MoveTo(stop,World.settings.approachSeconds);step=ServiceStep.Approach;timer=World.settings.approachSeconds;}
        public void UseGate(Metro3DFacility gate)
        {
            if(gate.kind==Metro3DFacilityKind.FareGate) {
                if(!HasPackage) { Notice="먼저 배송을 선택하세요."; return; }
                if(!gatePaid&&!Economy.TrySpend(Order.outboundFare)) { Notice="교통비가 부족합니다."; return; }
                if(!gatePaid) { outboundPaid=Order.outboundFare; gatePaid=true; }
            } else {
                int station=World.stations.IndexOf(gate.transform.parent);
                if(!HasPackage||station!=Order.stops.Length-1||StopIndex!=station) { Notice="배송 목적지 출구가 아닙니다."; return; }
                Finish(false); return;
            }
            if(openGate!=null&&openGate!=gate&&openGate.gateDoor!=null) openGate.gateDoor.Close();
            if(gate.gateDoor!=null) gate.gateDoor.Open();
            else {gate.blocker.enabled=false; gate.anchor.localScale=new Vector3(.06f,1,1);}
            Notice="개찰구가 열렸습니다. 통과하세요.";
            openGate=gate; gateTime=Time.time+5;
        }
        void OnDamage(SubwayCarry.Core.Contracts.PackageDurabilitySnapshot d)
        {
            float loss=Mathf.Max(previousBox-d.BoxDurability,previousCake-d.CakeDurability);
            previousBox=d.BoxDurability; previousCake=d.CakeDurability;
            if(loss>=World.settings.deliveries.damageCutInThreshold&&Time.unscaledTime-lastDamageTime>4) { damageUntil=Time.unscaledTime+World.settings.deliveries.damageCutInSeconds; lastDamageTime=Time.unscaledTime; }
            if(d.DeliveryFailed&&HasPackage) Finish(false);
        }
        void Finish(bool missed)
        {
            if(ResultOpen||Order==null) return;
            var d=World.player.package.CurrentDurability;
            Settlement=SliceDeliveryRules.Evaluate(d.BoxDurability,d.CakeDurability,missed,Order.value,Order.fee,outboundPaid,Order.returnFare,Insurance,FareSupport);
            // Outbound fare was already spent at the gate; do not charge it twice.
            Economy.ApplyDelta(Settlement.NetIncome+outboundPaid);
            if(Settlement.Success) Unlocked=Mathf.Min(World.settings.deliveries.orders.Length,Mathf.Max(Unlocked,SelectedIndex+2));
            ResultOpen=true; Notice=Settlement.Success?"배송 완료":"배송 실패"; SetCursor(); SaveProgress();
        }
        public void ReturnToHub()
        {
            World.player.TeleportForReturn(World.HubSpawn);
            World.Traffic.RecycleAll();
            var oldTrains=GetComponentsInChildren<Metro3DTrain>(); foreach(var t in oldTrains) Destroy(t.gameObject);
            World.facilities.RemoveAll(f=>f==null||f.trainFacility);
            HasPackage=false; ResultOpen=false; PhoneOpen=true; Paused=false; StopIndex=0; Order=null; transferNeeded=false; middleBalanceTriggered=false;
            World.ResetServiceSide(); World.train=World.CreateTrain(World.ServiceSide,0); World.EnsureRoute(); World.SpawnPassengers(0,true); World.SpawnPassengers(0,false); StartIncoming(0);
            Insurance=FareSupport=false; damageUntil=0;
            if(Economy.CurrentEconomyState.CurrentCash<0) { Economy.ResetToInitial(); Unlocked=1; StaminaLevel=BalanceLevel=AgilityLevel=0; Notice="새 배송을 시작하세요."; }
            SaveProgress(); SetCursor();
        }
        public void Purchase(int kind)
        {
            var c=World.settings.deliveries;
            if(kind==3) { if(!Insurance&&Economy.TrySpend(c.insurancePrice)) Insurance=true; }
            else if(kind==4) { if(!FareSupport&&Economy.TrySpend(c.fareSupportPrice)) FareSupport=true; }
            else {
                int level=kind==0?StaminaLevel:kind==1?BalanceLevel:AgilityLevel;
                if(level>=c.maximumUpgradeLevel||!Economy.TrySpend(c.upgradeBasePrice*(level+1))) return;
                if(kind==0) StaminaLevel++; else if(kind==1) BalanceLevel++; else AgilityLevel++;
            }
            SaveProgress();
        }
        void SaveProgress()
        {
            PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(new Save {cash=Economy.CurrentEconomyState.CurrentCash,unlocked=Unlocked,stamina=StaminaLevel,balance=BalanceLevel,agility=AgilityLevel})); PlayerPrefs.Save();
        }
        void Load()
        {
            if(!PlayerPrefs.HasKey(SaveKey)) return;
            try { var save=JsonUtility.FromJson<Save>(PlayerPrefs.GetString(SaveKey)); if(save==null) return;
                Economy.ApplyDelta(Mathf.Max(0,save.cash)-Economy.CurrentEconomyState.CurrentCash); Unlocked=Mathf.Clamp(save.unlocked,1,World.settings.deliveries.orders.Length);
                StaminaLevel=Mathf.Clamp(save.stamina,0,3); BalanceLevel=Mathf.Clamp(save.balance,0,3); AgilityLevel=Mathf.Clamp(save.agility,0,3);
            } catch(ArgumentException) { Notice="저장 데이터를 읽지 못했습니다."; }
        }
        void OnApplicationQuit() { if(Economy!=null) SaveProgress(); }
        void OnDestroy() { if(World!=null&&World.player!=null) World.player.package.DurabilityChanged-=OnDamage; }
    }
}
