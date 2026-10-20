using UnityEngine;
using Unity.MLAgents.Policies;
using Unity.MLAgents;
using UnityEngine.InputSystem;

namespace SubwayCarry.AI.HelpNPC
{
    public sealed class HelperDemoController : MonoBehaviour
    {
        public HelperTrainingArena arena;
        public Camera cameraView;
        public Font hudFont;
        int previousEpisode;
        float episodeStarted;
        GUIStyle body;
        static readonly string[] Cases={"일반 이동","통행 방해","균형 불안정","피로·혼잡"};
        void Update()
        {
            if(arena.EpisodeCount!=previousEpisode){previousEpisode=arena.EpisodeCount;episodeStarted=Time.time;}
            var decision=arena.agent.GetComponent<DecisionRequester>();
            decision.enabled=Time.time-episodeStarted>1.1f;
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.digit1Key.wasPressedThisFrame)SetCase(0);
            if(keyboard.digit2Key.wasPressedThisFrame)SetCase(1);
            if(keyboard.digit3Key.wasPressedThisFrame)SetCase(2);
            if(keyboard.digit4Key.wasPressedThisFrame)SetCase(3);
        }
        public void SetCase(int value){arena.agent.GetComponent<DecisionRequester>().enabled=false;arena.demonstrationCase=value;arena.agent.EndEpisode();episodeStarted=Time.time;}
        void OnGUI()
        {
            if(body==null)body=new GUIStyle(GUI.skin.label){fontSize=18,font=hudFont};
            GUI.Label(new Rect(24,20,650,32),Cases[arena.Case]+" / "+HelperSituation.Label(arena.Action),body);
        }
    }
}
