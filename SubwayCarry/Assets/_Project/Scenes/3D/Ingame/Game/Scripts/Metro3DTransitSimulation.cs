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
        sealed class Service
        {
            public Metro3DTrain train;
            public Phase phase;
            public float timer;
            public bool departureWarning;
            public BoardingRecovery boardingRecovery;
            public int from,to;
            public IEnumerator<int> passengerSeed;
        }
        internal struct BoardingRecovery
        {
            internal bool observedAlighting, evaluated;
            internal float nextCheck, holdUntil;
        }
        readonly List<Service> services=new List<Service>(8);
        readonly float[] nextDeparture=new float[2];
        readonly Plane[] viewPlanes=new Plane[6];
        int revision=-1;
        float nextStationBuild;
        bool farServicePending;
        public int Arrivals {get;private set;}
        public int ServicesCreated {get;private set;}

        void Update()
        {
            if(world==null||world.session==null||world.session.Route==null||world.session.SimulationPaused)return;
            for(int i=world.Trains.Count-1;i>=0;i--)if(world.Trains[i]==null)world.Trains.RemoveAt(i);
            if(revision!=world.RouteRevision) {ResetRoute();return;}
            bool seededCar=false;
            for(int i=services.Count-1;i>=0;i--) {
                var s=services[i];
                if(s.train==null){s.passengerSeed?.Dispose();services.RemoveAt(i);continue;}
                if(!seededCar&&s.passengerSeed!=null) {
                    seededCar=s.passengerSeed.MoveNext();
                    if(!seededCar){s.passengerSeed.Dispose();s.passengerSeed=null;}
                }
                if(s.train==world.train&&world.session.HasPackage) {s.train.Autonomous=false;s.to=s.train.CurrentStop;s.from=s.train.Moving?Mathf.Max(0,s.to-1):s.to;continue;}
                if(!s.train.Autonomous) {s.train.Autonomous=true;s.phase=s.train.Moving?Phase.Travel:Phase.Opening;s.from=s.to=s.train.CurrentStop;s.timer=world.settings.dwellSeconds;}
                Tick(s);
            }
            if(world.train!=null&&!world.session.HasPackage&&!Known(world.train)) Add(world.train,1,0,true);
            if(seededCar)return;
            for(int direction=-1;direction<=1;direction+=2) {
                int lane=direction>0?1:0,count=0;
                foreach(var s in services)if(s.train!=null&&s.train.TravelDirection==direction)count++;
                int limit=world.settings.ambientTrainsPerDirection-(direction<0&&farServicePending?1:0);
                if(count>=limit||Time.time<nextDeparture[lane])continue;
                int stop=0;
                int side=PlatformSideAt(stop,direction);
                if(!Free(null,side,stop,stop))continue;
                Add(world.CreateTrain(side,stop),direction,stop,true);
                nextDeparture[lane]=Time.time+Mathf.Max(35,world.settings.ambientTrainHeadway);
                return;
            }
            PrepareRoute();
        }
        bool Known(Metro3DTrain train) {foreach(var s in services)if(s.train==train)return true;return false;}
        internal string LineAt(int stop,int direction)=>world.session.Route.stops[Mathf.Clamp(direction>0?stop+1:stop,1,world.session.Route.stops.Length-1)].line;
        internal int PlatformSideAt(int stop,int direction)
        {
            int edge=Mathf.Clamp(direction>0?stop+1:stop,1,world.session.Route.stops.Length-1),side=world.ServiceSide;
            for(int i=1;i<edge;i++)if(world.session.Route.stops[i].transfer)side=-side;
            return side*(direction<0?-1:1);
        }
        internal bool CanAdvancePrimary(int next)
        {
            Service own=null;foreach(var s in services)if(s.train==world.train){own=s;break;}
            return Free(own,world.train.side,world.train.CurrentStop,next);
        }
        internal bool CanIntroduceService(int side,int stop,Metro3DTrain retiring)
        {
            foreach(var s in services)if(s.train!=null&&s.train!=retiring&&s.train.side==side&&
                (s.to==stop||s.from==stop))return false;
            return true;
        }
        internal void RegisterPrimary(Metro3DTrain train)
        {
            if(Known(train))return;
            train.ConfigureService(1,train.CurrentStop);
            services.Add(new Service{train=train,from=train.CurrentStop,to=train.CurrentStop});
        }
        internal void RegisterRestoredService(Metro3DTrain train,float wait)
        {
            if(Known(train))return;
            train.Autonomous=true;
            services.Add(new Service{train=train,from=train.Moving?Mathf.Clamp(train.CurrentStop-train.TravelDirection,0,world.session.Route.stops.Length-1):train.CurrentStop,
                to=train.CurrentStop,phase=train.Moving?Phase.Travel:train.DoorsOpen?Phase.Dwell:Phase.Opening,timer=Mathf.Max(0,wait)});
        }
        internal void SynchronizeRoute(){if(revision!=world.RouteRevision)ResetRoute();}
        // Adjacent station records describe the line on which each leg arrives.
        // A transfer station can be served by either line; a train cannot simply
        // continue into the next line because its array index increased.
        bool HasRailLeg(Metro3DTrain train,int next)
        {
            var stops=world.session.Route.stops;
            int arrival=Mathf.Max(train.CurrentStop,next);
            return arrival>0&&arrival<stops.Length&&stops[arrival].line==train.ServiceLine;
        }
        internal bool EndsLine(Metro3DTrain train)
        {
            int next=train.CurrentStop+train.TravelDirection;
            return next>=0&&next<world.session.Route.stops.Length&&!HasRailLeg(train,next);
        }
        void ResetRoute()
        {
            // EnsureRoute has already released old-route passengers; do not rebuild saved scenes.
            foreach(var s in services) {
                s.passengerSeed?.Dispose();
                if(s.train!=null&&s.train!=world.train)Retire(s.train);
            }
            services.Clear();revision=world.RouteRevision;
            // The hub can run both directions while the rest of the route is built.
            nextStationBuild=Time.time+.45f;
            farServicePending=world.settings.ambientTrainsPerDirection>1;
            if(world.train!=null) {
                if(!world.session.HasPackage)Add(world.train,1,0,true);
                else {world.train.ConfigureService(1,world.train.CurrentStop);services.Add(new Service{train=world.train,from=world.train.CurrentStop,to=world.train.CurrentStop});}
            }
            nextDeparture[1]=Time.time+Mathf.Max(35,world.settings.ambientTrainHeadway);
            nextDeparture[0]=Time.time+.18f;
        }
        void PrepareRoute()
        {
            if(Time.time<nextStationBuild)return;
            int end=world.session.Route.stops.Length-1;
            if(world.stations.Count<=end) {
                if(world.PrepareNextStation())nextStationBuild=Time.time+.10f;
                return;
            }
            int side=PlatformSideAt(end,-1);
            if(!farServicePending||!Free(null,side,end,end))return;
            farServicePending=false;
            Add(world.CreateTrain(side,end),-1,end,true);
        }
        void Add(Metro3DTrain train,int direction,int stop,bool approaching)
        {
            if(!world.Trains.Contains(train))world.Trains.Add(train);
            train.SetServiceLine(LineAt(stop,direction));train.ConfigureService(direction,stop);train.Autonomous=true;train.BoardingWindow=false;
            if(direction>0&&world.train==null)world.train=train;
            Vector3 p=train.transform.localPosition;
            p.z=stop*Metro3DWorld.StationSpacing+(direction>0?-Metro3DTrain.FloorEnd-30:Metro3DTrain.FloorEnd+30);
            train.transform.localPosition=p;
            train.MoveTo(stop,world.settings.approachSeconds);
            var seed=world.Traffic.SeedTrainCars(train,stop,stop==0);
            services.Add(new Service{train=train,phase=Phase.Approach,from=stop,to=stop,passengerSeed=seed});ServicesCreated++;
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
            if(s.from==0&&(s.phase==Phase.Travel||s.phase==Phase.Departing)&&CanParkAfterHub(train))
                world.Traffic.ParkHubRiders(train,s.phase==Phase.Departing);
            switch(s.phase) {
                case Phase.Approach:
                case Phase.Travel:
                    if(!train.Moving&&s.passengerSeed==null) {
                        s.from=s.to;
                        world.Traffic.ArriveWithBackgroundRiders(train);
                        train.SetDoors(true);s.phase=Phase.Opening;Arrivals++;
                    }break;
                case Phase.Opening:
                    if(train.DoorsOpen) {
                        s.phase=Phase.Dwell;s.timer=world.settings.dwellSeconds;s.departureWarning=false;
                        s.boardingRecovery=default;
                    }break;
                case Phase.Dwell:
                    UpdateBoardingWindow(s);
                    if(!s.departureWarning&&s.timer<=5){s.departureWarning=true;world.session.TrainCue(train,false);}
                    if(s.timer>0||world.Traffic.HasDoorCrossing(train)||world.Traffic.HasAlighters(train))break;
                    train.BoardingWindow=false;train.SetDoors(false);s.phase=Phase.Closing;break;
                case Phase.Closing:
                    if(train.OpenAmount>.001f)break;
                    int next=train.CurrentStop+train.TravelDirection;
                    if(next<0||next>=world.session.Route.stops.Length||!HasRailLeg(train,next)) {
                        bool continuesBeyondHub=train.CurrentStop==0&&train.TravelDirection<0;
                        if(world.player.Riding==train||!continuesBeyondHub&&world.Traffic.HasPassengers(train.transform)) {
                            train.SetDoors(true);s.phase=Phase.Opening;break;
                        }
                        train.DepartOutOfRoute(world.settings.approachSeconds);s.phase=Phase.Departing;break;
                    }
                    if(next>=world.stations.Count||!Free(s,train.side,train.CurrentStop,next))break;
                    s.from=train.CurrentStop;s.to=next;train.MoveTo(next,world.settings.travelSeconds);s.phase=Phase.Travel;break;
                case Phase.Departing:
                    if(!train.Moving) {
                        if(world.Traffic.HasPassengers(train.transform))break;
                        bool primary=train==world.train;
                        Retire(train);services.Remove(s);
                        if(primary)world.train=null;
                    }break;
            }
            // Rebind the primary pointer only to the normal-direction service.
            if(world.train==null)foreach(var candidate in services)if(candidate.train!=null&&candidate.train.TravelDirection>0){world.train=candidate.train;break;}
        }
        void UpdateBoardingWindow(Service service)
        {
            RecoverBoardingTime(service.train,ref service.timer,ref service.boardingRecovery,service.departureWarning);
            service.train.BoardingWindow=service.timer>7;
        }
        internal void RecoverBoardingTime(Metro3DTrain train,ref float remaining,ref BoardingRecovery recovery,bool warningPlayed)
        {
            const float reserve=7f;
            if(warningPlayed)return;
            if(recovery.holdUntil>0) {
                float timeLeft=recovery.holdUntil-Time.time;
                if(timeLeft<=0) {recovery.holdUntil=0;return;}
                if(Time.time>=recovery.nextCheck) {
                    recovery.nextCheck=Time.time+.35f;
                    if(!world.Traffic.HasWaitingBoarders(train)) {recovery.holdUntil=0;return;}
                    if(!world.Traffic.HasAlighters(train)) {
                        remaining=Mathf.Max(remaining,reserve+Mathf.Min(4f,timeLeft));
                        recovery.holdUntil=0;return;
                    }
                }
                remaining=Mathf.Max(remaining,reserve+.07f);
                return;
            }
            if(recovery.evaluated||remaining<=5f||remaining>=12f)return;
            if(Time.time<recovery.nextCheck&&(!recovery.observedAlighting||remaining>reserve))return;
            recovery.nextCheck=Time.time+.35f;
            bool leaving=world.Traffic.HasAlighters(train);
            recovery.observedAlighting|=leaving;
            if(!recovery.observedAlighting||leaving&&remaining>reserve)return;

            recovery.evaluated=true;
            if(!world.Traffic.HasWaitingBoarders(train))return;
            if(leaving) {
                // The train is still unloading. Other doors may admit only
                // while there is actual room and before the departure cue.
                recovery.holdUntil=Time.time+13f;
                remaining=reserve+.07f;
            } else remaining=Mathf.Max(remaining,reserve+6f);
        }
        bool CanParkAfterHub(Metro3DTrain train)
        {
            var player=world.player;
            if(player==null||player.Riding==train||world.session.HasPackage&&train==world.train||
                !world.Traffic.HasPassengers(train.transform))return false;
            float z=train.transform.localPosition.z;
            if(train.TravelDirection>0?z+Metro3DTrain.FloorStart<=Metro3DWorld.PlatformLength+4:z+Metro3DTrain.FloorEnd>=-4)
                return false;
            if(train.TravelDirection>0&&world.StationAt(player.transform.position)!=0)return false;
            var camera=world.Observer!=null?world.Observer.View:player.view;
            if(camera==null||!camera.isActiveAndEnabled)return true;
            float length=Metro3DTrain.FloorEnd-Metro3DTrain.FloorStart;
            var bounds=new Bounds(train.transform.TransformPoint(new Vector3(0,1.2f,Metro3DTrain.FloorStart+length*.5f)),
                new Vector3(3.3f,2.8f,length+1));
            GeometryUtility.CalculateFrustumPlanes(camera,viewPlanes);
            return !GeometryUtility.TestPlanesAABB(viewPlanes,bounds);
        }
        void Retire(Metro3DTrain train)
        {
            world.Traffic.RetireTrain(train);
            world.facilities.RemoveAll(f=>f==null||f.transform.IsChildOf(train.transform));
            Destroy(train.gameObject);
        }
        internal float TimeToBoarding(Metro3DPassenger actor,Metro3DTrain excluded=null,bool includeScheduled=true)
        {
            int stop=actor.stationIndex,direction=actor.Journey.TravelDirection;
            float estimate=float.PositiveInfinity;
            foreach(var service in services) {
                var train=service.train;
                if(train==null||train==excluded||train.TravelDirection!=direction||train.side!=actor.Journey.TrainSide||train.ServiceLine!=actor.Journey.NextLine||
                    service.phase==Phase.Departing)continue;
                int stopsAhead=(stop-service.to)*direction;
                if(stopsAhead<0)continue;
                float remaining=train.MovementRemaining;
                if(stopsAhead==0) {
                    if(train.CanBoard)return 0;
                    if(service.phase==Phase.Dwell||service.phase==Phase.Closing)continue;
                } else if(!train.Moving)remaining=Mathf.Max(0,service.timer)-world.settings.dwellSeconds;
                remaining+=stopsAhead*(world.settings.travelSeconds+world.settings.dwellSeconds);
                estimate=Mathf.Min(estimate,Mathf.Max(0,remaining)+world.settings.doorSeconds);
            }
            if(stop==0&&includeScheduled) {
                float next=Mathf.Max(0,nextDeparture[direction>0?1:0]-Time.time);
                estimate=Mathf.Min(estimate,next+world.settings.approachSeconds+world.settings.doorSeconds);
            }
            return estimate;
        }

        public Metro3DTrain BoardingTrain(Metro3DPassenger actor)
        {
            if(actor.inTrain||!actor.Journey.CanBoard)return null;
            var companion=actor.HasCompanion?actor.Companion:null;
            if(actor.WaitForCompanion())return null;
            foreach(var train in world.Trains)if(train!=null&&(train.CanBoard||actor.CanFinishGroupBoarding(train))&&train.CurrentStop==actor.stationIndex&&
                train.TravelDirection==actor.Journey.TravelDirection&&train.side==actor.Journey.TrainSide&&train.ServiceLine==actor.Journey.NextLine&&
                (companion==null||!companion.inTrain||companion.Riding==train||actor.stationIndex!=companion.Riding.CurrentStop))return train;
            return null;
        }
        internal float BoardingSeconds(Metro3DTrain train)
        {
            if(train==world.train&&world.session.HasPackage)return Mathf.Max(0,world.session.Timer-7);
            foreach(var service in services)if(service.train==train&&service.phase==Phase.Dwell)return Mathf.Max(0,service.timer-7);
            return 0;
        }
        internal void HandOverBoardedService(Metro3DTrain train)
        {
            foreach(var s in services) {
                if(s.train!=train||s.phase!=Phase.Dwell)continue;
                world.train=train;
                world.session.ResumeService(train,s.timer);
                world.session.ResumeBoardingRecovery(s.boardingRecovery,s.departureWarning);
                train.Autonomous=false;
                return;
            }
        }
        public void HandOverPrimary()
        {
            if(world.train==null)Add(world.CreateTrain(world.ServiceSide,0),1,0,true);
            foreach(var s in services)if(s.train==world.train) {
                world.session.ResumeService(s.train,s.timer,s.phase==Phase.Closing);
                if(s.phase==Phase.Dwell)world.session.ResumeBoardingRecovery(s.boardingRecovery,s.departureWarning);
                s.train.Autonomous=false;return;
            }
        }
    }
}
