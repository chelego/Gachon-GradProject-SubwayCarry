using UnityEngine;
using UnityEngine.InputSystem;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Lightweight review tools only. Hover never pauses simulation or captures the NPC input.
    public sealed class AnimationLabReviewMenu : MonoBehaviour
    {
        AnimationLabPhoneCase owner;bool sources;float leaveAt;GUIStyle text,button,title;
        public bool Open {get;private set;}
        public Rect Header=>new Rect(Screen.width-260,14,244,34);
        public Rect Panel=>new Rect(Screen.width-310,48,294,sources?Mathf.Min(Screen.height-60,650):304);
        public static bool PointerOverMenu {get;private set;}
        Vector2 scroll;
        public void Initialize(AnimationLabPhoneCase value){owner=value;}
        public void HoverAt(Vector2 point,float now)
        {
            bool inside=Header.Contains(point)||(Open&&Panel.Contains(point));
            if(inside){Open=true;leaveAt=now+.22f;}else if(now>leaveAt){Open=false;sources=false;}
            PointerOverMenu=inside;
        }
        void Update(){if(Mouse.current!=null){var p=Mouse.current.position.ReadValue();HoverAt(new Vector2(p.x,Screen.height-p.y),Time.unscaledTime);}}
        public void Draw()
        {
            if(owner==null||owner.Data==null)return;
            if(text==null){text=new GUIStyle(GUI.skin.label){font=owner.Data.font,fontSize=13,wordWrap=true};button=new GUIStyle(GUI.skin.button){font=owner.Data.font,fontSize=14,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(12,8,6,6)};title=new GUIStyle(text){fontSize=15,fontStyle=FontStyle.Bold};}
            GUI.Box(Header,GUIContent.none);GUI.Label(new Rect(Header.x+12,Header.y+7,220,24),"동작 검수   "+(Open?"▴":"▾"),title);
            if(!Open)return;var r=Panel;GUI.Box(r,GUIContent.none);float y=r.y+10;
            if(sources)
            {
                if(GUI.Button(new Rect(r.x+10,y,r.width-20,28),"← 동작 선택",button)){sources=false;return;}y+=34;
                var entries=owner.Data.squeeze;float height=32*(owner.Data.actions.Length+entries.Length+1);
                scroll=GUI.BeginScrollView(new Rect(r.x+8,y,r.width-16,r.height-50),scroll,new Rect(0,0,r.width-38,height));float row=0;
                Source(owner.Data.displayName,owner.Data.authored,ref row,r.width-42);
                foreach(var e in owner.Data.actions)if(e!=null)Source(e.displayName,e.authored,ref row,r.width-42);
                foreach(var e in entries)if(e!=null&&e.authored!=null)Source(e.authored.clipName,e.authored,ref row,r.width-42);
                GUI.EndScrollView();return;
            }
            string[] names={"자동 · 걷기와 폰 보기","주머니에 넣기","꺼내서 한 손으로 보기","양손으로 가로 잡기","손에 들고 걷기","사람 사이 비집고 지나가기"};
            for(int i=0;i<names.Length;i++){if(GUI.Button(new Rect(r.x+10,y,r.width-20,30),(owner.ReviewMode==i?"●  ":"    ")+names[i],button))owner.SetReviewMode(i);y+=33;}
            if(GUI.Button(new Rect(r.x+10,y,r.width-20,28),"편집 원본 ▸",button))sources=true;y+=34;
            GUI.Label(new Rect(r.x+14,y,r.width-28,55),owner.Caption,text);
        }
        void Source(string label,AnimationLabAuthoredMotion source,ref float y,float width)
        {
            if(GUI.Button(new Rect(0,y,width,29),label.Replace("_"," "),button))
            {
#if UNITY_EDITOR
                if(source!=null){UnityEditor.Selection.activeObject=source;UnityEditor.EditorGUIUtility.PingObject(source);}
#endif
            }
            y+=32;
        }
        void OnDisable(){PointerOverMenu=false;Open=false;}
    }
}
