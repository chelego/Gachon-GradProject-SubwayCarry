using System.Text;
using UnityEngine;
using Unity.Profiling;

namespace SubwayCarry.Gameplay3D
{
    internal enum MetroDecision { Rest, Wait, Prepare, Door, NextTrain, Courtesy, Resume, HeardPhone, SawCarry, GuardianResponse, CheckBag }
    internal enum MetroDecisionReason { TimeMargin, DestinationNear, TravelAndCongestion, CannotReachInTime, Crowded, VisiblePackage, PathClear, AudibleNearby, VisibleChild, VisibleCarrying, PersonalRoutine, ActivityChanged, EventFinished, ClosePassage }
    public sealed partial class Metro3DPassenger
    {
        internal struct DecisionRecord
        {
            public MetroDecision choice;public MetroDecisionReason reason;
            public float at,score,alternative,margin,next;public int selected,other;
        }
        static readonly ProfilerMarker decisionMarker=new ProfilerMarker("SubwayCarry.Passenger.Decision");
        readonly DecisionRecord[] decisions=new DecisionRecord[6];
        int decisionWrite,decisionCount;
        float nextBoardChoice,boardCommitUntil,nextSeatLook,nextCourtesySense,courtesyHoldUntil,courtesyCooldown,nextStandingCourtesy;
        Metro3DTrain deferredService;
        Metro3DTrain evaluatedService;
        int evaluatedDoor;
        bool evaluatedBoard;
        Vector3 perceivedCarryPoint;
        bool yieldingToPackage;
        public int DecisionCount=>decisionCount;
        internal DecisionRecord LastDecision=>decisions[(decisionWrite+decisions.Length-1)%decisions.Length];
        internal void RecordDecision(MetroDecision choice,MetroDecisionReason reason,float score=0,float alternative=0,float margin=0,int selected=-1,int other=-1,float next=0)
        {
            if(decisionCount>0){var last=LastDecision;if(last.choice==choice&&last.reason==reason&&last.selected==selected&&Time.time-last.at<4)return;}
            decisions[decisionWrite]=new DecisionRecord{choice=choice,reason=reason,score=score,alternative=alternative,margin=margin,selected=selected,other=other,at=Time.time,next=next};
            decisionWrite=(decisionWrite+1)%decisions.Length;decisionCount=Mathf.Min(decisionCount+1,decisions.Length);
        }
        internal void AppendDecisionDetails(StringBuilder text)
        {
            text.Append("\n최근 판단 · 규칙/비용 비교 (근거리 회피 정책과 별도)");
            for(int i=0;i<Mathf.Min(3,decisionCount);i++){
                var d=decisions[(decisionWrite+decisions.Length-1-i)%decisions.Length];
                text.Append('\n').Append((Time.time-d.at).ToString("F1")).Append("초 전 · ").Append(DecisionName(d.choice)).Append(" / ").Append(ReasonName(d.reason));
                if(d.choice==MetroDecision.Door||d.choice==MetroDecision.NextTrain)
                    text.Append(" · 문 ").Append(d.selected>=0?Metro3DTrain.DoorNumber(d.selected):"없음").Append(" 비용 ").Append(float.IsInfinity(d.score)?"접근 불가":d.score.ToString("F1"))
                        .Append(" / 대안 ").Append(d.other>=0?Metro3DTrain.DoorNumber(d.other):"다음 차").Append(' ').Append(d.alternative.ToString("F1")).Append(" · 잔여 ").Append(d.margin.ToString("F1")).Append('s');
                else if(d.reason==MetroDecisionReason.TimeMargin&&(d.choice==MetroDecision.Rest||d.choice==MetroDecision.Wait))text.Append(d.selected==1?" · 기대기":" · 좌석").Append(" · 선호 ").Append(d.score.ToString("F2")).Append(" / 기준 ").Append(d.alternative.ToString("F2")).Append(" · 여유 ").Append(d.margin.ToString("F1")).Append('s');
                text.Append(" · 재평가 ").Append(d.next>0?Mathf.Max(0,d.next-Time.time).ToString("F1")+"s":"상황 변화");
            }
        }
        static string DecisionName(MetroDecision d)=>d switch{
            MetroDecision.Rest=>"휴식",MetroDecision.Wait=>"대기",MetroDecision.Prepare=>"이동 준비",MetroDecision.Door=>"탑승 문 선택",
            MetroDecision.NextTrain=>"다음 열차",MetroDecision.Courtesy=>"운반자 양보",MetroDecision.Resume=>"진행 재개",
            MetroDecision.HeardPhone=>"알림 확인",MetroDecision.SawCarry=>"운반 흉내",MetroDecision.GuardianResponse=>"아이 확인",_=>"가방 확인"};
        static string ReasonName(MetroDecisionReason r)=>r switch{
            MetroDecisionReason.TimeMargin=>"시간·선호 비교",MetroDecisionReason.DestinationNear=>"목적역 접근",
            MetroDecisionReason.TravelAndCongestion=>"접근 거리·문 혼잡",MetroDecisionReason.CannotReachInTime=>"남은 시간 부족",
            MetroDecisionReason.Crowded=>"혼잡 비용",MetroDecisionReason.VisiblePackage=>"시야 내 운반자",
            MetroDecisionReason.PathClear=>"진로 확보",MetroDecisionReason.AudibleNearby=>"가까운 알림 소리",MetroDecisionReason.ClosePassage=>"바로 앞 근접 통행",
            MetroDecisionReason.VisibleChild=>"시야 내 아이",MetroDecisionReason.VisibleCarrying=>"시야 내 상자 운반",MetroDecisionReason.ActivityChanged=>"시설/탑승 행동 전환",MetroDecisionReason.EventFinished=>"짧은 행동 종료",_=>"개인 소지품 점검"};
        void ResetDecisions()
        {
            decisionWrite=decisionCount=0;deferredService=null;nextBoardChoice=boardCommitUntil=0;
            evaluatedService=null;evaluatedDoor=-1;evaluatedBoard=false;
            nextSeatLook=Time.time+4+(index%9)*.29f;nextCourtesySense=Time.time+(index%7)*.09f;
            courtesyHoldUntil=courtesyCooldown=0;nextStandingCourtesy=Time.time+(index%7)*.09f;yieldingToPackage=false;
        }
        bool TryGivePlayerRoom()
        {
            if(inTrain||seat!=null||!Journey.CanBoard||PortalCommitted||QueueActive||Journey.OnStairs||
                CompanionService!=null||Time.time<nextStandingCourtesy)return false;
            nextStandingCourtesy=Time.time+.6f+(index%4)*.07f;
            var p=world.player;
            if(p==null||p.Riding!=null||p.cargo==null||!p.cargo.gameObject.activeInHierarchy)return TryGivePassengerRoom();
            Vector3 gap=transform.position-p.transform.position;float height=gap.y;gap.y=0;
            Vector3 intent=p.Velocity;intent.y=0;
            if(Mathf.Abs(height)>.6f||gap.sqrMagnitude>4||intent.sqrMagnitude<.04f)return TryGivePassengerRoom();
            intent.Normalize();float ahead=Vector3.Dot(gap,intent);
            if(ahead<.1f||(gap-intent*ahead).sqrMagnitude>.72f*.72f)return TryGivePassengerRoom();
            bool seen=crowd.CanPerceive(this,p.cargo.position,p.transform,true);
            bool close=gap.sqrMagnitude<.75f*.75f&&crowd.CanPerceive(this,p.cargo.position,p.transform,false);
            if((!seen||Journey.Personality.Courtesy<=.25f)&&!close)return false;
            if(!world.Traffic.TryGivePlatformRoom(this,out var point))return false;
            nextStandingCourtesy=Time.time+4;perceivedCarryPoint=p.cargo.position;
            yieldingToPackage=true;courtesyHoldUntil=Time.time+.6f;courtesyCooldown=Time.time+3;
            RecordDecision(MetroDecision.Courtesy,seen?MetroDecisionReason.VisiblePackage:MetroDecisionReason.ClosePassage,next:nextStandingCourtesy);
            WalkJourney(point,true);return true;
        }
        bool TryGivePassengerRoom()
        {
            if(world.Traffic.SupportFor(this)!=null||!crowd.HasPassingPassenger(this)||
                !world.Traffic.TryGivePlatformRoom(this,out var spot))return false;
            nextStandingCourtesy=Time.time+3.5f;
            RecordDecision(MetroDecision.Courtesy,MetroDecisionReason.ClosePassage,next:nextStandingCourtesy);
            WalkJourney(spot,false);
            return true;
        }
        // Costs are seconds plus a personality-weighted comfort penalty. Only
        // feasible, visible platform approaches compete; Motor still owns movement.
        internal bool ChooseBoardingDoor(Metro3DTrain service,out int chosen)
        {
            chosen=door;
            if(service==deferredService)return false;
            if(CompanionService==service)return true;
            if(state==Activity.Board&&(waypoint>0||Time.time<nextBoardChoice||Time.time<boardCommitUntil||
                service.CanBoard&&FlatDistance(transform.position,service.DoorPoint(door,true))<2.3f))return true;
            if(evaluatedService==service&&Time.time<nextBoardChoice){chosen=evaluatedDoor;return evaluatedBoard;}
            using(decisionMarker.Auto()){
                evaluatedService=service;evaluatedBoard=false;evaluatedDoor=-1;
                nextBoardChoice=Time.time+1.1f+(index%5)*.13f;
                float remaining=world.Transit.BoardingSeconds(service);
                if(remaining<=0) {
                    // Doors can become ready before this service publishes its
                    // dwell timer. Retry the open train before deferring it.
                    nextBoardChoice=Time.time+.25f;
                    return false;
                }
                float urgency=BoardWindowUrgency(remaining);
                var temperament=Journey.Personality;
                float initiative=BoardInitiative;
                float speed=Mathf.Max(.55f,world.settings.npcSpeed*Journey.SpeedScale);
                float best=float.PositiveInfinity,second=float.PositiveInfinity;int other=-1;
                int nearest=service.NearestDoor(transform.position);chosen=-1;
                bool hasStagedDoor=world.Traffic.StagedPlatformDoor(this,out int stagedDoor,out _);
                if(hasStagedDoor&&state!=Activity.Board&&remaining>=9f) {
                    Vector3 opening=service.DoorPoint(stagedDoor,true);
                    float walk=FlatDistance(transform.position,opening)/speed;
                    if(walk<4.4f&&walk+5f<remaining&&crowd.CanPlanBoardingApproach(this,opening)) {
                        // Keep the line we walked to while the train is arriving.
                        // A short alighting delay alone is not a reason to change doors.
                        chosen=evaluatedDoor=stagedDoor;evaluatedBoard=true;
                        boardCommitUntil=Time.time+6f;
                        RecordDecision(MetroDecision.Door,MetroDecisionReason.TravelAndCongestion,walk,
                            margin:remaining,selected:chosen,next:boardCommitUntil);
                        return true;
                    }
                }
                for(int i=Mathf.Max(0,nearest-2);i<=Mathf.Min(Metro3DTrain.DoorZ.Length-1,nearest+2);i++){
                    Vector3 point=service.DoorPoint(i,true);
                    float walk=FlatDistance(transform.position,point)/speed;
                    if(walk+1.2f>remaining||!crowd.CanPlanBoardingApproach(this,point))continue;
                    if(HasCompanion&&((point-transform.position).sqrMagnitude>16||(point-Companion.transform.position).sqrMagnitude>16))continue;
                    world.Traffic.BoardingLoad(this,service,i,out int queued,out int alighting,out float density);
                    var sensed=crowd.ObserveDoor(service,i);
                    float room=Mathf.Max(sensed.leftRoom,sensed.rightRoom);
                    float chance=urgency*initiative*Mathf.InverseLerp(1.7f,.45f,walk)
                        *Mathf.Lerp(.28f,1f,room)*(1-sensed.pressure*.52f);
                    // Queue depth is a forecast. A nearby opening with space
                    // ahead can move as a group and may be worth attempting.
                    float crossing=1.2f+queued*Mathf.Lerp(.65f,.22f,chance)
                        +alighting*.8f+density*Mathf.Lerp(3f,1.2f,chance);
                    float needed=walk+crossing+(HasCompanion?2.5f:0);
                    if(needed>remaining)continue;
                    float cost=needed+density*(1-Mathf.Clamp01(temperament.CrowdTolerance))*Mathf.Lerp(10f,3f,chance);
                    if(state==Activity.Board&&i==door)cost-=2.5f;
                    if(hasStagedDoor&&i==stagedDoor)cost-=1.8f;
                    if(cost<best){second=best;other=chosen;best=cost;chosen=i;}
                    else if(cost<second){second=cost;other=i;}
                }
                // Compare with another service, not this train's zero boarding
                // delay. If none is known, the configured headway is a forecast,
                // not a promise. Patience discounts its cost by at most 25%.
                float nextWait=world.Transit.TimeToBoarding(this,service,false);
                if(float.IsInfinity(nextWait))nextWait=Mathf.Max(35,world.settings.ambientTrainHeadway);
                float waitCost=nextWait*(1-Mathf.Clamp01(Journey.Personality.Patience)*.25f)+Mathf.Clamp01(Journey.Personality.Assertiveness)*4;
                if(chosen<0&&remaining>5){
                    // A temporary obstruction is not a decision to abandon the
                    // service. The existing path and door queue remain owners.
                    RecordDecision(MetroDecision.Wait,MetroDecisionReason.TravelAndCongestion,margin:remaining,next:nextBoardChoice);
                    return false;
                }
                if(chosen<0||best>waitCost){
                    RecordDecision(MetroDecision.NextTrain,chosen<0?MetroDecisionReason.CannotReachInTime:MetroDecisionReason.Crowded,best,waitCost,remaining,chosen,-1);
                    deferredService=service;world.Traffic.ReleaseDoor(this);boardingTrain=null;
                    if(state==Activity.Board){Journey.CancelBoard();SetWaitingPoint();}
                    return false;
                }
                RecordDecision(MetroDecision.Door,MetroDecisionReason.TravelAndCongestion,best,Mathf.Min(second,waitCost),remaining,chosen,second<waitCost?other:-1,nextBoardChoice);
                if(state==Activity.Board&&chosen!=door){world.Traffic.ReleaseDoor(this);door=chosen;waypoint=0;FollowQueueGoal(world.Traffic.DoorApproach(this,service,door,true));}
                boardCommitUntil=Time.time+3.5f;
                evaluatedDoor=chosen;evaluatedBoard=true;
                return true;
            }
        }
        internal float PerceivedCarrySpeedScale(Vector3 forward)
        {
            if(!CanYieldToFlow||PortalCommitted||QueueActive||Journey.OnStairs||NarrowWalking){
                if(yieldingToPackage){yieldingToPackage=false;RecordDecision(MetroDecision.Resume,MetroDecisionReason.ActivityChanged);}
                return 1;
            }
            if(Time.time>=nextCourtesySense){
                nextCourtesySense=Time.time+.55f+(index%4)*.07f;
                var p=world.player;
                bool visible=p!=null&&p.cargo!=null&&p.cargo.gameObject.activeInHierarchy&&
                    (p.Riding==riding)&&(p.transform.position-transform.position).sqrMagnitude<12.25f&&
                    crowd.CanPerceive(this,p.cargo.position,p.transform,true);
                Vector3 delta=visible?p.transform.position-transform.position:Vector3.zero;delta.y=0;
                bool crossing=visible&&Vector3.Dot(delta,forward)>.2f&&Vector3.Dot(delta,forward)<3.1f&&
                    (delta-forward*Vector3.Dot(delta,forward)).sqrMagnitude<.95f*.95f;
                if(crossing&&Journey.Personality.Courtesy>.25f&&Time.time>=courtesyCooldown){
                    perceivedCarryPoint=p.cargo.position;courtesyHoldUntil=Time.time+1.1f;
                    if(!yieldingToPackage)RecordDecision(MetroDecision.Courtesy,MetroDecisionReason.VisiblePackage,next:nextCourtesySense);
                    yieldingToPackage=true;
                }else if(yieldingToPackage&&Time.time>=courtesyHoldUntil){
                    yieldingToPackage=false;courtesyCooldown=Time.time+1.5f;RecordDecision(MetroDecision.Resume,MetroDecisionReason.PathClear);
                }
            }
            return yieldingToPackage?.68f:1;
        }
    }
}
