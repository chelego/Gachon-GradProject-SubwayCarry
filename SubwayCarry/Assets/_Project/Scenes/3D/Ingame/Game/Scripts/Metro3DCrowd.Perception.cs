using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed partial class Metro3DCrowd
    {
        readonly RaycastHit[] perceptionHits=new RaycastHit[24];
        internal bool CanPerceive(Metro3DPassenger actor,Vector3 target,Transform source,bool visual)
        {
            Vector3 eye=actor.transform.position+actor.transform.up*(1.5f*actor.HeightRatio);
            Vector3 delta=target-eye;float distance=delta.magnitude;
            if(distance>(visual?6:8)||Mathf.Abs(delta.y)>2.2f)return false;
            if(distance<.1f)return true;
            if(visual&&Vector3.Dot(actor.transform.forward,delta/distance)<.12f)return false;
            int n=Physics.RaycastNonAlloc(eye,delta/distance,perceptionHits,distance,~0,QueryTriggerInteraction.Ignore);
            if(n==perceptionHits.Length)return false;
            for(int i=0;i<n;i++){
                var hit=perceptionHits[i];
                if(hit.transform.IsChildOf(actor.transform)||source!=null&&hit.transform.IsChildOf(source))continue;
                if(!visual&&hit.collider is CharacterController)continue;
                if(hit.distance<distance-.15f)return false;
            }
            return true;
        }
    }
}
