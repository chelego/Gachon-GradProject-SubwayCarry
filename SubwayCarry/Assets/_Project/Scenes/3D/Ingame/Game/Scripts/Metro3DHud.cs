using System;
using SubwayCarry.Prototype.ArtMapSlice;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DHud : MonoBehaviour
    {
        public Metro3DWorld world;
        readonly SliceMetroMap map=new SliceMetroMap();
        GUIStyle small,normal,title,button,interaction,interactionSelected,interactionHeader,interactionKey,interactionCount,interactionHint,staminaLabel,staminaValue;
        GUIStyle phoneStatus,phoneStatusRight,phoneAppTitle,phoneAppSubtitle,phoneSection,phoneOrderTitle,phoneMeta,phoneButtonLabel,phoneButtonLabelDark,phoneButtonDisabled,phoneNotice;
        readonly Color panel=new Color(.05f,.065f,.067f,.92f),accent=new Color(.95f,.73f,.21f);
        readonly Color interactionPanel=new Color(.035f,.047f,.047f,.68f),interactionRow=new Color(.14f,.155f,.145f,.72f);
        float phoneOpenProgress;
        void Styles()
        {
            if(normal!=null) return;
            normal=new GUIStyle(GUI.skin.label) {font=world.settings.font,fontSize=18,normal={textColor=new Color(.88f,.9f,.88f)}};
            small=new GUIStyle(normal) {fontSize=14}; title=new GUIStyle(normal) {fontSize=25,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button) {font=world.settings.font,fontSize=17,padding=new RectOffset(12,12,10,10)};
            interaction=new GUIStyle(normal) {fontSize=13,alignment=TextAnchor.MiddleLeft};
            interaction.normal.textColor=new Color(.78f,.81f,.79f,.94f);
            interactionSelected=new GUIStyle(interaction) {fontStyle=FontStyle.Bold}; interactionSelected.normal.textColor=Color.white;
            interactionHeader=new GUIStyle(small) {fontSize=10,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft};
            interactionHeader.normal.textColor=new Color(.8f,.83f,.81f,.9f);
            interactionKey=new GUIStyle(title) {fontSize=14,alignment=TextAnchor.MiddleCenter}; interactionKey.normal.textColor=new Color(.08f,.075f,.045f);
            interactionCount=new GUIStyle(small) {fontSize=10,alignment=TextAnchor.MiddleCenter}; interactionCount.normal.textColor=new Color(.88f,.9f,.88f,.92f);
            interactionHint=new GUIStyle(small) {fontSize=9,alignment=TextAnchor.MiddleRight}; interactionHint.normal.textColor=new Color(.72f,.76f,.74f,.85f);
            staminaLabel=new GUIStyle(small) {fontSize=10,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft}; staminaLabel.normal.textColor=new Color(.78f,.82f,.80f);
            staminaValue=new GUIStyle(small) {fontSize=10,alignment=TextAnchor.MiddleRight,wordWrap=false,clipping=TextClipping.Clip}; staminaValue.normal.textColor=new Color(.69f,.82f,.79f);
            phoneStatus=new GUIStyle(small) {fontSize=10,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft}; phoneStatus.normal.textColor=new Color(.78f,.83f,.81f);
            phoneStatusRight=new GUIStyle(phoneStatus) {alignment=TextAnchor.MiddleRight};
            phoneAppTitle=new GUIStyle(title) {fontSize=16,alignment=TextAnchor.MiddleLeft};
            phoneAppSubtitle=new GUIStyle(small) {fontSize=9,alignment=TextAnchor.MiddleLeft}; phoneAppSubtitle.normal.textColor=new Color(.58f,.64f,.62f);
            phoneSection=new GUIStyle(small) {fontSize=11,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft}; phoneSection.normal.textColor=new Color(.66f,.72f,.70f);
            phoneOrderTitle=new GUIStyle(normal) {fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft};
            phoneMeta=new GUIStyle(small) {fontSize=11,alignment=TextAnchor.MiddleLeft}; phoneMeta.normal.textColor=new Color(.68f,.73f,.71f);
            phoneButtonLabel=new GUIStyle(small) {fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter}; phoneButtonLabel.normal.textColor=new Color(.9f,.92f,.9f);
            phoneButtonLabelDark=new GUIStyle(phoneButtonLabel); phoneButtonLabelDark.normal.textColor=new Color(.08f,.075f,.045f);
            phoneButtonDisabled=new GUIStyle(phoneButtonLabel); phoneButtonDisabled.normal.textColor=new Color(.38f,.42f,.4f);
            phoneNotice=new GUIStyle(small) {fontSize=10,alignment=TextAnchor.MiddleCenter,wordWrap=false,clipping=TextClipping.Clip}; phoneNotice.normal.textColor=new Color(.68f,.73f,.71f);
        }
        void Fill(Rect r,Color color) { var old=GUI.color; GUI.color=color; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old; }
        void Outline(Rect r,float width,Color color) { Fill(new Rect(r.x,r.y,r.width,width),color); Fill(new Rect(r.x,r.yMax-width,r.width,width),color); Fill(new Rect(r.x,r.y,width,r.height),color); Fill(new Rect(r.xMax-width,r.y,width,r.height),color); }
        void Panel(Rect r) { var old=GUI.color; GUI.color=panel; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old; }
        void Bar(Rect r,float ratio,Color color) { var old=GUI.color; GUI.color=new Color(.25f,.28f,.29f); GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=color; GUI.DrawTexture(new Rect(r.x,r.y,r.width*Mathf.Clamp01(ratio),r.height),Texture2D.whiteTexture); GUI.color=old; }
        void Update()
        {
            if(!UsesPersonalHud()) return;
            float target=world.session!=null&&world.session.PhoneOpen?1:0;
            phoneOpenProgress=Mathf.MoveTowards(phoneOpenProgress,target,Time.unscaledDeltaTime*4.5f);
        }
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
                if((!world.player.UsesPersonalBalance||!world.player.PersonalBalance.ConsumesMovementInput)&&world.player.Using==null&&world.player.Target!=null) {
                    if(world.player.InteractionSelector!=null&&world.player.InteractionSelector.isActiveAndEnabled) DrawInteractionOptions(world.player.InteractionSelector);
                    else DrawLegacyInteractionPrompt(scale);
                }
                if(world.player.BalanceActive) { Panel(new Rect(562,460,156,72)); GUI.Label(new Rect(586,476,130,40),world.player.BalanceKey+"  균형",title); }
                DrawStaminaBar(world.player.Stamina/(100+s.StaminaLevel*20));
            }
            if(UsesPersonalHud()) { if(s.PhoneOpen||phoneOpenProgress>.001f) DrawPhone(); }
            else if(s.PhoneOpen) DrawPhone();
            if(s.ResultOpen) DrawResult();
            if(s.Paused&&!s.PhoneOpen) { Panel(new Rect(470,260,340,180)); GUI.Label(new Rect(500,285,280,35),"일시정지",title); GUI.Label(new Rect(500,340,280,30),"ESC · 계속하기",normal); }
            if(s.DamageRemaining>0) DrawDamage();
        }
        void DrawInteractionOptions(Metro3DInteractionSelector selector)
        {
            int count=selector.OptionCount;
            if(count<=0) return;
            Matrix4x4 previousMatrix=GUI.matrix; GUI.matrix=Matrix4x4.identity;
            const float headerHeight=20,rowHeight=30;
            float footerHeight=count>1?18:0;
            float width=Mathf.Clamp(Screen.width*.27f,220,248),x=CompactHudLeft();
            float height=headerHeight+count*rowHeight+footerHeight;
            float y=StaminaFrameRect().y-6-height;
            Rect frame=new Rect(x,y,width,height);
            Fill(new Rect(frame.x+2,frame.y+3,frame.width,frame.height),new Color(0,0,0,.2f));
            Fill(frame,interactionPanel); Outline(frame,1,new Color(.42f,.46f,.44f,.46f));
            Fill(new Rect(frame.x,frame.y,frame.width,1),new Color(accent.r,accent.g,accent.b,.9f));
            Fill(new Rect(x+10,y+8,4,4),accent);
            GUI.Label(new Rect(x+21,y,width-29,19),"상호작용",interactionHeader);
            for(int i=0;i<count;i++) {
                var option=selector.Options[i];
                bool selected=i==selector.SelectedIndex;
                float rowY=y+headerHeight+i*rowHeight;
                Rect row=new Rect(x+7,rowY+2,width-14,rowHeight-4);
                if(selected) { Fill(row,interactionRow); Fill(new Rect(row.x,row.y,2,row.height),accent); }
                Rect keyRect=new Rect(x+15,rowY+5,25,20);
                if(selected) { Fill(keyRect,accent); GUI.Label(keyRect,"E",interactionKey); }
                else Outline(keyRect,1,new Color(.48f,.52f,.5f,.65f));
                float labelWidth=option.CandidateCount>1?width-108:width-57;
                GUI.Label(new Rect(x+49,rowY+2,labelWidth,rowHeight-4),option.Label,selected?interactionSelected:interaction);
                if(option.CandidateCount>1) {
                    Rect countRect=new Rect(x+width-50,rowY+6,39,18);
                    Fill(countRect,new Color(.08f,.095f,.09f,.62f)); Outline(countRect,1,new Color(.42f,.47f,.44f,.58f));
                    GUI.Label(countRect,option.CandidateCount+"곳",interactionCount);
                }
            }
            if(count>1) {
                float footerY=y+headerHeight+count*rowHeight;
                Fill(new Rect(x+10,footerY,width-20,1),new Color(.4f,.44f,.42f,.45f));
                GUI.Label(new Rect(x+14,footerY,width-28,16),"휠 선택  ·  E 실행",interactionHint);
            }
            GUI.matrix=previousMatrix;
        }
        float CompactHudLeft() { return Mathf.Max(16,Screen.width*.025f); }
        Rect StaminaFrameRect()
        {
            float width=Mathf.Clamp(Screen.width*.25f,220,260),height=24;
            return new Rect(CompactHudLeft(),Screen.height-Mathf.Max(14,Screen.height*.025f)-height,width,height);
        }
        void DrawStaminaBar(float ratio)
        {
            Matrix4x4 previousMatrix=GUI.matrix; GUI.matrix=Matrix4x4.identity;
            ratio=Mathf.Clamp01(ratio); Rect frame=StaminaFrameRect();
            Fill(new Rect(frame.x+3,frame.y+4,frame.width,frame.height),new Color(0,0,0,.3f));
            Fill(frame,new Color(.035f,.047f,.047f,.94f)); Outline(frame,1,new Color(.34f,.39f,.37f,.8f));
            Fill(new Rect(frame.x,frame.y,3,frame.height),new Color(.56f,.76f,.72f));
            GUI.Label(new Rect(frame.x+10,frame.y+1,55,21),"STAMINA",staminaLabel);
            Rect track=new Rect(frame.x+67,frame.y+8,frame.width-116,8);
            Fill(track,new Color(.12f,.15f,.145f,.95f));
            Rect fill=new Rect(track.x,track.y,track.width*ratio,track.height);
            Fill(fill,new Color(.48f,.72f,.68f));
            if(fill.width>0) Fill(new Rect(fill.x,fill.y,fill.width,2),new Color(.72f,.88f,.84f,.9f));
            for(int i=1;i<5;i++) Fill(new Rect(track.x+track.width*i/5f,track.y,1,track.height),new Color(.035f,.047f,.047f,.7f));
            GUI.Label(new Rect(frame.xMax-45,frame.y+1,36,21),Mathf.RoundToInt(ratio*100)+"%",staminaValue);
            GUI.matrix=previousMatrix;
        }
        void DrawLegacyInteractionPrompt(float scale)
        {
            Vector3 screen=world.player.view.WorldToScreenPoint(world.player.Target.transform.position+Vector3.up*.5f);
            if(screen.z<=0) return;
            Vector2 margin=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f);
            Vector2 p=(new Vector2(screen.x,Screen.height-screen.y)-margin)/scale;
            Panel(new Rect(Mathf.Clamp(p.x+32,40,1200),Mathf.Clamp(p.y-20,130,630),38,36));
            GUI.Label(new Rect(Mathf.Clamp(p.x+40,48,1208),Mathf.Clamp(p.y-17,133,633),25,30),"E",normal);
        }
        void DrawPhone()
        {
            if(UsesPersonalHud()) { DrawHandheldPhone(); return; }
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
        void DrawHandheldPhone()
        {
            var s=world.session; var c=world.settings.deliveries;
            Matrix4x4 previousMatrix=GUI.matrix; GUI.matrix=Matrix4x4.identity;
            float eased=1-Mathf.Pow(1-Mathf.Clamp01(phoneOpenProgress),3);
            Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.01f,.015f,.015f,.13f*eased));

            float phoneScale=Mathf.Min((Screen.width-24)/480f,(Screen.height-16)/694f);
            phoneScale=Mathf.Clamp(phoneScale,.58f,1f);
            float phoneWidth=480*phoneScale,phoneHeight=694*phoneScale;
            float phoneX=(Screen.width-phoneWidth)*.5f;
            float openY=Mathf.Max(8,(Screen.height-phoneHeight)*.5f);
            float phoneY=Mathf.Lerp(Screen.height+24,openY,eased);
            Rect phone=new Rect(phoneX,phoneY,phoneWidth,phoneHeight);
            Rect shadow=PhoneRect(phone,phoneScale,12,11,480,694);
            RoundedFill(shadow,18*phoneScale,new Color(0,0,0,.38f*eased));
            RoundedFill(phone,18*phoneScale,new Color(.012f,.017f,.018f,.99f));
            Outline(PhoneRect(phone,phoneScale,7,7,466,680),1,new Color(.28f,.31f,.3f,.9f));
            Fill(PhoneRect(phone,phoneScale,-4,128,4,68),new Color(.08f,.09f,.09f,.98f));
            Fill(PhoneRect(phone,phoneScale,480,154,4,42),new Color(.08f,.09f,.09f,.98f));

            Rect screen=PhoneRect(phone,phoneScale,21,32,438,632);
            Fill(screen,new Color(.045f,.057f,.057f,.995f));
            Fill(PhoneRect(phone,phoneScale,193,14,94,5),new Color(.16f,.18f,.18f,.98f));
            Fill(PhoneRect(phone,phoneScale,297,13,7,7),new Color(.11f,.19f,.18f,.98f));

            GUI.Label(PhoneRect(phone,phoneScale,33,33,90,18),DateTime.Now.ToString("HH:mm"),phoneStatus);
            GUI.Label(PhoneRect(phone,phoneScale,299,33,148,18),"LTE  82%",phoneStatusRight);
            Fill(PhoneRect(phone,phoneScale,21,53,438,1),new Color(.22f,.27f,.25f,.72f));

            Rect appIcon=PhoneRect(phone,phoneScale,34,65,29,29);
            Fill(appIcon,accent); GUI.Label(appIcon,"S",phoneButtonLabelDark);
            GUI.Label(PhoneRect(phone,phoneScale,73,60,245,25),"SUBWAY CARRY",phoneAppTitle);
            GUI.Label(PhoneRect(phone,phoneScale,74,83,245,16),"DELIVERY SERVICE",phoneAppSubtitle);
            GUI.Label(PhoneRect(phone,phoneScale,329,64,118,28),"TAB · 닫기",phoneStatusRight);

            var order=s.Order??c.orders[s.SelectedIndex]; SliceRouteHighlight route=null;
            if(c.routeHighlights!=null) foreach(var r in c.routeHighlights) if(r.orderId==order.id) route=r;
            Rect mapRect=PhoneRect(phone,phoneScale,21,104,438,214);
            bool previousEnabled=GUI.enabled;
            GUI.enabled=previousEnabled&&s.PhoneOpen&&phoneOpenProgress>.92f;
            map.Draw(mapRect,c.metroMapImage,route,small,true);
            GUI.enabled=previousEnabled;
            Outline(mapRect,1,new Color(.32f,.37f,.35f,.9f));

            Rect orderCard=PhoneRect(phone,phoneScale,30,326,420,54);
            Fill(orderCard,new Color(.085f,.105f,.102f,.98f));
            Fill(new Rect(orderCard.x,orderCard.y,3,orderCard.height),accent);
            GUI.Label(PhoneRect(phone,phoneScale,42,329,396,24),order.title,phoneOrderTitle);
            GUI.Label(PhoneRect(phone,phoneScale,42,353,396,20),"보수 ₩"+order.fee.ToString("N0")+"   ·   교통비 ₩"+order.outboundFare.ToString("N0"),phoneMeta);

            GUI.Label(PhoneRect(phone,phoneScale,31,386,418,18),"배송 선택",phoneSection);
            for(int i=0;i<Mathf.Min(c.orders.Length,8);i++) {
                int column=i%2,row=i/2;
                Rect orderButton=PhoneRect(phone,phoneScale,30+column*214,407+row*30,205,26);
                bool enabled=!s.HasPackage&&i<s.Unlocked;
                if(PhoneButton(orderButton,i<s.Unlocked?c.orders[i].title:"잠김",enabled,false)) { s.Select(i); map.ResetView(); }
            }

            Rect acceptRect=PhoneRect(phone,phoneScale,30,531,205,39);
            Rect insuranceRect=PhoneRect(phone,phoneScale,244,531,206,39);
            if(PhoneButton(acceptRect,s.HasPackage?"배송 계속하기":"배송 수락",true,true)) s.Accept();
            if(PhoneButton(insuranceRect,"보험 ₩"+c.insurancePrice.ToString("N0")+(s.Insurance?" · 적용":""),true,false)) s.Purchase(3);

            GUI.Label(PhoneRect(phone,phoneScale,31,578,418,18),"학교 지원",phoneSection);
            if(PhoneButton(PhoneRect(phone,phoneScale,30,600,134,31),"체력 · "+s.StaminaLevel,true,false)) s.Purchase(0);
            if(PhoneButton(PhoneRect(phone,phoneScale,173,600,134,31),"균형 · "+s.BalanceLevel,true,false)) s.Purchase(1);
            if(PhoneButton(PhoneRect(phone,phoneScale,316,600,134,31),"이동 · "+s.AgilityLevel,true,false)) s.Purchase(2);
            GUI.Label(PhoneRect(phone,phoneScale,31,635,418,22),s.Notice,phoneNotice);
            Fill(PhoneRect(phone,phoneScale,186,677,108,4),new Color(.64f,.68f,.66f,.82f));
            GUI.matrix=previousMatrix;
        }
        static Rect PhoneRect(Rect phone,float scale,float x,float y,float width,float height)
        {
            return new Rect(phone.x+x*scale,phone.y+y*scale,width*scale,height*scale);
        }
        bool PhoneButton(Rect rect,string label,bool enabled,bool primary)
        {
            bool interactive=enabled&&world.session.PhoneOpen&&phoneOpenProgress>.92f;
            Color background=primary?accent:new Color(.12f,.145f,.14f,.98f);
            if(!enabled) background=new Color(.065f,.075f,.073f,.96f);
            else if(interactive&&rect.Contains(Event.current.mousePosition)) background=Color.Lerp(background,Color.white,.1f);
            Fill(rect,background); Outline(rect,1,primary?new Color(1,.85f,.4f,.9f):new Color(.3f,.35f,.33f,.85f));
            bool previousEnabled=GUI.enabled; GUI.enabled=interactive;
            bool pressed=GUI.Button(rect,GUIContent.none,GUIStyle.none);
            GUI.enabled=previousEnabled;
            GUI.Label(rect,label,!enabled?phoneButtonDisabled:primary?phoneButtonLabelDark:phoneButtonLabel);
            return pressed;
        }
        void RoundedFill(Rect rect,float radius,Color color)
        {
            float edge=Mathf.Clamp(radius,1,Mathf.Min(rect.width,rect.height)*.5f);
            Fill(new Rect(rect.x+edge,rect.y,rect.width-edge*2,rect.height),color);
            Fill(new Rect(rect.x,rect.y+edge,rect.width,rect.height-edge*2),color);
            Fill(new Rect(rect.x+edge*.35f,rect.y+edge*.35f,rect.width-edge*.7f,rect.height-edge*.7f),color);
        }
        bool UsesPersonalHud()
        {
            return world!=null&&world.player!=null&&world.player.InteractionSelector!=null;
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
