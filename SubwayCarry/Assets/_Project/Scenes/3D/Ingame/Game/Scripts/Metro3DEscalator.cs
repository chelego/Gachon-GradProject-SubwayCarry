using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DEscalator : MonoBehaviour
    {
        public Metro3DWorld world;
        public Vector3 uphill; // Existing serialized contract; new assets use the profile below.
        public int direction=1;
        public const float Length=11.0932f,Lower=2.239f,Run=6.2352f,Rise=3.6f;
        Transform[] steps;
        float[] arc,ySamples;
        float distance;
        Transform assembly;
        public static float Height(float y)
        {
            const float k=Rise/Run; float end=Lower+Run;
            if(y<=Lower-.5f) return 0;
            if(y<Lower+.5f) return k*(y-Lower+.5f)*(y-Lower+.5f)*.5f;
            if(y<=end-.5f) return k*(y-Lower);
            if(y<end+.5f) {float d=y-end+.5f; return Rise-k*.5f+k*(d-d*d*.5f);}
            return Rise;
        }
        void Start()
        {
            assembly=transform.parent;
            var list=new List<Transform>();
            foreach(var child in assembly.GetComponentsInChildren<Transform>())
                if(child.name.StartsWith("MovingStep_")&&child.name.Length==13) list.Add(child);
            list.Sort((a,b)=>string.CompareOrdinal(a.name,b.name)); steps=list.ToArray();
            arc=new float[160]; ySamples=new float[160];
            for(int i=0;i<160;i++) {
                float y=Length*i/159; ySamples[i]=y;
                if(i>0) arc[i]=arc[i-1]+Vector2.Distance(new Vector2(y,Height(y)),new Vector2(ySamples[i-1],Height(ySamples[i-1])));
            }
        }
        float YAt(float d)
        {
            int i=System.Array.BinarySearch(arc,d); if(i>=0) return ySamples[i];
            i=Mathf.Clamp(~i,1,159); return Mathf.Lerp(ySamples[i-1],ySamples[i],Mathf.InverseLerp(arc[i-1],arc[i],d));
        }
        void Update()
        {
            if(steps==null||steps.Length==0||world.session==null||world.session.Blocked) return;
            distance+=direction*.5f*Time.deltaTime;
            float length=arc[159],pitch=length/steps.Length;
            for(int i=0;i<steps.Length;i++) {
                float d=Mathf.Repeat(i*pitch+.44f+distance,length),y=YAt(d);
                bool visible=y>.36f&&y<Length-.36f;
                if(steps[i].gameObject.activeSelf!=visible) steps[i].gameObject.SetActive(visible);
                steps[i].position=assembly.TransformPoint(new Vector3(0,Height(y),-y));
            }
        }
        void OnTriggerStay(Collider other)
        {
            if(assembly==null||world==null||world.session==null||world.session.Blocked) return;
            var controller=other.GetComponent<CharacterController>();
            if(controller==null||!controller.enabled) return;
            var feet=assembly.InverseTransformPoint(controller.transform.position); float y=-feet.z;
            if(y<.35f||y>Length-.35f||Mathf.Abs(feet.x)>.49f||Mathf.Abs(feet.y-Height(y))>.32f) return;
            float gradient=(Height(y+.01f)-Height(y-.01f))/.02f;
            float next=Mathf.Clamp(y+direction*.5f*Time.fixedDeltaTime/Mathf.Sqrt(1+gradient*gradient),0,Length);
            controller.Move(assembly.TransformVector(new Vector3(0,Height(next)-Height(y),-(next-y))));
        }
    }
}
