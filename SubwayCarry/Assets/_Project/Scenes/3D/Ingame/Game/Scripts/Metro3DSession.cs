using System;
using SubwayCarry.Core.Contracts;
using SubwayCarry.Delivery;
using SubwayCarry.Prototype.ArtMapSlice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay3D
{
    public sealed partial class Metro3DSession : MonoBehaviour
    {
        [SerializeField] bool startFreshOnLaunch;
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
        public bool EntryPaid => gatePaid;
        public string ResultReason {get;private set;}
        internal string DestinationLabel => Route.stops[Route.stops.Length-1].label;
        internal bool IsDeliveryDirection(Metro3DTrain train)
            =>train!=null&&train.TravelDirection>0&&train.side==DeliveryPlatformSide&&train.ServiceLine==DeliveryLine;
        internal bool IsRequiredStop(int stop) => stop>=0&&stop<Route.stops.Length&&
            (stop==Route.stops.Length-1||Route.stops[stop].transfer&&stop!=legStart);
        internal void PlayerBoarded(Metro3DTrain service)
        {
            if(!HasPackage||ResultOpen||!gatePaid||TransferWalkingRequired||!IsDeliveryDirection(service)||service==World.train||
               service.Moving||!service.DoorsOpen||service.CurrentStop==Route.stops.Length-1||
               World.StationAt(World.player.transform.position)!=service.CurrentStop)return;
            World.Transit.HandOverBoardedService(service);
        }
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
        AudioSource cues;
        AudioClip acceptedCue,doorOpenCue,doorWarningCue,phoneCue;
        AudioSource lifeCue,transitCue;
        internal void PhoneNotification(Vector3 position)
        {
            if(lifeCue==null) {var o=new GameObject("Passenger notification");o.transform.SetParent(transform,false);lifeCue=o.AddComponent<AudioSource>();lifeCue.spatialBlend=1;lifeCue.minDistance=1;lifeCue.maxDistance=8;lifeCue.rolloffMode=AudioRolloffMode.Linear;lifeCue.volume=.14f;lifeCue.playOnAwake=false;}
            if(phoneCue==null) {const int rate=22050;var samples=new float[rate/3];for(int i=0;i<samples.Length;i++){float t=i/(float)rate;float envelope=Mathf.Sin(Mathf.PI*t*3);samples[i]=(Mathf.Sin(t*2*Mathf.PI*1046)+.25f*Mathf.Sin(t*2*Mathf.PI*1568))*envelope*envelope*.25f;}phoneCue=AudioClip.Create("Passenger notification",samples.Length,1,rate,false);phoneCue.SetData(samples,0);}
            lifeCue.transform.position=position+Vector3.up*1.1f;lifeCue.PlayOneShot(phoneCue);
        }
        bool departureWarning;
        Metro3DTransitSimulation.BoardingRecovery boardingRecovery;
        internal void TrainCue(Metro3DTrain train,bool opened)
        {
            if(train==null||World.player==null||World.Observer!=null&&World.Observer.Active)return;
            Vector3 feet=World.player.transform.position;
            if(World.player.Riding!=train&&
                (train.DoorPoint(train.NearestDoor(feet),true)-feet).sqrMagnitude>13f*13f)return;
            if(transitCue==null){var cue=new GameObject("Train door cue");transitCue=cue.AddComponent<AudioSource>();transitCue.playOnAwake=false;transitCue.spatialBlend=1;transitCue.minDistance=2;transitCue.maxDistance=15;transitCue.rolloffMode=AudioRolloffMode.Linear;transitCue.volume=.24f;}
            transitCue.Stop();transitCue.transform.SetParent(train.transform,true);
            transitCue.transform.position=train.DoorPoint(train.NearestDoor(feet),World.player.Riding!=train)+Vector3.up*2;
            if(opened)PlayCue(ref doorOpenCue,"Doors open",690,880,transitCue);
            else PlayCue(ref doorWarningCue,"Doors closing",545,545,transitCue);
        }
        void PlayCue(ref AudioClip clip,string label,float first,float second,AudioSource source=null)
        {
            if(World.player.view==null)return;
            if(cues==null) {
                cues=World.player.view.gameObject.AddComponent<AudioSource>();
                cues.playOnAwake=false;cues.volume=.24f;
            }
            if(clip==null) {
                const int rate=24000,length=7200;
                var samples=new float[length];
                for(int i=0;i<length;i++) {
                    float time=i/(float)rate;
                    float local=time<.15f?time:time-.15f;
                    float frequency=time<.15f?first:second;
                    float envelope=Mathf.Clamp01(local/.012f)*Mathf.Clamp01((.145f-local)/.04f);
                    samples[i]=Mathf.Sin(2*Mathf.PI*frequency*local)*envelope*.36f;
                }
                clip=AudioClip.Create(label,length,1,rate,false);clip.SetData(samples,0);
            }
            (source!=null?source:cues).PlayOneShot(clip);
        }
        bool gatePaid,transferNeeded,middleBalanceTriggered;
        const string BoardingNotice="열차에 탑승하세요.";
        const string StandardSaveKey="SubwayCarry.3D.Progress.v1";
        string SaveKey => World.UsesStationLayouts ? "SubwayCarry.3D.StationLayoutTest.Progress" : StandardSaveKey;


        public void Initialize(Metro3DWorld world)
        {
            World=world; Economy=GetComponent<EconomyService>(); if(Economy==null) Economy=gameObject.AddComponent<EconomyService>();
            Load();activeRoute=world.settings.deliveries.orders[SelectedIndex];world.player.package.DurabilityChanged+=OnDamage;
            world.EnsureRoute(); world.SpawnPassengers(0,false);
            StartIncoming(0); world.player.RefreshCargo();SetCursor();
        }
        public bool Select(int index)
        {
            if(HasPackage||index<0||index>=World.settings.deliveries.orders.Length||index>=Unlocked) return false;
            SelectedIndex=index; return true;
        }
        public void Accept()
        {
            if(ResultOpen)return;
            if(HasPackage) { PhoneOpen=false; SetCursor(); return; }
            int revision=World.RouteRevision;
            Order=World.settings.deliveries.orders[SelectedIndex];activeRoute=Order;HasPackage=true;legStart=0;transferConcourseVisited=destinationReached=false;gatePaid=false;outboundPaid=0;middleBalanceTriggered=false;ResultReason=null;
            World.player.package.ResetToFull(); previousBox=previousCake=100; World.EnsureRoute();
            if(revision!=World.RouteRevision) { StopIndex=0; transferNeeded=false; World.SpawnPassengers(0,true); World.SpawnPassengers(0,false); StartIncoming(0); }
            else World.Transit?.HandOverPrimary();
            World.player.RefreshCargo();PhoneOpen=false; Notice="개찰구를 통과해 승강장으로 이동하세요."; SetCursor(); SaveProgress();
        }
        public void TogglePhone() { PhoneOpen=!PhoneOpen; SetCursor(); }
        internal void SetCursor() { bool free=Blocked||World.Observer!=null&&World.Observer.Active; Cursor.lockState=free?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=free; }
        public void ObservationInput(bool active)
        {
            // Observer input ownership does not change phone, pause or timetable.
            SetCursor();
        }
        public void ResumeService(Metro3DTrain service,float remaining,bool closing=false)
        {
            boardingRecovery=default;
            StopIndex=service.CurrentStop;transferNeeded=legStart==StopIndex&&legStart>0;
            if(service.Moving&&StopIndex>0){StopIndex--;step=ServiceStep.Travel;timer=service.MovementRemaining;}
            else if(service.Moving){step=ServiceStep.Approach;timer=service.MovementRemaining;}
            else if(closing){step=ServiceStep.Closing;timer=0;}
            else if(service.DoorsOpen){step=ServiceStep.Dwell;timer=Mathf.Max(0,remaining);departureWarning=false;}
            else {service.SetDoors(true);step=ServiceStep.Opening;timer=World.settings.doorSeconds;}
        }
        internal void ResumeBoardingRecovery(Metro3DTransitSimulation.BoardingRecovery recovery,bool warning)
        {
            boardingRecovery=recovery;departureWarning=warning;
        }
        void Update()
        {
            var key=Keyboard.current;
            bool observing=World.Observer!=null&&World.Observer.Active;
            if(!observing&&key!=null&&key.tabKey.wasPressedThisFrame&&!ResultOpen) TogglePhone();
            if(!observing&&key!=null&&key.escapeKey.wasPressedThisFrame) { if(PhoneOpen) PhoneOpen=false; else Paused=!Paused; SetCursor(); }
            // Opening the player's phone or a damage overlay does not stop the city.
            if(!startFreshOnLaunch&&Time.unscaledTime>=nextProgressSave)SaveProgress();
            if(SimulationPaused||ResultOpen) return;
            ObserveTransferProgress();
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
                    if(World.train.DoorsOpen) { step=ServiceStep.Dwell; timer=World.settings.dwellSeconds;departureWarning=false;boardingRecovery=default; Notice=StopIndex==0?BoardingNotice:string.Empty; }
                    break;
                case ServiceStep.Dwell:
                    if(!TransferAtCurrentStop&&StopIndex<Route.stops.Length-1)
                        World.Transit.RecoverBoardingTime(World.train,ref timer,ref boardingRecovery,departureWarning);
                    if(!departureWarning&&timer<=5) {departureWarning=true;TrainCue(World.train,false);}
                    if(timer<=0) {
                        if(World.Traffic.HasDoorCrossing(World.train)||World.Traffic.HasAlighters(World.train)) {timer=1;break;}
                        if(Notice==BoardingNotice) Notice=string.Empty;
                        World.train.SetDoors(false); step=ServiceStep.Closing; timer=World.settings.doorSeconds+1;
                    }
                    break;
                case ServiceStep.Closing:
                    if(timer>0||World.train.OpenAmount>.001f) break;
                    bool required=IsRequiredStop(StopIndex);
                    if(required&&HasPackage&&World.player.Riding==World.train&&!ResultOpen) Finish(true);
                    if(transferNeeded&&StopIndex==legStart&&World.player.Riding!=World.train){BeginReplacement(false,true);break;}
                    if(StopIndex==Route.stops.Length-1) { BeginReplacement(false); break; }
                    if(required&&!transferNeeded) { transferNeeded=true; BeginReplacement(true); break; }
                    World.EnsureStation(StopIndex+1);
                    if(!World.Transit.CanAdvancePrimary(StopIndex+1)){Notice="앞 열차 통과 대기";break;}
                    middleBalanceTriggered=false;
                    if(HasPackage&&World.player.Riding==World.train) World.player.TriggerBalanceMotion(TrainMotionPhase.Departing,Vector2.down,1f);
                    World.train.MoveTo(StopIndex+1,World.settings.travelSeconds); step=ServiceStep.Travel; timer=World.settings.travelSeconds;
                    break;
                case ServiceStep.Travel:
                    if(HasPackage&&World.player.Riding==World.train&&World.player.UsesPersonalBalance) {
                        if(!middleBalanceTriggered&&timer<=World.settings.travelSeconds*.52f) {
                            middleBalanceTriggered=true;
                            bool emergency=Route.stars>=3&&StopIndex%2==1;
                            World.player.TriggerBalanceMotion(emergency?TrainMotionPhase.EmergencyBraking:TrainMotionPhase.SpeedChanging,
                                emergency?Vector2.up:Vector2.right,emergency?1.2f:.7f);
                        }
                        if(timer<4&&timer+Time.deltaTime>=4)
                            World.player.TriggerBalanceMotion(TrainMotionPhase.Arriving,Vector2.up,.8f);
                    } else if(HasPackage&&World.player.Riding==World.train&&timer<2.5f&&timer+Time.deltaTime>=2.5f)
                        World.player.TriggerBalanceMotion(TrainMotionPhase.Arriving,Vector2.up,.8f);
                    if(!World.train.Moving) { StopIndex++; transferNeeded=false; World.Traffic.ArriveWithBackgroundRiders(World.train); World.train.SetDoors(true); step=ServiceStep.Opening; timer=World.settings.doorSeconds; }
                    break;
                case ServiceStep.TransferDeparture:
                    if(timer<=0) {
                        if(World.player.Riding==departing) { timer=1; break; }
                        if(!World.Transit.CanIntroduceService(DeliveryPlatformSide,StopIndex,departing)){timer=1;break;}
                        World.Traffic.RetireTrain(departing);
                        World.facilities.RemoveAll(f=>f==null||f.transform.IsChildOf(departing.transform));
                        Destroy(departing.gameObject);
                        World.train=World.CreateTrain(DeliveryPlatformSide,StopIndex);World.train.SetServiceLine(DeliveryLine);
                        StartIncoming(StopIndex);
                        World.Transit.RegisterPrimary(World.train);World.SpawnPassengers(StopIndex,true);
                    }
                    break;
            }
        }
        void BeginReplacement(bool transfer,bool repeatTransfer=false)
        {
            departing=World.train; departing.DepartOutOfRoute(10);
            if(transfer){legStart=StopIndex;transferConcourseVisited=World.player.Riding==null&&World.StationAt(World.player.transform.position)==legStart&&World.player.transform.position.y>World.settings.departureFloor-.35f;}
            else if(!repeatTransfer) {StopIndex=0;transferNeeded=false;}
            step=ServiceStep.TransferDeparture; timer=12;
            Notice=transfer?DeliveryLine+" 환승 · 계단으로 올라가 반대편 승강장으로 이동하세요.":"다음 열차가 들어옵니다.";
            SaveProgress();
        }
        void StartIncoming(int stop) {World.train.MoveTo(stop,World.settings.approachSeconds);step=ServiceStep.Approach;timer=World.settings.approachSeconds;}
        public void UseGate(Metro3DFacility gate)
        {
            if(gate.kind==Metro3DFacilityKind.FareGate) {
                if(!HasPackage) { Notice="먼저 배송을 선택하세요."; return; }
                if(!gatePaid&&!Economy.TrySpend(Order.outboundFare)) { Notice="교통비가 부족합니다."; return; }
                if(!gatePaid) { outboundPaid=Order.outboundFare; gatePaid=true;SaveProgress(); }
            } else {
                int station=World.stations.IndexOf(gate.transform.parent);
                if(!HasPackage||!gatePaid||World.player.Riding!=null||station!=Order.stops.Length-1||!destinationReached) { Notice="배송 목적지 출구가 아닙니다."; return; }
                Finish(false); return;
            }
            if(openGate!=null&&openGate!=gate&&openGate.gateDoor!=null) openGate.gateDoor.Close();
            if(gate.gateDoor!=null) gate.gateDoor.Open();
            else {gate.blocker.enabled=false; gate.anchor.localScale=new Vector3(.06f,1,1);}
            PlayCue(ref acceptedCue,"Fare accepted",880,1100);
            Notice="개찰구가 열렸습니다. 통과하세요.";
            openGate=gate; gateTime=Time.time+5;
            SaveProgress();
        }
        void OnDamage(SubwayCarry.Core.Contracts.PackageDurabilitySnapshot d)
        {
            if(restoringProgress)return;
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
            ResultReason=missed?(StopIndex==Route.stops.Length-1?"목적역에서 내리지 못했어요.":"환승역에서 내리지 못했어요."):
                Settlement.Success?"목적역 출구에 케이크를 전달했어요.":"운반 중 케이크가 파손됐어요.";
            if(Settlement.UsedInsurance)Insurance=false;
            if(Settlement.UsedFareSupport)FareSupport=false;
            if(Economy.CurrentEconomyState.CurrentCash<0)ResetBankruptProgress();
            ResultOpen=true; Notice=Settlement.Success?"배송 완료":"배송 실패"; SetCursor(); SaveProgress();
        }
        internal void BeginFreshGame()
        {
            if(openGate!=null) {
                if(openGate.gateDoor!=null)openGate.gateDoor.Close();
                else {openGate.blocker.enabled=true;openGate.anchor.localScale=Vector3.one;}
                openGate=null;
            }
            pendingProgress=null;
            ResetBankruptProgress();
            World.player.package.ResetToFull();
            previousBox=previousCake=100;
            ReturnToHub();
        }

        public void ReturnToHub()
        {
            if(Bankrupt){SelectedIndex=0;activeRoute=World.settings.deliveries.orders[0];World.ResetStationLayouts();}
            World.player.TeleportForReturn(World.HubSpawn);
            World.Traffic.RecycleAll();
            var oldTrains=GetComponentsInChildren<Metro3DTrain>(); foreach(var t in oldTrains) Destroy(t.gameObject);
            World.facilities.RemoveAll(f=>f==null||f.trainFacility);
            HasPackage=false; ResultOpen=false; PhoneOpen=true; Paused=false; StopIndex=legStart=0; Order=null; transferNeeded=transferConcourseVisited=destinationReached=false; middleBalanceTriggered=false;ResultReason=null;Notice="배송을 선택하세요.";
            World.ResetServiceSide(); World.train=World.CreateTrain(World.ServiceSide,0); World.EnsureRoute(); World.SpawnPassengers(0,true); World.SpawnPassengers(0,false); StartIncoming(0);
            Bankrupt=false;damageUntil=0;World.player.RefreshCargo();

            SaveProgress(); SetCursor();
        }
        void OnApplicationQuit() { if(Economy!=null) SaveProgress(); }
        void OnDestroy()
        {
            if(World!=null&&World.player!=null)World.player.package.DurabilityChanged-=OnDamage;
            if(phoneCue!=null)Destroy(phoneCue);
            if(acceptedCue!=null)Destroy(acceptedCue);
            if(doorOpenCue!=null)Destroy(doorOpenCue);
            if(doorWarningCue!=null)Destroy(doorWarningCue);
        }
    }
}
