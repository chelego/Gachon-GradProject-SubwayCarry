using UnityEngine;

namespace SubwayCarry.AI.V2
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class PassengerAiV23DMotor : MonoBehaviour, IPassengerAiV2PlanarMotor
    {
        [SerializeField] private float groundHeight;
        private Rigidbody body;
        private CapsuleCollider capsule;
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private System.Func<Vector2, float> heightAt;
        private bool stairMovement;
        private float stairUpSpeed,stairDownSpeed,lowerStairZ,upperStairZ;
        public Collider LastBlockingCollider {get;private set;}
#if UNITY_EDITOR
        public float ObservedStairPlanarSpeed {get;private set;}
        public float ObservedStairVerticalSpeed {get;private set;}
        private Vector3 previousPhysicsPosition;
        private bool physicsSampleValid;
        private void FixedUpdate()
        {
            Vector3 current=Body.position;
            if(physicsSampleValid && IsOnStairs && current.z> -4.15f && current.z<4.15f)
            {
                Vector3 change=current-previousPhysicsPosition;
                if(change.magnitude<1)
                {
                    ObservedStairPlanarSpeed=Mathf.Max(ObservedStairPlanarSpeed,new Vector2(change.x,change.z).magnitude/Time.fixedDeltaTime);
                    ObservedStairVerticalSpeed=Mathf.Max(ObservedStairVerticalSpeed,Mathf.Abs(change.y)/Time.fixedDeltaTime);
                }
            }
            previousPhysicsPosition=current; physicsSampleValid=true;
        }
#endif
        public bool IsOnStairs => stairMovement && Body.position.z>=lowerStairZ && Body.position.z<=upperStairZ && Mathf.Abs(Body.position.x)<1.9f;
        public void ConfigureStairMovement(float upSpeed,float downSpeed,float lowerZ,float upperZ)
        { stairMovement=true; stairUpSpeed=upSpeed; stairDownSpeed=downSpeed; lowerStairZ=lowerZ; upperStairZ=upperZ; }
        public void SetGroundSurface(System.Func<Vector2, float> sampleHeight) { heightAt = sampleHeight; }
        private float Height(Vector2 p) { return heightAt != null ? heightAt(p) : groundHeight; }

        public Vector2 Position
        {
            get { Vector3 p = Body.position; return new Vector2(p.x, p.z); }
        }
        public Vector3 WorldPosition => Body.position;
        private Rigidbody Body { get { if (body == null) Cache(); return body; } }

        private void Awake() { Cache(); }
        private void Cache()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            capsule.height = 1.78f;
            capsule.radius = 0.28f;
            capsule.center = Vector3.up * 0.89f;
        }
        public void Teleport(Vector2 position)
        {
            Body.position = new Vector3(position.x, Height(position), position.y);
            transform.position = Body.position;
#if UNITY_EDITOR
            physicsSampleValid=false;
#endif
        }
        public Vector2 Move(Vector2 target)
        {
            Vector3 start = Body.position;
            LastBlockingCollider=null;
            if(IsOnStairs)
            {
                Vector2 origin=new Vector2(start.x,start.z);
                float cap=target.y>=start.z?stairUpSpeed:stairDownSpeed;
                target=origin+Vector2.ClampMagnitude(target-origin,cap*Time.fixedDeltaTime);
            }
            Vector3 delta = new Vector3(target.x - start.x, Height(target) - start.y, target.y - start.z);
            float distance = delta.magnitude;
            if (distance > 0.0001f)
            {
                // The .22m step allowance clears authored .16m stair risers, not walls.
                Vector3 bottom = start + Vector3.up * 0.52f;
                Vector3 top = start + Vector3.up * 1.5f;
                int count = Physics.CapsuleCastNonAlloc(bottom, top, 0.27f, delta / distance,
                    hits, distance + 0.015f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float allowed = count == hits.Length ? 0f : distance;
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].transform.IsChildOf(transform)) continue;
                    float beforeHit=Mathf.Max(0f,hits[i].distance-.015f);
                    if(beforeHit<allowed) {allowed=beforeHit;LastBlockingCollider=hits[i].collider;}
                }
                delta *= allowed / distance;
            }
            Vector3 next = start + delta;
            float surface=Height(new Vector2(next.x,next.z));
            // A .16m riser must not become an instantaneous 8m/s vertical pop at 50Hz.
            next.y=stairMovement?Mathf.MoveTowards(start.y,surface,.9f*Time.fixedDeltaTime):surface;
            Body.MovePosition(next);
            return new Vector2(next.x, next.z);
        }
    }
}
