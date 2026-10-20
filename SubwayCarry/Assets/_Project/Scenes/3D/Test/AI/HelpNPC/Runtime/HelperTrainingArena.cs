using Unity.MLAgents;
using UnityEngine;

namespace SubwayCarry.AI.HelpNPC
{
    // Small, randomized interaction task. PPO chooses intent; deterministic
    // action execution and locomotion remain separate from the learned policy.
    public sealed class HelperTrainingArena : MonoBehaviour
    {
        public HelpfulCitizenAgent agent;
        public Transform helperVisual, playerVisual, cargoVisual, safeMarker;
        public Animator helperAnimator, playerAnimator;
        public Renderer[] helperRenderers;
        public bool demonstration;
        public int demonstrationCase;
        public Vector2 helper, player, helperVelocity, playerVelocity;
        public float balanceRisk, fatigue, density, trainMotion;
        public int EpisodeCount {get;private set;}
        public int SuccessCount {get;private set;}
        public int FallCount {get;private set;}
        public int HelpSteps {get;private set;}
        public float EpisodeReward {get;private set;}
        public int Case {get;private set;}
        public HelperAction Action {get;private set;}
        public bool HelpVisible => Action!=HelperAction.ContinueJourney;
        public float DecisionSeconds=>.1f;
        public float DemoPauseUntil {get;private set;}
        int steps;
        Vector2 safe;
        float cumulativeProgress;
        MaterialPropertyBlock block;
        static readonly Color Orange=new Color(.90f,.42f,.12f),Green=new Color(.12f,.76f,.30f);
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color"),Speed=Animator.StringToHash("Speed");
        public void ResetEpisode()
        {
            EpisodeCount++;
            Case=demonstration?demonstrationCase%4:Random.Range(0,4);
            steps=0;EpisodeReward=0;cumulativeProgress=0;Action=HelperAction.ContinueJourney;
            DemoPauseUntil=demonstration?Time.time+1.1f:0;
            player=new Vector2(Random.Range(-.45f,.45f),-3.7f);
            helper=player+new Vector2(Random.Range(.8f,1.6f)*(Random.value>.5f?1:-1),Random.Range(.4f,1.4f));
            balanceRisk=Random.Range(.02f,.13f);fatigue=Random.Range(.08f,.25f);density=Random.Range(.05f,.25f);trainMotion=Random.Range(.02f,.15f);
            if(Case==1)helper=player+new Vector2(Random.Range(-.18f,.18f),Random.Range(.65f,1.0f));
            if(Case==2){balanceRisk=Random.Range(.58f,.78f);trainMotion=Random.Range(.65f,.95f);}
            if(Case==3){fatigue=Random.Range(.72f,.96f);density=Random.Range(.55f,.85f);}
            safe=new Vector2(player.x>0?-2.2f:2.2f,-.5f);
            helperVelocity=playerVelocity=Vector2.zero;UpdateVisuals();
        }
        float Blocking()
        {
            Vector2 offset=helper-player;
            return offset.y>.15f&&offset.y<1.4f?Mathf.Clamp01(1-Mathf.Abs(offset.x)/.65f):0;
        }
        public HelperSituation Situation(HelperAction previous) => new HelperSituation {
            playerOffset=player-helper,goalOffset=new Vector2(-helper.x,-5.5f-helper.y),
            playerVelocity=playerVelocity,helperVelocity=helperVelocity,
            balanceRisk=balanceRisk,fatigue=fatigue,density=density,blocking=Blocking(),
            trainMotion=trainMotion,cargo=1,safeSpace=1,timeMargin=1-steps/100f,previousAction=previous };

