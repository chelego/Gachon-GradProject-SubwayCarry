using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace SubwayCarry.Gameplay3D
{
    // Rendering-only observer: labels never change destinations, activation or simulation priority.
    [DefaultExecutionOrder(-160)]
    public sealed class Metro3DAiObserver : MonoBehaviour
    {
        public Metro3DWorld world;
        public bool Active {get;private set;}
        public bool SimulationFrozen {get;private set;}
        public enum FollowView { Free, Eyes, Rear, Side }
        public FollowView CameraMode {get;private set;}
        public Guid SelectedId {get;private set;}
        public string SelectedDetails => selectedDetails;
        public Metro3DPassenger SelectedPassenger => crowd!=null&&crowd.TryPassenger(SelectedId,out var actor)?actor:null;
        public Camera View => Active&&cameraView!=null?cameraView:world.player.view;
        Camera cameraView;
        float pitch,yaw,savedScale=1,nextLabels;
        bool savedCameraEnabled;
        CursorLockMode savedCursorLock;
        bool savedCursorVisible;
        Metro3DCrowd crowd;
        Metro3DPassenger boundRepresentation;
        Transform selectedHead;
        string selectedDetails="",location="";
        readonly StringBuilder detail=new StringBuilder(1800);
        struct Label {public Metro3DPassenger actor;public Guid identity;public string text;public float distance;}
        readonly List<Label> labels=new List<Label>(32);
        readonly RaycastHit[] hits=new RaycastHit[24];
        readonly Rect[] labelRects=new Rect[32];
        GUIStyle labelStyle,hintStyle;
        static readonly string[] Arrows={"↑","↗","→","↘","↓","↙","←","↖"};
        public int LabelCount=>labels.Count;
        void Update()
        {
            if(world==null||world.player==null||world.session==null)return;
            var key=Keyboard.current;
            if(key!=null&&key.f5Key.wasPressedThisFrame)SetActive(!Active);
            if(key!=null&&key.f6Key.wasPressedThisFrame)SetFrozen(!SimulationFrozen);
            if(!Active)return;
            if(key!=null&&key.mKey.wasPressedThisFrame&&SelectedPassenger!=null)Metro3DMotionTrial.Toggle(SelectedPassenger);
            var mouse=Mouse.current;
            if(key!=null&&key.escapeKey.wasPressedThisFrame)ClearSelection();
            if(key!=null&&key.tabKey.wasPressedThisFrame&&SelectedId!=Guid.Empty)SetFollowView(CameraMode==FollowView.Eyes?FollowView.Rear:CameraMode==FollowView.Rear?FollowView.Side:FollowView.Eyes);
            if(mouse!=null&&mouse.leftButton.wasPressedThisFrame)Pick(mouse.position.ReadValue());
            if(key!=null&&CameraMode==FollowView.Free)MoveView(key,mouse);
            if(Time.unscaledTime>=nextLabels){nextLabels=Time.unscaledTime+.2f;RefreshLabels();RefreshSelected();location=LocateView();}
        }
        void LateUpdate()
        {
            if(!Active||CameraMode==FollowView.Free)return;
            var actor=SelectedPassenger;if(actor==null)return;
            if(actor!=boundRepresentation)BindRepresentation(actor);
            Vector3 eye=selectedHead!=null?selectedHead.position:actor.transform.position+Vector3.up*1.62f;
            Vector3 desired=CameraMode==FollowView.Eyes?eye+actor.transform.forward*.12f:
                CameraMode==FollowView.Rear?eye-actor.transform.forward*2.7f+Vector3.up*.35f:eye+actor.transform.right*2.6f+Vector3.up*.25f;
            Vector3 offset=desired-eye;int count=Physics.SphereCastNonAlloc(eye,.08f,offset.normalized,hits,offset.magnitude,~0,QueryTriggerInteraction.Ignore);
            float distance=offset.magnitude;
            for(int i=0;i<count;i++)if(!(hits[i].collider is CharacterController)&&!hits[i].transform.IsChildOf(actor.transform))distance=Mathf.Min(distance,Mathf.Max(.08f,hits[i].distance-.06f));
            desired=eye+offset.normalized*distance;
            var t=cameraView.transform;t.position=Vector3.Lerp(t.position,desired,1-Mathf.Exp(-12*Mathf.Min(.1f,Time.unscaledDeltaTime)));
            Vector3 looking=CameraMode==FollowView.Eyes?actor.transform.forward:eye-t.position;
            if(looking.sqrMagnitude>.001f)t.rotation=Quaternion.LookRotation(looking,Vector3.up);
        }
        void BindRepresentation(Metro3DPassenger actor)
        {
            boundRepresentation=actor;selectedHead=null;
            foreach(var bone in actor.GetComponentsInChildren<Transform>(true))if(bone.name=="DEF-head"||bone.name=="Head"){selectedHead=bone;break;}
        }
        public bool SelectPassenger(Guid id)
        {
            if(!Active||crowd==null||!crowd.TryPassenger(id,out var actor))return false;
            SelectedId=id;CameraMode=FollowView.Rear;BindRepresentation(actor);RefreshSelected();return true;
        }
        public void ClearSelection()
        {
            SelectedId=Guid.Empty;CameraMode=FollowView.Free;boundRepresentation=null;selectedHead=null;selectedDetails="";
            if(cameraView!=null){yaw=cameraView.transform.eulerAngles.y;pitch=Mathf.DeltaAngle(0,cameraView.transform.eulerAngles.x);}
        }
        public void SetFollowView(FollowView mode) {if(SelectedId!=Guid.Empty&&mode!=FollowView.Free)CameraMode=mode;else ClearSelection();}
        void Pick(Vector2 point)
        {
            Ray ray=cameraView.ScreenPointToRay(point);
            // Only on an explicit click: NonAlloc results are unordered and a
            // full buffer can omit the nearest passenger behind many wall panels.
            var clicked=Physics.RaycastAll(ray,80,~0,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;Metro3DPassenger chosen=null;
            for(int i=0;i<clicked.Length;i++) {
                if(world.player!=null&&clicked[i].transform.IsChildOf(world.player.transform)||clicked[i].distance>=nearest)continue;
                nearest=clicked[i].distance;chosen=clicked[i].collider.GetComponentInParent<Metro3DPassenger>();
            }
            if(chosen!=null)SelectPassenger(chosen.PersistentId);else ClearSelection();
        }
        public void SetActive(bool active)
        {
            if(Active==active)return;
            if(active) {
                crowd=world.GetComponent<Metro3DCrowd>();savedCursorLock=Cursor.lockState;savedCursorVisible=Cursor.visible;
                if(cameraView==null) {
                    var o=new GameObject("AI observer camera");o.tag="MainCamera";o.transform.SetParent(transform,false);
                    cameraView=o.AddComponent<Camera>();cameraView.CopyFrom(world.player.view);
                    var source=world.player.view.GetComponent<UniversalAdditionalCameraData>();
                    var data=o.AddComponent<UniversalAdditionalCameraData>();
                    if(source!=null){data.renderPostProcessing=source.renderPostProcessing;data.renderShadows=source.renderShadows;data.volumeLayerMask=source.volumeLayerMask;data.antialiasing=source.antialiasing;data.antialiasingQuality=source.antialiasingQuality;}
                }
                cameraView.transform.SetPositionAndRotation(world.player.view.transform.position,world.player.view.transform.rotation);
                yaw=cameraView.transform.eulerAngles.y;pitch=Mathf.DeltaAngle(0,cameraView.transform.eulerAngles.x);
                savedCameraEnabled=world.player.view.enabled;world.player.view.enabled=false;cameraView.gameObject.SetActive(true);
                if(SelectedId==Guid.Empty)CameraMode=FollowView.Free;
            } else {
                if(cameraView!=null)cameraView.gameObject.SetActive(false);
                if(world.player!=null)world.player.view.enabled=savedCameraEnabled;
                labels.Clear();
            }
            Active=active;world.session.ObservationInput(active);nextLabels=0;
            if(!active){Cursor.lockState=savedCursorLock;Cursor.visible=savedCursorVisible;}
        }
        public void SetFrozen(bool frozen)
        {
            if(SimulationFrozen==frozen)return;
            if(frozen){savedScale=Time.timeScale;Time.timeScale=0;}
            else Time.timeScale=savedScale;
            SimulationFrozen=frozen;world.session.SetCursor();
        }
        void MoveView(Keyboard key,Mouse mouse)
        {
            if(mouse!=null&&mouse.rightButton.isPressed){Vector2 d=mouse.delta.ReadValue()*world.settings.mouseSensitivity;yaw+=d.x;pitch=Mathf.Clamp(pitch-d.y,-89,89);}
            var t=cameraView.transform;t.rotation=Quaternion.Euler(pitch,yaw,0);
            Vector3 input=new Vector3((key.dKey.isPressed?1:0)-(key.aKey.isPressed?1:0),(key.eKey.isPressed?1:0)-(key.qKey.isPressed?1:0),(key.wKey.isPressed?1:0)-(key.sKey.isPressed?1:0));
            Vector3 motion=t.right*input.x+Vector3.up*input.y+t.forward*input.z;
            t.position+=Vector3.ClampMagnitude(motion,1)*(key.leftShiftKey.isPressed?12:4)*Mathf.Min(.1f,Time.unscaledDeltaTime);
        }
        bool Visible(Metro3DPassenger actor,Vector3 head)
        {
            Vector3 offset=head-View.transform.position;int n=Physics.RaycastNonAlloc(View.transform.position,offset.normalized,hits,offset.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(n==hits.Length)return false;
            for(int i=0;i<n;i++)if(!(hits[i].collider is CharacterController)&&!hits[i].transform.IsChildOf(actor.transform)&&hits[i].distance<offset.magnitude-.2f)return false;
            return true;
        }
        void RefreshLabels()
        {
            labels.Clear();var cam=View;
            foreach(var actor in world.passengers) {
                if(actor==null||!actor.isActiveAndEnabled||actor.Journey==null)continue;
                Vector3 head=actor.transform.position+Vector3.up*1.95f;
                float distance=(head-cam.transform.position).sqrMagnitude;
                if(distance>32*32)continue;
                Vector3 p=cam.WorldToViewportPoint(head);
                if(p.z<.1f||p.x<0||p.x>1||p.y<0||p.y>1||!Visible(actor,head))continue;
                int at=0;while(at<labels.Count&&labels[at].distance<distance)at++;
                if(at>=32)continue;
                var j=actor.Journey;
                Vector3 desired=actor.Velocity.sqrMagnitude>.01f?actor.Velocity:actor.MovementIntent;desired.y=0;
                float angle=Mathf.Atan2(Vector3.Dot(desired,cam.transform.right),Vector3.Dot(desired,Vector3.ProjectOnPlane(cam.transform.forward,Vector3.up)))*Mathf.Rad2Deg;
                string arrow=desired.sqrMagnitude<.01f?"·":Arrows[(Mathf.RoundToInt(angle/45)+8)%8];
                string text=(actor.PersistentId==SelectedId?"선택 · ":"#"+actor.index+"  ")+arrow;
                labels.Insert(at,new Label{actor=actor,identity=actor.PersistentId,text=text,distance=distance});if(labels.Count>32)labels.RemoveAt(32);
            }
        }
        public string CurrentLocation()=>location;
        string LocateView()
        {
            Vector3 p=View.transform.position;
            foreach(var train in world.Trains)if(train!=null&&train.Contains(p))return (train.Moving?"이동 중 · ":"정차 · ")+world.StopLabel(train.CurrentStop);
            return world.StopLabel(world.StationAt(p))+"역 · "+(p.y>world.settings.departureFloor-1?"대합실":"승강장");
        }
        void RefreshSelected()
        {
            if(SelectedId==Guid.Empty){selectedDetails="";return;}
            var actor=SelectedPassenger;
            if(actor==null){if(!selectedDetails.StartsWith("표현 없음"))selectedDetails="표현 없음 · 선택 ID 유지 (새 승객으로 재사용된 모델은 추적하지 않음)\n"+selectedDetails;return;}
            var j=actor.Journey;detail.Clear();
            detail.Append("승객 ID ").Append(SelectedId.ToString("N")).Append("  [").Append(CameraMode).Append("]\n");
            detail.Append("출발 ").Append(world.StopLabel(j.OriginStop)).Append(" / 최종 ").Append(j.DestinationLabel).Append('\n');
            detail.Append("다음 하차 ").Append(j.HasModeledDestination?j.DestinationLabel:"논리 탑승 구간 연결 대기").Append(" / 환승: 경로 연결 미구현\n");
            var route=world.session.Route.stops;
            detail.Append("물리 서비스 노선 ").Append(j.OriginStop<route.Length?route[j.OriginStop].line:"").Append(" / 방향 ").Append(j.TravelDirection>0?"정방향":"역방향").Append(" / 승강장 측 ").Append(j.TrainSide).Append('\n');
            var train=actor.Riding;detail.Append("열차 ").Append(train!=null?train.name:"미탑승 · 선택 확정 없음").Append(" / 현재 ").Append(actor.inTrain?"객차":world.StopLabel(actor.stationIndex)).Append('\n');
            detail.Append("여정 ").Append(j.Blackboard.CurrentStage).Append(" / 행동 ").Append(actor.ActivityLabel).Append(" / ").Append(j.GateAction).Append('\n');
            detail.Append("보행 ").Append(actor.WalkPhase).Append(" / 희망 ").Append(actor.DesiredVelocity.magnitude.ToString("F2")).Append(" / 회피 ").Append(actor.AvoidanceVelocity.magnitude.ToString("F2")).Append(" / 실제 ").Append(actor.Velocity.magnitude.ToString("F2")).Append(" m/s\n");
            detail.Append("속도 벡터 희망 ").Append(actor.DesiredVelocity.ToString("F2")).Append(" / 회피 ").Append(actor.AvoidanceVelocity.ToString("F2")).Append(" / 실제 ").Append(actor.Velocity.ToString("F2")).Append('\n');
            detail.Append("몸 ").Append(actor.transform.eulerAngles.y.ToString("F1")).Append("° / 회전 ").Append(actor.RotationDegreesPerSecond.ToString("F1")).Append("°/s / 누적 ").Append(actor.RotationTravel.ToString("F0")).Append("° / 시선: 독립 제어 없음\n");
            detail.Append("정지 이유 ").Append(world.session.SimulationPaused?Metro3DStopReason.SimulationPaused:actor.StopReason).Append(" / 막힘 ").Append(actor.BlockedSeconds.ToString("F1")).Append("s\n");
            detail.Append("경로 ").Append(actor.PathPointIndex).Append('/').Append(actor.PathPointCount).Append(" / 다음점 ").Append(actor.PathWaypoint.ToString("F2")).Append(" / 선행점 ").Append(actor.TravelLookahead.ToString("F2")).Append('\n');
            detail.Append("시설 ").Append(j.ReservedGate!=null?j.ReservedGate.name:actor.Seat!=null?actor.Seat.name:"없음").Append(" / 줄 ").Append(j.GateQueueSlot).Append(" / 준비 경로 ").Append(actor.ContinuationReady).Append('\n');
            detail.Append("최근 복구 ").Append(j.Blackboard.LastRecoveryReason).Append(" / 누적 ").Append(j.Blackboard.RecoveryCount);
            actor.MotionTrial?.AppendDetails(detail);
            selectedDetails=detail.ToString();
        }
        void OnGUI()
        {
            if(world==null||world.player==null||(!Active&&!SimulationFrozen))return;
            if(labelStyle==null){labelStyle=new GUIStyle(GUI.skin.label){font=world.settings.font,fontSize=13,alignment=TextAnchor.MiddleCenter};hintStyle=new GUIStyle(labelStyle){alignment=TextAnchor.UpperLeft,fontSize=14};}
            Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;GUI.matrix=Matrix4x4.identity;
            float hintY=Active?16:Screen.height-75;
            GUI.Box(new Rect(16,hintY,620,72),GUIContent.none);
            GUI.Label(new Rect(26,hintY+6,600,64),(Active?CurrentLocation()+" · AI 관찰":"AI 일시정지")+"\nF5 종료/진입 · F6 명시적 정지/계속 · WASD / Q·E / Shift\n우클릭 시선 · 클릭 선택 · Tab 시점 · Esc 해제 · M 매칭 ON/OFF",hintStyle);
            if(Active&&SelectedId!=Guid.Empty){float width=Mathf.Min(790,Screen.width-32);GUI.Box(new Rect(16,100,width,430),GUIContent.none);GUI.Label(new Rect(26,106,width-20,420),selectedDetails,hintStyle);}
            int drawn=0;
            if(Active)foreach(var item in labels) {
                var actor=item.actor;if(actor==null||!actor.isActiveAndEnabled||actor.PersistentId!=item.identity)continue;
                Vector3 p=View.WorldToScreenPoint(actor.transform.position+Vector3.up*1.95f);if(p.z<.1f)continue;
                Rect r=new Rect(Mathf.Clamp(p.x-110,2,Mathf.Max(2,Screen.width-222)),Screen.height-p.y-44,220,42);
                bool overlaps=r.y<92||r.yMax>Screen.height||SelectedId!=Guid.Empty&&r.x<810&&r.y<530;
                for(int i=0;i<drawn&&!overlaps;i++)overlaps=r.Overlaps(labelRects[i]);
                if(overlaps)continue;
                labelRects[drawn++]=r;GUI.Box(r,GUIContent.none);GUI.Label(r,item.text,labelStyle);
            }
            GUI.matrix=matrix;GUI.color=color;
        }
        void OnDisable()
        {
            if(SimulationFrozen){Time.timeScale=savedScale;SimulationFrozen=false;}
            if(Active){if(world!=null&&world.player!=null)world.player.view.enabled=savedCameraEnabled;if(cameraView!=null)cameraView.gameObject.SetActive(false);Active=false;if(world!=null&&world.session!=null)world.session.ObservationInput(false);Cursor.lockState=savedCursorLock;Cursor.visible=savedCursorVisible;}
        }
    }
}
