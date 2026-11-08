using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // The exported PyTorch MLP ranks local velocity candidates. Physics,
    // reserved passages, and the walking motor still validate the move.
    internal sealed class Metro3DLearnedSteering
    {
        const int InputCount=24,HiddenCount=40,ActionCount=16,NeighborCount=4;
        [System.Serializable]
        sealed class Weights
        {
            public int version,contexts,ppoTransitions;
            public string algorithm;
            public float[] inputWeights,inputBias,outputWeights,outputBias;
        }

        readonly Weights weights;
        readonly float[] inputs=new float[InputCount],hidden=new float[HiddenCount],scores=new float[ActionCount];
        readonly float[] distances=new float[NeighborCount];
        Vector3 right,forward;
        float highest,encounterBlend;
        bool encounter;

        Metro3DLearnedSteering(Weights model) {weights=model;}
        public bool GroupPolicy => weights.algorithm=="GroupPPO";
        public string Label => GroupPolicy?"군중 PPO + 규칙":weights.ppoTransitions>0?"PPO + 규칙":"보행 학습 + 규칙";

        public static Metro3DLearnedSteering Load()
        {
            var args=System.Environment.GetCommandLineArgs();
            if(System.Array.IndexOf(args,"-subwayRuleSteering")>=0)return null;
            if(System.Array.IndexOf(args,"-subwayImitationSteering")>=0)return Read("PassengerSteering",null);
            var group=System.Array.IndexOf(args,"-subwaySinglePpo")<0?Read("PassengerSteeringGroup","GroupPPO"):null;
            return group??Read("PassengerSteeringPPO","PPO")??Read("PassengerSteering",null);
        }

        static Metro3DLearnedSteering Read(string resource,string algorithm)
        {
            var asset=Resources.Load<TextAsset>(resource);
            if(asset==null)return null;
            try {
                var model=JsonUtility.FromJson<Weights>(asset.text);
                if(model==null||model.version!=1||model.contexts<=0||
                    algorithm!=null&&(model.algorithm!=algorithm||model.ppoTransitions<=0)||
                    !Valid(model.inputWeights,InputCount*HiddenCount)||!Valid(model.inputBias,HiddenCount)||
                    !Valid(model.outputWeights,HiddenCount*ActionCount)||!Valid(model.outputBias,ActionCount)) {
                    Debug.LogWarning("Passenger steering model has an invalid schema; trying the available fallback.");
                    return null;
                }
                return new Metro3DLearnedSteering(model);
            } catch(System.ArgumentException exception) {
                Debug.LogWarning("Passenger steering model could not be read: "+exception.Message);
                return null;
            }
        }

        static bool Valid(float[] values,int count)
        {
            if(values==null||values.Length!=count)return false;
            foreach(float value in values)if(float.IsNaN(value)||float.IsInfinity(value))return false;
            return true;
        }

        public void Begin(Vector3 direction,float requested,Vector3 current,int passingSide)
        {
            forward=direction;right=Vector3.Cross(Vector3.up,forward);encounter=false;encounterBlend=0;
            inputs[0]=requested;inputs[1]=Vector3.Dot(current,right);
            inputs[2]=Vector3.Dot(current,forward);inputs[3]=passingSide;
            System.Array.Clear(inputs,4,InputCount-4);
            for(int i=0;i<distances.Length;i++)distances[i]=float.PositiveInfinity;
        }

        public void Observe(Vector3 offset,Vector3 velocity,float radius)
        {
            float x=Vector3.Dot(offset,right),z=Vector3.Dot(offset,forward);
            if(z<-.5f||z>3.5f||Mathf.Abs(x)>2)return;
            float squared=x*x+z*z;
            if(squared>=distances[NeighborCount-1])return;
            float vx=Vector3.Dot(velocity,right),vz=Vector3.Dot(velocity,forward);
            Vector2 relative=new Vector2(vx,vz-inputs[0]);
            Vector2 position=new Vector2(x,z);
            float time=Mathf.Clamp(-Vector2.Dot(position,relative)/Mathf.Max(.01f,relative.sqrMagnitude),0,1.45f);
            float gap=(position+relative*time).sqrMagnitude;
            encounter|=gap<1.05f*1.05f;
            if(GroupPolicy&&gap<1.12f*1.12f)
                encounterBlend=Mathf.Max(encounterBlend,Mathf.Clamp01((1.12f-Mathf.Sqrt(gap))/.54f));
            int slot=NeighborCount-1;
            while(slot>0&&squared<distances[slot-1]) {
                distances[slot]=distances[slot-1];
                System.Array.Copy(inputs,4+(slot-1)*5,inputs,4+slot*5,5);
                slot--;
            }
            distances[slot]=squared;
            int index=4+slot*5;
            inputs[index]=x;inputs[index+1]=z;inputs[index+2]=vx;inputs[index+3]=vz;inputs[index+4]=radius;
        }

        public bool Evaluate()
        {
            if(GroupPolicy?encounterBlend<=.001f:!encounter)return false;
            for(int i=0;i<HiddenCount;i++) {
                float value=weights.inputBias[i];
                for(int j=0;j<InputCount;j++)value+=inputs[j]*weights.inputWeights[i*InputCount+j];
                hidden[i]=Mathf.Max(0,value);
            }
            highest=float.NegativeInfinity;
            for(int i=0;i<ActionCount;i++) {
                float value=weights.outputBias[i];
                for(int j=0;j<HiddenCount;j++)value+=hidden[j]*weights.outputWeights[i*HiddenCount+j];
                scores[i]=value;highest=Mathf.Max(highest,value);
            }
            return true;
        }

        public float Cost(float angle,float speedScale)
        {
            if(Mathf.Abs(angle)==12)return Mathf.Lerp(Cost(0,speedScale),Cost(Mathf.Sign(angle)*25,speedScale),.48f);
            int group=angle switch {0=>0,25=>1,50=>2,-25=>3,-50=>4,_=>-1};
            if(group<0)return .36f*(GroupPolicy?encounterBlend:1);
            int action=speedScale==0?15:group*3+(speedScale==1?0:speedScale>.5f?1:2);
            return Mathf.Min(.36f,(highest-scores[action])*.18f)*(GroupPolicy?encounterBlend:1);
        }
    }
}