        public void Simulate(HelperAction selected)
        {
            const float dt=.1f;
            float oldRisk=balanceRisk,oldFatigue=fatigue,oldBlocking=Blocking();
            Vector2 oldHelper=helper,oldPlayer=player;
            Action=selected;steps++;
            Vector2 target;
            switch(selected) {
                case HelperAction.YieldPassage:
                    target=new Vector2(Mathf.Clamp(player.x+(helper.x>=player.x?1.2f:-1.2f),-2.6f,2.6f),helper.y);break;
                case HelperAction.SteadyLoad:
                    target=player+new Vector2(helper.x>=player.x?.80f:-.80f,.05f);break;
                case HelperAction.GuideToSpace:
                    target=Vector2.Lerp(player,safe,.42f)+Vector2.up*.6f;break;
                default:target=new Vector2(helper.x,-5.5f);break;
            }
            helper=Vector2.MoveTowards(helper,target,1.6f*dt);
            float distance=Vector2.Distance(helper,player);
            bool support=selected==HelperAction.SteadyLoad&&distance<1.35f;
            bool guiding=selected==HelperAction.GuideToSpace&&distance<1.85f;
            float blockNow=Blocking();
            Vector2 direction=Vector2.up;
            if(guiding&&fatigue>.3f)direction=(safe-player).normalized;
            float speed=1.35f*(1-fatigue*.40f)*(1-density*.25f)*(1-blockNow*.92f);
            player+=direction*speed*dt;
            balanceRisk=Mathf.Clamp(balanceRisk+trainMotion*.13f*dt+(speed>.4f?.016f:0)*dt-(support?.34f:.014f)*dt,0,1.05f);
            fatigue=Mathf.Clamp01(fatigue+speed*.003f*dt-(guiding?.25f:0)*dt);
            if(guiding)density=Mathf.Max(.05f,density-.20f*dt);
            helperVelocity=(helper-oldHelper)/dt;playerVelocity=(player-oldPlayer)/dt;
            float progress=Mathf.Max(0,player.y-oldPlayer.y);cumulativeProgress+=progress;
            float reward=.08f*progress-.001f-.004f*balanceRisk;
            // Relief of meaningful need, rather than reward farming in a healthy state.
            if(oldRisk>.25f)reward+=Mathf.Max(0,oldRisk-balanceRisk)*1.4f;
            if(oldFatigue>.35f)reward+=Mathf.Max(0,oldFatigue-fatigue)*.85f;
            if(oldBlocking>.35f)reward+=Mathf.Max(0,oldBlocking-blockNow)*.30f;
            if(selected!=HelperAction.ContinueJourney){HelpSteps++;reward-=.003f;}
            if(selected==HelperAction.SteadyLoad&&oldRisk<.22f)reward-=.025f;
            if(selected==HelperAction.GuideToSpace&&oldFatigue<.3f)reward-=.025f;
            if(selected==HelperAction.YieldPassage&&oldBlocking<.12f)reward-=.006f;
            if(distance<.42f)reward-=.04f;
            bool fell=balanceRisk>=1;
            bool complete=player.y>=4.2f || (Vector2.Distance(player,safe)<.55f&&fatigue<.3f);
            if(fell){reward-=1;FallCount++;}
            if(complete){reward+=1;SuccessCount++;}
            if(steps>=100&&!complete&&!fell)reward-=.25f;
            EpisodeReward+=reward;agent.AddReward(reward);
            if(helperVisual!=null)UpdateVisuals();
            if(fell||complete||steps>=100) {
                Academy.Instance.StatsRecorder.Add("Helper/Success",complete?1:0);
                Academy.Instance.StatsRecorder.Add("Helper/Fall",fell?1:0);
                Academy.Instance.StatsRecorder.Add("Helper/Progress",cumulativeProgress);
                Academy.Instance.StatsRecorder.Add("Helper/RemainingRisk",balanceRisk);
                Academy.Instance.StatsRecorder.Add("Helper/RemainingFatigue",fatigue);
                agent.EndEpisode();
            }
        }
        void UpdateVisuals()
        {
            Vector3 origin=transform.position;
            if(helperVisual!=null) {
                helperVisual.position=origin+new Vector3(helper.x,0,helper.y);
                if(helperVelocity.sqrMagnitude>.005f)helperVisual.rotation=Quaternion.LookRotation(new Vector3(helperVelocity.x,0,helperVelocity.y));
            }
            if(playerVisual!=null) {
                playerVisual.position=origin+new Vector3(player.x,0,player.y);
                if(playerVelocity.sqrMagnitude>.005f)playerVisual.rotation=Quaternion.LookRotation(new Vector3(playerVelocity.x,0,playerVelocity.y));
            }
            if(cargoVisual!=null)cargoVisual.localRotation=Quaternion.Euler(0,0,Mathf.Sin(steps*.6f)*balanceRisk*18);
            if(safeMarker!=null)safeMarker.position=origin+new Vector3(safe.x,.012f,safe.y);
            if(helperAnimator!=null)helperAnimator.SetFloat(Speed,helperVelocity.magnitude);
            if(playerAnimator!=null)playerAnimator.SetFloat(Speed,playerVelocity.magnitude);
            if(block==null)block=new MaterialPropertyBlock();
            Color color=HelpVisible?Green:Orange;block.SetColor(BaseColor,color);block.SetColor(ColorId,color);
            if(helperRenderers!=null)foreach(var r in helperRenderers)if(r!=null) {
                for(int i=0;i<r.sharedMaterials.Length;i++)if(r.sharedMaterials[i]!=null&&!r.sharedMaterials[i].name.Contains("Joint"))r.SetPropertyBlock(block,i);
            }
        }
    }
}
