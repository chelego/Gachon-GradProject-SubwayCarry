#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace SubwayCarry.Transit.Editor
{
    // Non-destructive visual pass: keeps gameplay objects, colliders and transfer links.
    public static class StationVisualPolish
    {
        const string Art="Assets/_Project/Art/Sprites/StationExpansion";
        static Color C(string s) { ColorUtility.TryParseHtmlString("#"+s,out var c); return c; }
        static Vector2 P(float u,float v) => new Vector2((u-v)*2.45f,(u+v)*1.225f);
        static Vector2 UV(Vector3 p) => new Vector2(p.x/4.9f+p.y/2.45f,p.y/2.45f-p.x/4.9f);
        sealed class Surface {
            public List<Vector3> vertices=new List<Vector3>();
            public List<int> indices=new List<int>();
            public List<Color> colors=new List<Color>();
            public void Tile(float u,float v,float x,float y,Color c) {
                int n=vertices.Count; vertices.Add(P(u,v));vertices.Add(P(x,v));vertices.Add(P(x,y));vertices.Add(P(u,y));
                c=QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;
                for(int i=0;i<4;i++)colors.Add(c);
                indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            public void Apply(MeshFilter f) {
                Mesh m=f.sharedMesh; Undo.RecordObject(m,"Refine station paving");
                m.Clear();m.SetVertices(vertices);m.SetTriangles(indices,0);m.SetColors(colors);m.RecalculateBounds();
                EditorUtility.SetDirty(m);
            }
        }
        public static void ApplyCurrent() {
            var layout=UnityEngine.Object.FindFirstObjectByType<StationSceneLayout>();
            if(layout==null)throw new InvalidOperationException("Open Suseo or Bokjeong first.");
            bool suseo=layout.name.StartsWith("Suseo");
            for(int level=0;level<layout.Levels.Length;level++) {
                var root=layout.Levels[level].geometry.transform;
                bool hall=suseo&&level==1; int length=hall?9:suseo?14:12, width=hall?5:4;
                foreach(var f in root.GetComponentsInChildren<MeshFilter>(true)) {
                    if(f.name=="Seamless stone floor") Floor(f,length,width,suseo);
                    if(f.name=="Station-specific stone inlays") Inlays(f,length,suseo);
                    if(f.name=="Continuous flush tactile strip") Tactile(f,length);
                }
                if(hall) {
                    foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(x=>x.name.EndsWith(" floor route"))) {
                        var col=f.sharedMesh.colors; for(int i=0;i<col.Length;i++) col[i]=Color.Lerp(col[i],C("A29C8A").linear,.35f);
                        Undo.RecordObject(f.sharedMesh,"Soften transfer path"); f.sharedMesh.colors=col; EditorUtility.SetDirty(f.sharedMesh);
                    }
                }
                var signs=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Sign · ")).ToArray();
                for(int i=0;i<signs.Length;i++) {
                    Vector2 pos;
                    float w=4.5f;
                    if(!hall) {
                        float[] us=suseo?new[]{2.8f,6.8f,10.5f}:new[]{2.1f,5.35f,8.15f,10.65f};
                        pos=P(us[Math.Min(i,us.Length-1)],.85f)+Vector2.up*1.95f;
                        if(i==3)w=3.65f;
                    } else {
                        float[] us={3.2f,1.1f,7.1f,7.1f,7.1f};
                        float[] vs={3.2f,1.4f,.45f,1.9f,3.35f};
                        pos=P(us[Math.Min(i,4)],vs[Math.Min(i,4)])+Vector2.up*.7f;
                        w=i<2?3.7f:3.25f;
                    }
                    Sign(signs[i],pos,w,2600+i*20,length,width);
                }
                // The suspended transfer sign already labels the elevator.
                foreach(var text in root.GetComponentsInChildren<TextMesh>(true).Where(t=>t.text=="ELEVATOR"))
                    text.gameObject.SetActive(false);
            }
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(layout.gameObject.scene);
        }
        static void Floor(MeshFilter f,int length,int width,bool suseo) {
            var s=new Surface(); s.Tile(-.5f,-.5f,length-.5f,width-.5f,C("827F75"));
            for(int u=0;u<length*4;u++)for(int v=0;v<width*4;v++) {
                float x=-.5f+u*.25f,y=-.5f+v*.25f;
                var c=C(suseo?"BDBFB7":"C4BFB0");
                float tone=.97f+((u*13+v*7)%7)*.008f;
                c*=new Color(tone,tone,tone,1);
                s.Tile(x+.006f,y+.006f,x+.244f,y+.244f,c);
                s.Tile(x+.008f,y+.008f,x+.24f,y+.016f,Color.Lerp(c,Color.white,.16f));
                s.Tile(x+.008f,y+.016f,x+.016f,y+.24f,Color.Lerp(c,Color.white,.10f));
                // Tiny stone grains on alternate slabs, at the same visual scale as existing sprites.
                if((u+v)%3==0) s.Tile(x+.10f,y+.14f,x+.116f,y+.15f,Color.Lerp(c,C("858779"),.23f));
            }
            // Inset stone border finishes the platform without competing with tactile yellow.
            var border=C(suseo?"758588":"918574");
            s.Tile(-.42f,-.42f,length-.58f,-.34f,border);
            s.Tile(-.42f,-.34f,-.34f,width-.58f,border);
            s.Tile(length-.66f,-.34f,length-.58f,width-.58f,border);
            s.Apply(f);
        }
        static void Inlays(MeshFilter f,int length,bool suseo) {
            var s=new Surface(); Color edge=C(suseo?"72858A":"817C70"), inner=C(suseo?"CAD0C9":"D7D0BC");
            for(float u=1;u<length-1;u+=1.5f)foreach(float v in new[]{.5f,2.25f}) {
                s.Tile(u-.15f,v-.15f,u+.15f,v+.15f,edge);
                s.Tile(u-.09f,v-.09f,u+.09f,v+.09f,inner);
                s.Tile(u-.035f,v-.035f,u+.035f,v+.035f,edge);
            }
            s.Apply(f);
        }
        static void Tactile(MeshFilter f,int length) {
            var uv=UV(f.sharedMesh.bounds.center);float v=uv.y;
            var s=new Surface();s.Tile(-.5f,v-.17f,length-.5f,v+.17f,C("96731C"));
            for(int i=0;i<length*4;i++) {
                float u=-.5f+i*.25f;
                s.Tile(u+.007f,v-.163f,u+.243f,v+.163f,C("E2B637"));
                s.Tile(u+.014f,v-.153f,u+.237f,v-.137f,C("F5D05A"));
                for(int a=0;a<3;a++)for(int b=0;b<4;b++) {
                    float x=u+.05f+a*.075f,y=v-.112f+b*.075f;
                    s.Tile(x-.015f,y-.014f,x+.02f,y+.021f,C("A67E21"));
                    s.Tile(x-.013f,y-.007f,x+.009f,y+.016f,C("FFE484"));
                }
            }
            s.Apply(f);
        }
        static void Sign(Transform t,Vector2 position,float width,int order,int length,int depth) {
            Undo.RecordObject(t,"Place suspended station sign");
            t.localPosition=position;t.localScale=Vector3.one;t.localRotation=Quaternion.Euler(0,0,26.565f);
            var texts=t.GetComponentsInChildren<TextMesh>(true);
            var accent=t.Find("Route color").GetComponent<SpriteRenderer>().color;
            Rect(t.Find("Dark frame"),Vector2.zero,new Vector2(width+.1f,1.04f),C("27353A"),order);
            Rect(t.Find("Ivory face"),new Vector2(0,.025f),new Vector2(width,.91f),C("E9E5D6"),order+1);
            Rect(t.Find("Route color"),new Vector2(0,-.43f),new Vector2(width,.10f),accent,order+2);
            for(int i=0;i<texts.Length;i++) {
                var tx=texts[i];Undo.RecordObject(tx,"Fit sign lettering");Undo.RecordObject(tx.transform,"Align sign lettering");
                tx.transform.localRotation=Quaternion.identity;tx.transform.localScale=Vector3.one;
                tx.transform.localPosition=new Vector3(0,i==0?.18f:-.16f,-.02f);
                tx.characterSize=(i==0?.40f:.19f)/6.4f;
                tx.color=C("26343C").linear;
                var r=tx.GetComponent<MeshRenderer>();Undo.RecordObject(r,"Sign order");r.sortingOrder=order+3;
                float limit=width-.24f;
                if(r.bounds.size.x>limit)tx.characterSize*=limit/r.bounds.size.x;
            }
            foreach(string name in new[]{"Ceiling rail","Hanger L","Hanger R","Frame highlight"}) {
                var old=t.Find(name);if(old)Undo.DestroyObjectImmediate(old.gameObject);
            }
            AddRect(t,"Ceiling rail",new Vector2(0,1.15f),new Vector2(width+.35f,.13f),C("596768"),order-3);
            AddRect(t,"Hanger L",new Vector2(-width*.34f,.82f),new Vector2(.045f,.64f),C("8D9997"),order-2);
            AddRect(t,"Hanger R",new Vector2(width*.34f,.82f),new Vector2(.045f,.64f),C("8D9997"),order-2);
            AddRect(t,"Frame highlight",new Vector2(0,.51f),new Vector2(width,.025f),C("B5BDB6"),order+2);
            // Keep the complete rendered sign and suspension inside the station's ground outline.
            Vector2 lo=new Vector2(float.MaxValue,float.MaxValue),hi=new Vector2(float.MinValue,float.MinValue);
            foreach(var r in t.GetComponentsInChildren<Renderer>(true)) {
                var b=r.bounds; foreach(float x in new[]{b.min.x,b.max.x})foreach(float y in new[]{b.min.y,b.max.y}) {
                    var uv=UV(t.parent.InverseTransformPoint(new Vector3(x,y,0)));lo=Vector2.Min(lo,uv);hi=Vector2.Max(hi,uv);
                }
            }
            Vector2 delta=Vector2.zero;
            if(lo.x<-.35f)delta.x=-.35f-lo.x;if(hi.x+delta.x>length-.65f)delta.x=length-.65f-hi.x;
            if(lo.y<-.35f)delta.y=-.35f-lo.y;if(hi.y+delta.y>depth-.65f)delta.y=depth-.65f-hi.y;
            t.localPosition+=(Vector3)P(delta.x,delta.y);
        }
        static void Rect(Transform t,Vector2 p,Vector2 size,Color color,int order) {
            Undo.RecordObject(t,"Style sign panel");var r=t.GetComponent<SpriteRenderer>();Undo.RecordObject(r,"Style sign panel");
            t.localPosition=p;t.localRotation=Quaternion.identity;t.localScale=new Vector3(size.x/r.sprite.bounds.size.x,size.y/r.sprite.bounds.size.y,1);
            r.color=color;r.sortingOrder=order;
        }
        static void AddRect(Transform parent,string name,Vector2 p,Vector2 size,Color color,int order) {
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Add ceiling suspension");go.transform.SetParent(parent,false);
            var r=go.AddComponent<SpriteRenderer>();var source=parent.Find("Dark frame").GetComponent<SpriteRenderer>();r.sprite=source.sprite;r.sharedMaterial=source.sharedMaterial;
            Rect(go.transform,p,size,color,order);
        }
    }
}
#endif