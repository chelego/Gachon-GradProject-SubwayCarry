using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace SubwayCarry.AI.HelpNPC
{
    public sealed class HelpfulCitizenAgent : Agent
    {
        public HelperTrainingArena arena;
        public HelperAction SelectedAction {get;private set;}
        float demoCommitUntil;
        public override void OnEpisodeBegin() { SelectedAction=HelperAction.ContinueJourney;demoCommitUntil=0; if(arena!=null) arena.ResetEpisode(); }
        public override void CollectObservations(VectorSensor sensor) { arena.Situation(SelectedAction).Observe(sensor); }
        public override void OnActionReceived(ActionBuffers actions)
        {
            if(arena.demonstration&&Time.time<arena.DemoPauseUntil)return;
            var next=(HelperAction)Mathf.Clamp(actions.DiscreteActions[0],0,3);
            // Demo uses the same short execution commitment as the live adapter.
            // Training and held-out raw-policy evaluation have no commitment wrapper.
            if(arena.demonstration&&next!=SelectedAction){if(Time.time<demoCommitUntil)next=SelectedAction;else demoCommitUntil=Time.time+1.1f;}
            SelectedAction=next;
            arena.Simulate(SelectedAction);
        }
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // Explicitly labelled manual baseline, never presented as a trained policy.
            var actions=actionsOut.DiscreteActions;
            actions[0]=0;
        }
    }
}
