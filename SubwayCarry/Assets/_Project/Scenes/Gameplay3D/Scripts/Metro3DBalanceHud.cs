using SubwayCarry.Core.Contracts;
using SubwayCarry.Gameplay;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    [DisallowMultipleComponent]
    public sealed class Metro3DBalanceHud : MonoBehaviour
    {
        Metro3DWorld world;
        PlayerBalance balance;
        GUIStyle header,label,key,result,time;
        readonly Color panel=new Color(.035f,.047f,.047f,.70f);
        readonly Color row=new Color(.14f,.155f,.145f,.72f);
        readonly Color accent=new Color(.95f,.73f,.21f,.96f);

        public void Configure(Metro3DWorld owner,PlayerBalance provider)
        {
            world=owner;
            balance=provider;
        }

        void PrepareStyles()
        {
            if(label!=null) return;
            Font font=world!=null&&world.settings!=null?world.settings.font:null;
            header=new GUIStyle(GUI.skin.label) {font=font,fontSize=10,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft};
            header.normal.textColor=new Color(.8f,.83f,.81f,.92f);
            label=new GUIStyle(GUI.skin.label) {font=font,fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft};
            label.normal.textColor=new Color(.94f,.95f,.94f,.98f);
            key=new GUIStyle(label) {fontSize=17,alignment=TextAnchor.MiddleCenter};
            key.normal.textColor=new Color(.08f,.075f,.045f);
            result=new GUIStyle(label) {fontSize=14,alignment=TextAnchor.MiddleCenter};
            time=new GUIStyle(header) {fontSize=10,alignment=TextAnchor.MiddleRight};
        }

        void OnGUI()
        {
            if(balance==null||world==null||world.session==null) return;
            if(world.session.PhoneOpen||world.session.Paused||world.session.ResultOpen) return;
            BalanceStateSnapshot state=balance.CurrentBalanceState;
            if(state.Phase==BalanceChallengePhase.Inactive||state.Phase==BalanceChallengePhase.Cancelled) return;

            PrepareStyles();
            Matrix4x4 previousMatrix=GUI.matrix;
            GUI.matrix=Matrix4x4.identity;
            float width=Mathf.Clamp(Screen.width*.31f,280,340);
            float height=96;
            float y=Mathf.Clamp(Screen.height*.39f-height*.5f,24,Screen.height-height-24);
            Rect frame=new Rect((Screen.width-width)*.5f,y,width,height);
            Fill(new Rect(frame.x+3,frame.y+4,frame.width,frame.height),new Color(0,0,0,.24f));
            Fill(frame,panel);
            Outline(frame,1,new Color(.42f,.46f,.44f,.5f));
            Fill(new Rect(frame.x,frame.y,frame.width,2),accent);
            Fill(new Rect(frame.x+10,frame.y+10,4,4),accent);
            GUI.Label(new Rect(frame.x+21,frame.y+3,frame.width-92,24),"균형잡기 / BALANCE",header);
            GUI.Label(new Rect(frame.xMax-68,frame.y+3,56,24),state.RemainingSeconds.ToString("0.0")+"초",time);

            Rect content=new Rect(frame.x+8,frame.y+25,frame.width-16,frame.height-33);
            Fill(content,row);
            if(state.Phase==BalanceChallengePhase.Succeeded||state.Phase==BalanceChallengePhase.Failed)
                DrawResult(content,state.Phase==BalanceChallengePhase.Succeeded);
            else if(state.Phase==BalanceChallengePhase.Warning)
                GUI.Label(content,"곧 흔들립니다 · 준비하세요",result);
            else
                DrawChallenge(content,state);

            GUI.matrix=previousMatrix;
        }

        void DrawResult(Rect area,bool succeeded)
        {
            Color old=result.normal.textColor;
            result.normal.textColor=succeeded?new Color(.58f,.84f,.76f):new Color(1f,.62f,.46f);
            GUI.Label(area,succeeded?"균형 유지 성공":"균형을 잃었습니다",result);
            result.normal.textColor=old;
        }

        void DrawChallenge(Rect area,BalanceStateSnapshot state)
        {
            Rect keyRect=new Rect(area.x+8,area.y+8,34,30);
            Fill(keyRect,accent);
            GUI.Label(keyRect,DirectionLabel(state.RequiredInput),key);
            string instruction=state.Pattern==BalanceChallengePattern.CounterTap?"표시 키를 연속 입력":
                state.Pattern==BalanceChallengePattern.CenterGauge?"표시 방향으로 중앙 유지":"노란 구간에서 표시 키 입력";
            GUI.Label(new Rect(area.x+51,area.y+5,area.width-59,28),instruction,label);

            Rect track=new Rect(area.x+51,area.y+39,area.width-61,9);
            Fill(track,new Color(.055f,.07f,.067f,.9f));
            if(state.Pattern==BalanceChallengePattern.CounterTap) {
                Fill(new Rect(track.x,track.y,track.width*state.Progress,track.height),new Color(.48f,.72f,.68f,.95f));
                return;
            }

            float safeMin=Mathf.Clamp01((state.TargetValue-state.SafeZoneHalfWidth+1)*.5f);
            float safeMax=Mathf.Clamp01((state.TargetValue+state.SafeZoneHalfWidth+1)*.5f);
            Fill(new Rect(track.x+track.width*safeMin,track.y,track.width*(safeMax-safeMin),track.height),new Color(accent.r,accent.g,accent.b,.72f));
            float indicator=Mathf.Clamp01((state.IndicatorValue+1)*.5f);
            Fill(new Rect(track.x+track.width*indicator-2,track.y-4,4,track.height+8),new Color(.92f,.96f,.94f,.98f));
        }

        static string DirectionLabel(BalanceInputDirection direction)
        {
            switch(direction) {
                case BalanceInputDirection.Up: return "W";
                case BalanceInputDirection.Left: return "A";
                case BalanceInputDirection.Down: return "S";
                case BalanceInputDirection.Right: return "D";
                default: return "!";
            }
        }

        static void Fill(Rect rect,Color color)
        {
            Color old=GUI.color;
            GUI.color=color;
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=old;
        }

        static void Outline(Rect rect,float width,Color color)
        {
            Fill(new Rect(rect.x,rect.y,rect.width,width),color);
            Fill(new Rect(rect.x,rect.yMax-width,rect.width,width),color);
            Fill(new Rect(rect.x,rect.y,width,rect.height),color);
            Fill(new Rect(rect.xMax-width,rect.y,width,rect.height),color);
        }
    }
}
