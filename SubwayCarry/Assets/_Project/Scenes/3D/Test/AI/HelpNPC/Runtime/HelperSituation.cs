using Unity.MLAgents.Sensors;
using UnityEngine;

namespace SubwayCarry.AI.HelpNPC
{
    public enum HelperAction { ContinueJourney, YieldPassage, SteadyLoad, GuideToSpace }

    // Shared observation contract for the learning field and live game adapter.
    public struct HelperSituation
    {
        public Vector2 playerOffset, goalOffset, playerVelocity, helperVelocity;
        public float balanceRisk, fatigue, density, blocking, trainMotion, cargo, safeSpace, timeMargin;
        public HelperAction previousAction;
        public const int ObservationCount = 20;
        public void Observe(VectorSensor sensor)
        {
            sensor.AddObservation(Vector2.ClampMagnitude(playerOffset / 6f,1));
            sensor.AddObservation(Vector2.ClampMagnitude(goalOffset / 10f,1));
            sensor.AddObservation(Vector2.ClampMagnitude(playerVelocity / 2f,1));
            sensor.AddObservation(Vector2.ClampMagnitude(helperVelocity / 2f,1));
            sensor.AddObservation(Mathf.Clamp01(balanceRisk));sensor.AddObservation(Mathf.Clamp01(fatigue));
            sensor.AddObservation(Mathf.Clamp01(density));sensor.AddObservation(Mathf.Clamp01(blocking));
            sensor.AddObservation(Mathf.Clamp01(trainMotion));sensor.AddObservation(Mathf.Clamp01(cargo));
            sensor.AddObservation(Mathf.Clamp01(safeSpace));sensor.AddObservation(Mathf.Clamp01(timeMargin));
            sensor.AddOneHotObservation((int)previousAction,4);
        }
        public static string Label(HelperAction action) => action==HelperAction.YieldPassage?"통행 공간 양보":action==HelperAction.SteadyLoad?"균형 안정 지원":action==HelperAction.GuideToSpace?"여유 공간 안내":"일반 이동";
    }
}
