using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed partial class Metro3DSession
    {
        int legStart;
        bool transferConcourseVisited,destinationReached;
        public int DeliveryPlatformSide
        {
            get{int changes=0;for(int i=1;i<=legStart&&i<Route.stops.Length;i++)if(Route.stops[i].transfer)changes++;return World.ServiceSide*(changes%2==0?1:-1);}
        }
        public string DeliveryLine=>Route.stops[Mathf.Min(legStart+1,Route.stops.Length-1)].line;
        internal int LegStart=>legStart;
        public bool TransferWalkingRequired=>HasPackage&&legStart>0&&StopIndex==legStart&&!transferConcourseVisited;
        public bool DestinationReached=>destinationReached;
        internal void PlayerAlighted(Metro3DTrain previous)
        {
            if(!HasPackage||previous==null||!IsDeliveryDirection(previous)||previous.Moving)return;
            if(previous.CurrentStop==Route.stops.Length-1)destinationReached=true;
            SaveProgress();
        }
        void ObserveTransferProgress()
        {
            if(!HasPackage||World.player.Riding!=null||World.player.transform.position.y<=World.settings.departureFloor-.35f)return;
            int station=World.StationAt(World.player.transform.position);
            if(TransferWalkingRequired&&station==legStart){
                transferConcourseVisited=true;Notice=DeliveryLine+" · 반대편 승강장으로 내려가세요.";SaveProgress();
            }
        }
    }
}
