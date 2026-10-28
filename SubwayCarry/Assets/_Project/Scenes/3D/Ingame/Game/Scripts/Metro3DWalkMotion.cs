using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Shared forward-gait integration. No position writes, physics or animation ownership.
    // The caller validates the returned arc with its existing collision/reservation layer.
    public static class Metro3DWalkMotion
    {
        public static float ArrivalSpeed(float remaining)=>Mathf.Sqrt(2*3.8f*Mathf.Max(0,remaining-.06f));
        public static Vector3 Step(Vector3 forward,Vector3 actual,Vector3 wanted,float turnLimit,float dt,
            ref float yawRate,ref bool pivot,out Vector3 facing)
        {
            forward.y=actual.y=wanted.y=0;forward.Normalize();
            float speed=actual.magnitude,request=wanted.magnitude;
            float error=request>.05f?Vector3.SignedAngle(forward,wanted,Vector3.up):0;
            if(request>.05f&&Mathf.Abs(error)>110)pivot=true;
            if(request<.05f||Mathf.Abs(error)<8)pivot=false;
            if(pivot&&speed>.1f){yawRate=Mathf.MoveTowards(yawRate,0,540*dt);facing=forward;return forward*Mathf.MoveTowards(speed,0,3.8f*dt);}
            // Braking angular velocity prevents hunting around small, steady targets.
            float targetRate=Mathf.Abs(error)<.35f?0:Mathf.Sign(error)*Mathf.Min(turnLimit,Mathf.Sqrt(2*540*Mathf.Abs(error)));
            yawRate=Mathf.MoveTowards(yawRate,targetRate,540*dt);
            float turn=Mathf.Clamp(yawRate*dt,-Mathf.Abs(error),Mathf.Abs(error));
            if(Mathf.Abs(error)<.35f)turn=0;
            facing=Quaternion.AngleAxis(turn,Vector3.up)*forward;
            // Constant-curvature midpoint displacement, not a projected avoidance vector.
            Vector3 tangent=Quaternion.AngleAxis(turn*.5f,Vector3.up)*forward;
            float cap=request;
            if(pivot)cap=0;
            else if(Mathf.Abs(error)>70)cap=Mathf.Min(cap,request*Mathf.Clamp01((110-Mathf.Abs(error))/40));
            float next=Mathf.MoveTowards(speed,cap,(cap<speed?3.8f:2.3f)*dt);
            return tangent*next;
        }
    }
}
