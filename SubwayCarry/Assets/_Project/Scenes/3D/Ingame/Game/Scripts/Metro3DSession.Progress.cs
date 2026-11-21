using System;
using System.Linq;
using SubwayCarry.Prototype.ArtMapSlice;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed partial class Metro3DSession
    {
        // Extends the existing private v1 key; no shared Core save contract is replaced.
        [Serializable] class Save
        {
            public int version,cash,unlocked=1,stamina,balance,agility;
            public bool insurance,fareSupport,accepted,settled,bankrupt,paid,transfer;
            public bool cushionPad,energyGel;
            public string orderId,resultReason;
            public int stop,outbound,phase,playerStation,trainSide,leg;
            public bool transferWalked,destinationReached;
            public float remaining,box=100,cake=100,playerStamina=100,viewPitch;
            public bool riding,ridingOther,phone=true;
            public Vector3 playerPosition;
            public Quaternion playerRotation=Quaternion.identity;
            public int facility=-1;
            public Vector3 facilityAnchor;
            public Metro3DTrain.Progress train;
            public Metro3DTrain.Progress otherRide;
            public int otherRideSide,openGateStation=-1;
            public float otherRideWait,openGateRemaining;
            public Vector3 openGatePosition;
            public bool success,usedInsurance,usedFare;
            public int fee,compensation,returnFare;
            public Metro3DWorld.StationLayoutChoice[] stationLayouts;
        }
        Save pendingProgress;
        bool restoringProgress;
        float nextProgressSave;
        public bool Bankrupt {get;private set;}
        public bool HasCushionPad {get;private set;}
        public bool HasEnergyGel {get;private set;}
        internal const int CushionPadItem = 5;
        internal const int EnergyGelItem = 6;
        internal float BalanceAssistance => BalanceLevel *
            (World.settings.deliveries.balancePerLevel + World.settings.extraBalancePerLevel);

        public readonly struct PurchaseOffer
        {
            public readonly bool Available;
            public readonly int Price;
            public readonly string Reason;
            internal PurchaseOffer(bool available,int price,string reason){Available=available;Price=price;Reason=reason;}
        }
        public PurchaseOffer GetPurchaseOffer(int kind)
        {
            if(kind<0||kind>EnergyGelItem)return new PurchaseOffer(false,0,"없는 상품");
            var c=World.settings.deliveries;
            int level=kind==0?StaminaLevel:kind==1?BalanceLevel:AgilityLevel;
            int price=kind switch
            {
                3 => c.insurancePrice,
                4 => c.fareSupportPrice,
                CushionPadItem => Mathf.Max(1, World.settings.cushionPadPrice),
                EnergyGelItem => Mathf.Max(1, World.settings.energyGelPrice),
                _ => c.upgradeBasePrice * (level + 1)
            };
            if(ResultOpen)return new PurchaseOffer(false,price,"정산 후 이용");
            if(World.player.Riding!=null)return new PurchaseOffer(false,price,"역에서 구매");
            int station=World.StationAt(World.player.transform.position);
            var stop=Route.stops[Mathf.Clamp(station,0,Route.stops.Length-1)];
            if(kind<3&&!stop.transfer)return new PurchaseOffer(false,price,"환승역에서 구매");
            if(kind>=3&&stop.id!="gachon")return new PurchaseOffer(false,price,"가천대역에서 구매");
            bool held = kind switch
            {
                3 => Insurance,
                4 => FareSupport,
                CushionPadItem => HasCushionPad,
                EnergyGelItem => HasEnergyGel,
                _ => false
            };
            if(held)return new PurchaseOffer(false,price,"보유 중 · 1회");
            if(kind<3&&level>=c.maximumUpgradeLevel)return new PurchaseOffer(false,price,"최대 단계");
            if(price<0||Economy.CurrentEconomyState.CurrentCash<price)return new PurchaseOffer(false,price,"잔액 부족");
            return new PurchaseOffer(true,price,"구매 가능");
        }
        public void Purchase(int kind)
        {
            var offer=GetPurchaseOffer(kind);
            if(!offer.Available){Notice=offer.Reason;return;}
            if(!Economy.TrySpend(offer.Price)){Notice="잔액 부족";return;}
            if(kind==0)StaminaLevel++;else if(kind==1)BalanceLevel++;else if(kind==2)AgilityLevel++;
            else if(kind==3)Insurance=true;else if(kind==4)FareSupport=true;
            else if(kind==CushionPadItem)HasCushionPad=true;
            else HasEnergyGel=true;

            Notice=kind switch
            {
                CushionPadItem => "완충 패드 · 다음 운반 충돌에서 자동 사용",
                EnergyGelItem => $"에너지 젤 · 체력 {Mathf.RoundToInt(World.settings.gelUseThreshold*100)}% 이하에서 R키 사용",
                <3 => "강화가 적용됐습니다.",
                _ => "1회 지원을 보유하고 있습니다."
            };
            SaveProgress();
        }

        internal bool TryConsumeCushionPad()
        {
            if(!HasCushionPad)return false;
            HasCushionPad=false;
            return true;
        }

        internal bool UseEnergyGel()
        {
            if(!HasEnergyGel||Blocked||!HasPackage)return false;
            var player=World.player;
            float cap=player.MaxStamina;
            if(player.Stamina>cap*World.settings.gelUseThreshold)return false;

            player.RestoreStamina(cap*World.settings.gelStaminaRecovery);
            HasEnergyGel=false;
            SaveProgress();
            return true;
        }

        void Start(){RestoreProgress();}
        internal void SaveProgress()
        {
            if(startFreshOnLaunch||restoringProgress||World==null||World.player==null||Economy==null)return;
            var player=World.player;
            bool aboard=player.Riding!=null;
            Transform frame=aboard?player.Riding.transform:World.transform;
            var d=player.package.CurrentDurability;
            var save=new Save{version=World.UsesStationLayouts ? 3 : 2,cash=Economy.CurrentEconomyState.CurrentCash,unlocked=Unlocked,
                stamina=StaminaLevel,balance=BalanceLevel,agility=AgilityLevel,insurance=Insurance,fareSupport=FareSupport,
                cushionPad=HasCushionPad,energyGel=HasEnergyGel,
                leg=legStart,transferWalked=transferConcourseVisited,destinationReached=destinationReached,accepted=HasPackage,settled=ResultOpen,bankrupt=Bankrupt,paid=gatePaid,transfer=transferNeeded,
                orderId=(Order??Route).id,stop=StopIndex,outbound=outboundPaid,phase=(int)step,remaining=timer,
                box=d.BoxDurability,cake=d.CakeDurability,riding=aboard,ridingOther=aboard&&player.Riding!=World.train,phone=PhoneOpen,
                playerStation=World.StationAt(player.transform.position),playerPosition=frame.InverseTransformPoint(player.transform.position),
                playerRotation=Quaternion.Inverse(frame.rotation)*player.transform.rotation,playerStamina=player.Stamina,viewPitch=player.SavedViewPitch,
                trainSide=World.train!=null?World.train.side:World.ServiceSide,train=World.train!=null?World.train.CaptureProgress():null,
                otherRide=aboard&&player.Riding!=World.train?player.Riding.CaptureProgress():null,
                otherRideSide=aboard?player.Riding.side:0,otherRideWait=aboard?World.Transit.BoardingSeconds(player.Riding)+7:0,
                openGateStation=openGate!=null?World.stations.IndexOf(openGate.transform.parent):-1,
                openGatePosition=openGate!=null?openGate.transform.localPosition:Vector3.zero,
                openGateRemaining=openGate!=null?Mathf.Max(0,gateTime-Time.time):0,
                facility=player.Using!=null?(int)player.Using.kind:-1,
                facilityAnchor=player.Using!=null?frame.InverseTransformPoint(player.Using.anchor.position):Vector3.zero,
                resultReason=ResultReason,success=Settlement.Success,usedInsurance=Settlement.UsedInsurance,usedFare=Settlement.UsedFareSupport,
                fee=Settlement.Fee,compensation=Settlement.Compensation,returnFare=Settlement.ReturnFare,
                stationLayouts=World.CaptureStationLayouts()};
            PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(save));PlayerPrefs.Save();
            nextProgressSave=Time.unscaledTime+15;
        }
        void Load()
        {
            if(startFreshOnLaunch)return;
            string key=PlayerPrefs.HasKey(SaveKey) ? SaveKey : StandardSaveKey;
            if(!PlayerPrefs.HasKey(key))return;
            try{
                var save=JsonUtility.FromJson<Save>(PlayerPrefs.GetString(key));if(save==null)return;
                Economy.ApplyDelta(Mathf.Max(0,save.cash)-Economy.CurrentEconomyState.CurrentCash);
                Unlocked=Mathf.Clamp(save.unlocked,1,World.settings.deliveries.orders.Length);
                int cap=World.settings.deliveries.maximumUpgradeLevel;
                StaminaLevel=Mathf.Clamp(save.stamina,0,cap);BalanceLevel=Mathf.Clamp(save.balance,0,cap);AgilityLevel=Mathf.Clamp(save.agility,0,cap);
                if(save.version<2)return;
                Insurance=save.insurance;FareSupport=save.fareSupport;
                HasCushionPad=save.cushionPad;HasEnergyGel=save.energyGel;
                int order=Array.FindIndex(World.settings.deliveries.orders,o=>o.id==save.orderId);
                if(order<0){Notice="저장된 배송이 현재 목록에 없습니다. 보유 현금과 서비스는 유지됩니다.";return;}
                SelectedIndex=order;activeRoute=World.settings.deliveries.orders[order];
                // Legacy progress can contain a player or an open gate away from the hub.
                World.RestoreStationLayouts(save.version >= 3 ? save.stationLayouts : null,
                    save.accepted || save.playerStation > 0 ? activeRoute.stops.Select(stop => stop.id) : Array.Empty<string>());
                HasPackage=save.accepted;Order=HasPackage?activeRoute:null;
                pendingProgress=save;
            }catch(ArgumentException){Notice="저장 데이터를 읽지 못했습니다.";}
        }
        void RestoreProgress()
        {
            var save=pendingProgress;pendingProgress=null;if(save==null)return;
            restoringProgress=true;
            try{
                StopIndex=Mathf.Clamp(save.stop,0,Route.stops.Length-1);legStart=Mathf.Clamp(save.leg,0,Route.stops.Length-1);
                transferConcourseVisited=save.transferWalked;destinationReached=save.destinationReached;
                World.EnsureStation(Mathf.Clamp(Mathf.Max(save.playerStation,save.train!=null?save.train.stop:StopIndex),0,Route.stops.Length-1));
                gatePaid=save.paid;outboundPaid=Mathf.Max(0,save.outbound);transferNeeded=save.transfer;
                ResultOpen=save.settled;Bankrupt=save.bankrupt;ResultReason=save.resultReason;
                Settlement=new SliceDeliveryRules.Settlement(save.success,save.usedInsurance,save.usedFare,save.fee,save.compensation,outboundPaid,save.returnFare);
                if(HasPackage&&save.train!=null){
                    if(World.train.side!=save.trainSide){World.Traffic.RetireTrain(World.train);World.facilities.RemoveAll(f=>f==null||f.transform.IsChildOf(World.train.transform));Destroy(World.train.gameObject);World.train=World.CreateTrain(save.trainSide,StopIndex);}
                    World.train.RestoreProgress(save.train);step=(ServiceStep)Mathf.Clamp(save.phase,0,(int)ServiceStep.TransferWait);timer=Mathf.Max(0,save.remaining);
                    if(step==ServiceStep.TransferDeparture)departing=World.train;
                }
                World.player.package.RestoreDurability(save.box,save.cake);previousBox=save.box;previousCake=save.cake;
                World.Transit.SynchronizeRoute();
                if(HasPackage)World.Transit.RegisterPrimary(World.train);
                Metro3DTrain ride=save.riding&&HasPackage?World.train:null;
                // Unity's inline class serialization can materialize an empty
                // object for a null snapshot. Presence must not imply another train.
                if(ride!=null&&save.otherRide!=null&&(save.ridingOther||!string.IsNullOrEmpty(save.otherRide.line))){
                    World.EnsureStation(Mathf.Clamp(save.otherRide.stop,0,Route.stops.Length-1));
                    ride=World.CreateTrain(save.otherRideSide,save.otherRide.stop);ride.RestoreProgress(save.otherRide);
                    World.Transit.RegisterRestoredService(ride,save.otherRideWait);
                }
                World.player.RestoreProgress(ride,save.playerPosition,save.playerRotation,save.playerStamina,save.viewPitch,save.facility,save.facilityAnchor);
                if(save.openGateRemaining>0&&save.openGateStation>=0&&save.openGateStation<World.stations.Count)
                    foreach(var f in World.facilities)if(f!=null&&f.gateDoor!=null&&f.transform.parent==World.stations[save.openGateStation]&&
                        (f.transform.localPosition-save.openGatePosition).sqrMagnitude<.001f){openGate=f;gateTime=Time.time+Mathf.Min(5,save.openGateRemaining);f.gateDoor.Open();break;}
                PhoneOpen=save.phone;World.player.RefreshCargo();Physics.SyncTransforms();
                Notice=ResultOpen?(Bankrupt?"파산 · 새 게임으로 복귀":Settlement.Success?"배송 완료":"배송 실패"):
                    HasPackage?"진행 중인 배송을 이어갑니다.":"배송을 선택하세요.";
                SetCursor();
            }finally{restoringProgress=false;nextProgressSave=Time.unscaledTime+15;}
        }
        void ResetBankruptProgress()
        {
            Bankrupt=true;Economy.ResetToInitial();Unlocked=1;StaminaLevel=BalanceLevel=AgilityLevel=0;Insurance=FareSupport=false;
            HasCushionPad=HasEnergyGel=false;
        }
    }
}
