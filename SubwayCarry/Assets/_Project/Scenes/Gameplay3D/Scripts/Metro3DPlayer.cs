using SubwayCarry.Core.Contracts;
using SubwayCarry.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay3D
{
    [DefaultExecutionOrder(10)]
    public sealed class Metro3DPlayer : MonoBehaviour
    {
        public Metro3DWorld world;
        public Camera view;
        public Transform cargo;
        public PackageDurability package;
        public Metro3DFacility Target {get;private set;}
        public Metro3DFacility Using {get;private set;}
        public CarryPosture Posture {get;private set;}=CarryPosture.Standing;
        public float Stamina {get;private set;}=100;
        public Vector3 Velocity {get;private set;}
        public Metro3DTrain Riding {get;private set;}
        public bool BalanceActive => balanceTimer>0;
        public string BalanceKey => balanceSide<0?"A":"D";
        CharacterController motor;
        float pitch,verticalSpeed,bobPhase,impactCooldown,balanceTimer;
        int balanceSide;
        readonly Collider[] contacts=new Collider[16];
        Vector3 recoil;
        Vector3 lastPackagePosition;
        Vector3 lastTrainPosition;
        bool hasPackagePosition;

        void Awake() { motor=GetComponent<CharacterController>(); }
        void Start() {
            bool free=world.session==null||world.session.Blocked;
            Cursor.lockState=free?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=free;
        }
        public void TeleportForReturn(Vector3 p)
        {
            CancelFacility(); Riding=null; transform.SetParent(world.transform,true);
            motor.enabled=false; transform.position=p; transform.rotation=Quaternion.identity; motor.enabled=true;
            pitch=0; verticalSpeed=0; hasPackagePosition=false;
        }
        public void TriggerBalance(bool braking)
        {
            if(Riding==null||Using!=null||!world.session.HasPackage) return;
            balanceSide=braking?-1:1; balanceTimer=1.8f+world.session.BalanceLevel*.25f;
        }
        void Update()
        {
            var k=Keyboard.current; var mouse=Mouse.current;
            if(k==null||world.session==null) return;
            if(world.session.Blocked) { Velocity=Vector3.zero; return; }
            impactCooldown=Mathf.Max(0,impactCooldown-Time.deltaTime);
            Vector2 look=mouse!=null?mouse.delta.ReadValue()*world.settings.mouseSensitivity:Vector2.zero;
            transform.Rotate(Vector3.up,look.x,Space.Self);
            pitch=Mathf.Clamp(pitch-look.y,-78,78); view.transform.localRotation=Quaternion.Euler(pitch,0,0);
            Vector2 input=new Vector2((k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0),
                (k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0));
            input=Vector2.ClampMagnitude(input,1);
            bool interact=k.eKey.wasPressedThisFrame||k.fKey.wasPressedThisFrame;
            if(Using!=null) { if(input.sqrMagnitude>0||interact) CancelFacility(); else { UpdateView(0); return; } }
            else { FindTarget(); if(interact&&Target!=null) Interact(Target); }
            if(Using!=null) return;
            bool overhead=mouse!=null&&mouse.rightButton.isPressed&&world.session.HasPackage&&Stamina>1;
            Posture=overhead?CarryPosture.OverheadCarry:CarryPosture.Standing;
            bool running=k.leftShiftKey.isPressed&&input.y>0&&Stamina>5&&!overhead;
            float speed=(running?world.settings.runSpeed:world.settings.walkSpeed)*(1+world.session.AgilityLevel*.07f)*(overhead?.6f:1);
            Vector3 horizontal=(transform.right*input.x+transform.forward*input.y)*speed;
            verticalSpeed=motor.isGrounded?-1.5f:verticalSpeed-20*Time.deltaTime;
            motor.Move((horizontal+Vector3.up*verticalSpeed+recoil)*Time.deltaTime); Velocity=horizontal;
            recoil=Vector3.MoveTowards(recoil,Vector3.zero,Time.deltaTime*5);
            Stamina=Mathf.Clamp(Stamina+(overhead?-12:running?-8:15)*Time.deltaTime,0,100+world.session.StaminaLevel*20);
            UpdateRiding(); UpdateView(horizontal.magnitude); CheckPackageContact(); UpdateBalance(k);
            if(transform.position.y<-3) TeleportForReturn(world.HubSpawn);
        }

        void UpdateRiding()
        {
            var next=world.train!=null&&world.train.Contains(transform.position)?world.train:null;
            if(next==Riding) return;
            if(Riding!=null&&!Riding.DoorsOpen) return;
            Riding=next; transform.SetParent(Riding!=null?Riding.transform:world.transform,true);
            hasPackagePosition=false;
        }
        void UpdateView(float speed)
        {
            float height=Posture==CarryPosture.Sitting?1.16f:1.62f;
            bobPhase+=Time.deltaTime*speed*4; float bob=Mathf.Sin(bobPhase)*world.settings.headBob*Mathf.Min(speed,1);
            view.transform.localPosition=Vector3.Lerp(view.transform.localPosition,new Vector3(0,height+bob,0),Time.deltaTime*12);
            if(cargo!=null) {
                cargo.gameObject.SetActive(world.session.HasPackage);
                Vector3 target=Posture==CarryPosture.OverheadCarry?new Vector3(0,.16f,.3f):new Vector3(0,-.47f,.62f);
                cargo.localPosition=Vector3.Lerp(cargo.localPosition,target,Time.deltaTime*7);
            }
        }
        void FindTarget()
        {
            Target=null; float nearest=1.65f;
            foreach(var f in world.facilities) {
                if(f==null||!f.gameObject.activeInHierarchy||(!f.Available&&f.Occupant!=this)) continue;
                Vector3 point=f.approach!=null?f.approach.position:f.transform.position;
                float distance=Vector3.Distance(transform.position+Vector3.up*.35f,point+Vector3.up*.35f);
                if(f.kind==Metro3DFacilityKind.FareGate||f.kind==Metro3DFacilityKind.ExitGate) distance=Vector3.Distance(transform.position+Vector3.up*.65f,f.transform.position);
                if(distance>=nearest) continue;
                Vector3 to=f.transform.position-view.transform.position;
                if(Vector3.Dot(view.transform.forward,to.normalized)<.15f) continue;
                // A facility on another side of a wall cannot be selected through that wall.
                if(Physics.Raycast(view.transform.position,to.normalized,out var hit,to.magnitude,~0,QueryTriggerInteraction.Ignore)&&hit.collider!=f.blocker&&hit.transform!=f.transform&&hit.distance<to.magnitude-.25f) continue;
                nearest=distance; Target=f;
            }
        }
        void Interact(Metro3DFacility f)
        {
            if(f.lift!=null) { f.lift.Request(f.liftFloor); return; }
            if(f.kind==Metro3DFacilityKind.FareGate||f.kind==Metro3DFacilityKind.ExitGate) { world.session.UseGate(f); return; }
            if(!f.Claim(this)) return;
            Using=f; motor.enabled=false;
            transform.position=f.anchor.position; transform.rotation=f.anchor.rotation; motor.enabled=true;
            Posture=f.kind==Metro3DFacilityKind.Seat?CarryPosture.Sitting:f.kind==Metro3DFacilityKind.Lean?CarryPosture.Leaning:CarryPosture.HoldingSupport;
            Velocity=Vector3.zero; Target=null; hasPackagePosition=false;
            UpdateRiding();
        }
        public void CancelFacility()
        {
            if(Using==null) return;
            var f=Using; f.Release(this); Using=null;
            if(f.approach!=null) { motor.enabled=false; transform.position=f.approach.position; motor.enabled=true; }
            Posture=CarryPosture.Standing; Target=null; hasPackagePosition=false;
        }
        void CheckPackageContact()
        {
            if(!world.session.HasPackage||Using!=null||impactCooldown>0) { hasPackagePosition=false; return; }
            // Actual package volume in world space, not a character-radius damage approximation.
            Vector3 p=cargo.position+cargo.up*.14f;
            Vector3 trainPosition=Riding!=null?Riding.transform.position:Vector3.zero;
            Vector3 displacement=p-lastPackagePosition-(Riding!=null?trainPosition-lastTrainPosition:Vector3.zero);
            float speed=hasPackagePosition?displacement.magnitude/Mathf.Max(Time.deltaTime,.001f):0;
            lastPackagePosition=p; lastTrainPosition=trainPosition; hasPackagePosition=true;
            if(speed<.8f) return;
            int count=Physics.OverlapBoxNonAlloc(p,new Vector3(.18f,.125f,.18f),contacts,cargo.rotation,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++) {
                var c=contacts[i]; if(c.transform.IsChildOf(transform)) continue;
                ApplyImpact(c.gameObject,speed,c.ClosestPoint(p)); recoil=-transform.forward*.8f; break;
            }
        }
        public void ApplyImpact(GameObject source,float speed,Vector3 point)
        {
            if(impactCooldown>0||!world.session.HasPackage) return;
            impactCooldown=.9f;
            var data=new PackageImpactData(source,new Vector2(point.x,point.z),Mathf.Max(0,speed-.65f)*.38f,0);
            package.ApplyImpact(in data);
        }
        void UpdateBalance(Keyboard k)
        {
            if(balanceTimer<=0) return;
            if((balanceSide<0?k.aKey:k.dKey).wasPressedThisFrame) { balanceTimer=0; return; }
            balanceTimer-=Time.deltaTime;
            if(balanceTimer<=0) { ApplyImpact(world.train.gameObject,3.5f,transform.position); recoil=-transform.forward*1.6f; }
        }
        void OnDestroy() { if(Using!=null) Using.Release(this); Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
    }
}
