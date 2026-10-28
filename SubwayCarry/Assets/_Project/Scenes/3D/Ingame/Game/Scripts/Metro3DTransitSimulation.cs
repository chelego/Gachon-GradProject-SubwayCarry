using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Timetabled services reuse the real train motor and passenger journey/actions.
    // The delivery session may own its primary train; other services never wait for the player.
    [DefaultExecutionOrder(-80)]
    public sealed class Metro3DTransitSimulation : MonoBehaviour
    {
        public Metro3DWorld world;
        enum Phase { Approach, Opening, Dwell, Closing, Travel, Departing }
        sealed class Service {public Metro3DTrain train;public Phase phase;public float timer;public int from,to;}
        readonly List<Service> services=new List<Service>(8);
        readonly float[] nextDeparture=new float[2];
        int revision=-1;
        public int Arrivals {get;private set;}
        public int ServicesCreated {get;private set;}

        void Update()
        {
            if(world==null||world.session==null||world.session.Route==null||world.session.SimulationPaused)return;
            for(int i=world.Trains.Count-1;i>=0;i--)if(world.Trains[i]==null)world.Trains.RemoveAt(i);
            if(revision!=world.RouteRevision) ResetRoute();
            for(int i=services.Count-1;i>=0;i--) {
                var s=services[i];if(s.train==null){services.RemoveAt(i);continue;}
                if(s.train==world.train&&world.session.HasPackage) {s.train.Autonomous=false;s.to=s.train.CurrentStop;s.from=s.train.Moving?Mathf.Max(0,s.to-1):s.to;continue;}
                if(!s.train.Autonomous) {s.train.Autonomous=true;s.phase=s.train.Moving?Phase.Travel:Phase.Opening;s.from=s.to=s.train.CurrentStop;s.timer=world.settings.dwellSeconds;}
                Tick(s);
            }
            if(world.train!=null&&!world.session.HasPackage&&!Known(world.train)) Add(world.train,1,0,true);
            for(int direction=-1;direction<=1;direction+=2) {
                int lane=direction>0?1:0,count=0;
                foreach(var s in services)if(s.train!=null&&s.train.TravelDirection==direction)count++;
                if(count>=world.settings.ambientTrainsPerDirection||Time.time<nextDeparture[lane])continue;
                int stop=direction>0?0:world.session.Route.stops.Length-1;
                if(!Free(null,world.ServiceSide*direction,stop,stop))continue;
                Add(world.CreateTrain(world.ServiceSide*direction,stop),direction,stop,true);
                nextDeparture[lane]=Time.time+Mathf.Max(35,world.settings.ambientTrainHeadway);
            }
        }
        bool Known(Metro3DTrain train) {foreach(var s in services)if(s.train==train)return true;return false;}
        void ResetRoute()
        {
            // EnsureRoute has already released old-route passengers; do not rebuild saved scenes.
            foreach(var s in services)if(s.train!=null&&s.train!=world.train) Retire(s.train);
            services.Clear();revision=world.RouteRevision;
            world.EnsureStation(world.session.Route.stops.Length-1);
            Physics.SyncTransforms();
            if(world.train!=null) {
                if(!world.session.HasPackage)Add(world.train,1,0,true);
                else {world.train.ConfigureService(1,world.train.CurrentStop);services.Add(new Service{train=world.train,from=world.train.CurrentStop,to=world.train.CurrentStop});}
            }
            nextDeparture[1]=Time.time+Mathf.Max(35,world.settings.ambientTrainHeadway);
            nextDeparture[0]=Time.time;
        }
        void Add(Metro3DTrain train,int direction,int stop,bool approaching)
        {
            if(!world.Trains.Contains(train))world.Trains.Add(train);
            train.ConfigureService(direction,stop);train.Autonomous=true;train.BoardingWindow=false;
            if(direction>0&&world.train==null)world.train=train;
            Vector3 p=train.transform.localPosition;
            p.z=stop*Metro3DWorld.StationSpacing+(direction>0?-Metro3DTrain.FloorEnd-30:Metro3DTrain.FloorEnd+30);
            train.transform.localPosition=p;
            world.Traffic.SeedTrain(train,stop);
            train.MoveTo(stop,world.settings.approachSeconds);
            services.Add(new Service{train=train,phase=Phase.Approach,from=stop,to=stop});ServicesCreated++;
        }
        bool Free(Service self,int side,int from,int to)
        {
            foreach(var s in services) {
                if(s==self||s.train==null||s.train.side!=side)continue;
                if(s.to==to||s.from==to)return false;
                if(s.train.Moving&&Mathf.Min(from,to)<=Mathf.Max(s.from,s.to)&&Mathf.Max(from,to)>=Mathf.Min(s.from,s.to))return false;
            }
            // The session-owned primary also participates in track occupancy.
            var main=world.train;
            if(main!=null&&world.session.HasPackage&&(self==null||self.train!=main)&&main.side==side) {
                if(main.CurrentStop==to||Mathf.Abs(main.transform.localPosition.z-to*Metro3DWorld.StationSpacing)<Metro3DTrain.FloorEnd+12)return false;
            }
            return true;
        }
        void Tick(Service s)
        {
            var train=s.train;s.timer-=Time.deltaTime;
            switch(s.phase) {
                case Phase.Approach:
                case Phase.Travel:
                    if(!train.Moving){s.from=s.to;train.SetDoors(true);s.phase=Phase.Opening;Arrivals++;}break;
                case Phase.Opening:
                    if(train.DoorsOpen){s.phase=Phase.Dwell;s.timer=world.settings.dwellSeconds;}break;
                case Phase.Dwell:
                    train.BoardingWindow=s.timer>7;
                    if(s.timer>0||world.Traffic.HasDoorCrossing(train)||world.Traffic.HasAlighters(train))break;
                    train.BoardingWindow=false;train.SetDoors(false);s.phase=Phase.Closing;break;
                case Phase.Closing:
                    if(train.OpenAmount>.001f)break;
                    int next=train.CurrentStop+train.TravelDirection;
                    if(next<0||next>=world.session.Route.stops.Length) {
                        // No disappearing occupied trains or camera-dependent recycling.
                        if(world.Traffic.HasPassengers(train.transform)||world.player.Riding==train){train.SetDoors(true);s.phase=Phase.Opening;break;}
                        train.DepartOutOfRoute(world.settings.approachSeconds);s.phase=Phase.Departing;break;
                    }
                    if(!Free(s,train.side,train.CurrentStop,next))break;
                    s.from=train.CurrentStop;s.to=next;train.MoveTo(next,world.settings.travelSeconds);s.phase=Phase.Travel;break;
                case Phase.Departing:
                    if(!train.Moving) {
                        bool primary=train==world.train;
                        Retire(train);services.Remove(s);
                        if(primary)world.train=null;
                    }break;
            }
            // Rebind the primary pointer only to the normal-direction service.
            if(world.train==null)foreach(var candidate in services)if(candidate.train!=null&&candidate.train.TravelDirection>0){world.train=candidate.train;break;}
        }
        void Retire(Metro3DTrain train)
        {
            world.Traffic.RetireTrain(train);
            world.facilities.RemoveAll(f=>f==null||f.transform.IsChildOf(train.transform));
            Destroy(train.gameObject);
        }
        public Metro3DTrain BoardingTrain(Metro3DPassenger actor)
        {
            foreach(var train in world.Trains)if(train!=null&&train.CanBoard&&train.CurrentStop==actor.stationIndex&&train.TravelDirection==actor.Journey.TravelDirection&&train.side==actor.Journey.TrainSide)return train;
            return null;
        }
        public void HandOverPrimary()
        {
            if(world.train==null)Add(world.CreateTrain(world.ServiceSide,0),1,0,true);
            foreach(var s in services)if(s.train==world.train) {
                world.session.ResumeService(s.train,s.timer);s.train.Autonomous=false;return;
            }
        }
    }
}
