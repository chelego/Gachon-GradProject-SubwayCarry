using SubwayCarry.Prototype.ArtMapSlice;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DHud : MonoBehaviour
    {
        public Metro3DWorld world;
        readonly SliceMetroMap map=new SliceMetroMap();
        GUIStyle small,normal,title,button;
        readonly Color panel=new Color(.05f,.065f,.067f,.92f),accent=new Color(.95f,.73f,.21f);
        void Styles()
        {
            if(normal!=null) return;
            normal=new GUIStyle(GUI.skin.label) {font=world.settings.font,fontSize=18,normal={textColor=new Color(.88f,.9f,.88f)}};
            small=new GUIStyle(normal) {fontSize=14}; title=new GUIStyle(normal) {fontSize=25,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button) {font=world.settings.font,fontSize=17,padding=new RectOffset(12,12,10,10)};
        }
        void Panel(Rect r) { var old=GUI.color; GUI.color=panel; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old; }
        void Bar(Rect r,float ratio,Color color) { var old=GUI.color; GUI.color=new Color(.25f,.28f,.29f); GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=color; GUI.DrawTexture(new Rect(r.x,r.y,r.width*Mathf.Clamp01(ratio),r.height),Texture2D.whiteTexture); GUI.color=old; }
        void OnGUI()
        {
            if(world.session==null||world.session.Economy==null) return; Styles(); var s=world.session;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f); GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            Panel(new Rect(24,24,365,82)); GUI.Label(new Rect(42,35,330,28),s.StationLabel,title); GUI.Label(new Rect(42,72,330,24),s.HasPackage?s.ServiceLabel:"TAB · 배달 앱",small);
            Panel(new Rect(991,24,265,s.HasPackage?135:52)); GUI.Label(new Rect(1010,35,225,30),"₩ "+s.Economy.CurrentEconomyState.CurrentCash.ToString("N0"),normal);
            if(s.HasPackage) {
                var d=world.player.package.CurrentDurability; GUI.Label(new Rect(1010,73,210,24),"케이크  "+Mathf.RoundToInt(d.CakeDurability)+"%",normal);
                Bar(new Rect(1010,104,225,5),d.CakeDurability/100,accent); GUI.Label(new Rect(1010,119,220,24),"상자 보호  "+Mathf.RoundToInt(d.BoxDurability)+"%",small);
                Bar(new Rect(1010,147,225,4),d.BoxDurability/100,new Color(.6f,.72f,.7f));
            }
            if(!s.Blocked) {
                GUI.Label(new Rect(635,351,10,20),"·",normal);
                if(world.player.Using==null&&world.player.Target!=null) {
                    Vector3 screen=world.player.view.WorldToScreenPoint(world.player.Target.transform.position+Vector3.up*.5f);
                    if(screen.z>0) {
                        Vector2 margin=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f);
                        Vector2 p=(new Vector2(screen.x,Screen.height-screen.y)-margin)/scale;
                        Panel(new Rect(Mathf.Clamp(p.x+32,40,1200),Mathf.Clamp(p.y-20,130,630),38,36));
                        GUI.Label(new Rect(Mathf.Clamp(p.x+40,48,1208),Mathf.Clamp(p.y-17,133,633),25,30),"E",normal);
                    }
                }
                if(world.player.BalanceActive) { Panel(new Rect(562,460,156,72)); GUI.Label(new Rect(586,476,130,40),world.player.BalanceKey+"  균형",title); }
                Bar(new Rect(520,690,240,4),world.player.Stamina/(100+s.StaminaLevel*20),new Color(.64f,.77f,.75f));
            }
            if(s.PhoneOpen) DrawPhone();
            if(s.ResultOpen) DrawResult();
            if(s.Paused&&!s.PhoneOpen) { Panel(new Rect(470,260,340,180)); GUI.Label(new Rect(500,285,280,35),"일시정지",title); GUI.Label(new Rect(500,340,280,30),"ESC · 계속하기",normal); }
            if(s.DamageRemaining>0) DrawDamage();
        }
        void DrawPhone()
        {
            var s=world.session; var c=world.settings.deliveries;
            Panel(new Rect(330,38,620,644)); GUI.Label(new Rect(358,59,350,38),"SUBWAY CARRY",title); GUI.Label(new Rect(785,66,135,30),"TAB · 닫기",small);
            var order=s.Order??c.orders[s.SelectedIndex]; SliceRouteHighlight route=null;
            if(c.routeHighlights!=null) foreach(var r in c.routeHighlights) if(r.orderId==order.id) route=r;
            map.Draw(new Rect(354,112,570,294),c.metroMapImage,route,small);
            GUI.Label(new Rect(356,423,560,30),order.title,normal); GUI.Label(new Rect(356,456,570,25),"보수 ₩"+order.fee.ToString("N0")+"   교통비 ₩"+order.outboundFare.ToString("N0"),small);
            for(int i=0;i<Mathf.Min(c.orders.Length,8);i++) {
                GUI.enabled=!s.HasPackage&&i<s.Unlocked;
                if(GUI.Button(new Rect(356+i%4*142,496+i/4*39,134,33),i<s.Unlocked?c.orders[i].title:"잠김",button)) { s.Select(i); map.ResetView(); }
            }
            GUI.enabled=true;
            if(GUI.Button(new Rect(354,588,282,52),s.HasPackage?"배송 계속하기":"배송 수락",button)) s.Accept();
            if(GUI.Button(new Rect(644,588,280,52),"보험 ₩"+c.insurancePrice.ToString("N0")+(s.Insurance?" ✓":""),button)) s.Purchase(3);
            Panel(new Rect(24,440,276,240)); GUI.Label(new Rect(44,458,240,30),"학교 지원",title);
            if(GUI.Button(new Rect(44,503,234,39),"체력 강화 · "+s.StaminaLevel,button)) s.Purchase(0);
            if(GUI.Button(new Rect(44,548,234,39),"균형 강화 · "+s.BalanceLevel,button)) s.Purchase(1);
            if(GUI.Button(new Rect(44,593,234,39),"이동 강화 · "+s.AgilityLevel,button)) s.Purchase(2);
            GUI.Label(new Rect(354,646,568,26),s.Notice,small);
        }
        void DrawResult()
        {
            var s=world.session; Panel(new Rect(390,190,500,350)); GUI.Label(new Rect(426,220,430,40),s.Notice,title);
            GUI.Label(new Rect(426,285,430,34),"배송 수입  ₩"+s.Settlement.Fee.ToString("N0"),normal);
            GUI.Label(new Rect(426,325,430,34),"배상금  ₩"+s.Settlement.Compensation.ToString("N0"),normal);
            GUI.Label(new Rect(426,365,430,34),"최종 손익  ₩"+s.Settlement.NetIncome.ToString("N0"),title);
            if(GUI.Button(new Rect(426,448,430,54),"가천대역으로 복귀",button)) s.ReturnToHub();
        }
        void DrawDamage()
        {
            var c=world.settings.deliveries; Panel(new Rect(390,170,500,380));
            if(c.damageBurst!=null) GUI.DrawTexture(new Rect(395,175,490,365),c.damageBurst,ScaleMode.ScaleToFit,true);
            if(c.damageAtlas!=null) {
                float box=world.player.package.CurrentDurability.BoxDurability; int stage=box>66?1:box>33?2:3;
                GUI.DrawTextureWithTexCoords(new Rect(490,252,300,220),c.damageAtlas,new Rect(stage/4f,.5f,.25f,.5f),true);
            }
        }
        void OnDestroy() { map.ReleaseRoute(); }
    }
}
