using System;
using SubwayCarry.Delivery;
using SubwayCarry.Prototype.ArtMapSlice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DSession : MonoBehaviour
    {
        public Metro3DWorld World {get;private set;}
        public EconomyService Economy {get;private set;}
        public SliceDeliveryOrder Order {get;private set;}
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
        public bool Blocked => PhoneOpen||Paused||ResultOpen||Time.unscaledTime<damageUntil;
        public bool CanPassengersBoard => step==ServiceStep.Dwell&&timer<World.settings.dwellSeconds-3;
        public float DamageRemaining => Mathf.Max(0,damageUntil-Time.unscaledTime);
        public float Timer => Mathf.Max(0,timer);
        public string Notice {get;private set;}="배송을 선택하세요.";
        public string StationLabel => Order==null?"가천대":Order.stops[StopIndex].label;
        public string NextLabel => Order!=null&&StopIndex+1<Order.stops.Length?Order.stops[StopIndex+1].label:"종착역";
        public SliceDeliveryRules.Settlement Settlement {get;private set;}
        public string ServiceLabel => step==ServiceStep.Travel?"다음 역 · "+NextLabel:step==ServiceStep.Approach?"열차 진입 중":step==ServiceStep.Dwell?"출입문 열림":"출입문 닫힘";
        enum ServiceStep { Off, Approach, Opening, Dwell, Closing, Travel, TransferDeparture, TransferWait }
        ServiceStep step;
        float timer,damageUntil,lastDamageTime,gateTime;
        float previousBox=100,previousCake=100;
        int outboundPaid;
        Metro3DFacility openGate;
        Metro3DTrain departing;
        bool gatePaid,transferNeeded;
        const string SaveKey="SubwayCarry.3D.Progress.v1";
        [Serializable] class Save { public int cash,unlocked=1,stamina,balance,agility; }

        public void Initialize(Metro3DWorld world)
        {
            World=world; Economy=GetComponent<EconomyService>(); if(Economy==null) Economy=gameObject.AddComponent<EconomyService>();
            Load(); world.player.package.DurabilityChanged+=OnDamage;
            world.SpawnPassengers(0,true); SetCursor();
        }
        public bool Select(int index)
        {
            if(HasPackage||index<0||index>=World.settings.deliveries.orders.Length||index>=Unlocked) return false;
            SelectedIndex=index; return true;
        }
        public void Accept()
        {
            if(HasPackage) { PhoneOpen=false; SetCursor(); return; }
            Order=World.settings.deliveries.orders[SelectedIndex]; StopIndex=0; HasPackage=true; gatePaid=false; outboundPaid=0;
            World.player.package.ResetToFull(); previousBox=previousCake=100; World.EnsureRoute();
            PhoneOpen=false; Notice="개찰구를 통과해 승강장으로 이동하세요."; SetCursor();
        }
        public void TogglePhone() { PhoneOpen=!PhoneOpen; SetCursor(); }
        void SetCursor() { bool free=Blocked; Cursor.lockState=free?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=free; }
        void Update()
        {
            var key=Keyboard.current;
            if(key!=null&&key.tabKey.wasPressedThisFrame&&!ResultOpen) TogglePhone();
            if(key!=null&&key.escapeKey.wasPressedThisFrame) { if(PhoneOpen) PhoneOpen=false; else Paused=!Paused; SetCursor(); }
            if(Blocked) return;
            timer-=Time.deltaTime;
            if(openGate!=null&&Time.time>gateTime) {
                if(openGate.gateDoor!=null) openGate.gateDoor.Close();
                else {openGate.blocker.enabled=true; openGate.anchor.localScale=Vector3.one;}
                openGate=null;
            }
            switch(step) {
                case ServiceStep.Approach:
                    if(!World.train.Moving) { World.train.SetDoors(true); step=ServiceStep.Opening; timer=World.settings.doorSeconds; }
                    break;
                case ServiceStep.Opening:
                    if(World.train.DoorsOpen) { step=ServiceStep.Dwell; timer=World.settings.dwellSeconds; Notice=StopIndex==0?"열차에 탑승하세요.":"이번 역 · "+StationLabel; }
                    break;
                case ServiceStep.Dwell:
                    if(timer<=0) { World.train.SetDoors(false); step=ServiceStep.Closing; timer=World.settings.doorSeconds+1; }
                    break;
                case ServiceStep.Closing:
                    if(timer>0) break;
                    if(StopIndex==0&&World.player.Riding==null) { BeginReplacement(false); break; }
                    bool required=StopIndex==Order.stops.Length-1||Order.stops[StopIndex].transfer;
                    if(required&&World.player.Riding!=null) { Finish(true); break; }
                    if(StopIndex==Order.stops.Length-1) { step=ServiceStep.Off; Notice="계단을 올라가 출구로 이동하세요."; break; }
                    if(required&&!transferNeeded) { transferNeeded=true; BeginReplacement(true); break; }
                    World.player.TriggerBalance(false); World.train.MoveTo(StopIndex+1,World.settings.travelSeconds); step=ServiceStep.Travel; timer=World.settings.travelSeconds;
                    break;
                case ServiceStep.Travel:
                    if(timer<2.5f&&timer+Time.deltaTime>=2.5f) World.player.TriggerBalance(true);
                    if(!World.train.Moving) { StopIndex++; transferNeeded=false; World.SpawnPassengers(StopIndex,false); World.train.SetDoors(true); step=ServiceStep.Opening; timer=World.settings.doorSeconds; }
                    break;
                case ServiceStep.TransferDeparture:
                    if(timer<=0) {
                        World.facilities.RemoveAll(f=>f==null||f.transform.IsChildOf(departing.transform));
                        Destroy(departing.gameObject);
                        World.train=World.CreateTrain(World.ServiceSide,StopIndex); World.SpawnPassengers(StopIndex,true);
                        World.train.MoveTo(StopIndex,World.settings.approachSeconds); step=ServiceStep.Approach; timer=World.settings.approachSeconds;
                    }
                    break;
            }
        }
        void BeginReplacement(bool transfer)
        {
            departing=World.train; departing.DepartOutOfRoute(10);
            step=ServiceStep.TransferDeparture; timer=12;
            Notice=transfer?"환승 열차를 기다리세요.":"다음 열차가 들어옵니다.";
        }
        public void UseGate(Metro3DFacility gate)
        {
            if(gate.kind==Metro3DFacilityKind.FareGate) {
                if(!HasPackage) { Notice="먼저 배송을 선택하세요."; return; }
                if(!gatePaid&&!Economy.TrySpend(Order.outboundFare)) { Notice="교통비가 부족합니다."; return; }
                if(!gatePaid) { outboundPaid=Order.outboundFare; gatePaid=true; World.train.MoveTo(0,World.settings.approachSeconds); step=ServiceStep.Approach; timer=World.settings.approachSeconds; }
            } else {
                int station=World.stations.IndexOf(gate.transform.parent);
                if(!HasPackage||station!=Order.stops.Length-1||StopIndex!=station) { Notice="배송 목적지 출구가 아닙니다."; return; }
                Finish(false); return;
            }
            if(openGate!=null&&openGate!=gate&&openGate.gateDoor!=null) openGate.gateDoor.Close();
            if(gate.gateDoor!=null) gate.gateDoor.Open();
            else {gate.blocker.enabled=false; gate.anchor.localScale=new Vector3(.06f,1,1);}
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
            for(int i=World.passengers.Count-1;i>=0;i--) if(World.passengers[i]!=null) Destroy(World.passengers[i].gameObject);
            World.passengers.Clear();
            var oldTrains=GetComponentsInChildren<Metro3DTrain>(); foreach(var t in oldTrains) Destroy(t.gameObject);
            World.facilities.RemoveAll(f=>f==null||f.trainFacility);
            World.ResetServiceSide(); World.train=World.CreateTrain(World.ServiceSide,0); World.SpawnPassengers(0,true); World.SpawnPassengers(0,false);
            HasPackage=false; ResultOpen=false; PhoneOpen=true; Paused=false; step=ServiceStep.Off; StopIndex=0; Order=null; transferNeeded=false;
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
