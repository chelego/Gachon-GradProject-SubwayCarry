using UnityEngine;
using Use=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabMotionSet.Use;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Small frame search for the two review fields. No navigation or production NPC ownership.
    public sealed class AnimationLabMotionSearch
    {
        public enum Intent { Rest,Travel,Brake,Pivot,SideStep,Look }
        public struct Result { public int entry;public float time,cost;public bool valid; }
        public readonly Vector3[] future=new Vector3[3];
        public readonly float[] futureYaw=new float[3];
        public static readonly float[] Horizons={.2f,.4f,.65f};
        public AnimationLabMotionSet.Frame pose;
        public AnimationLabMotionSet.Entry continuation;
        public bool refinePhase;
        public bool cruise;
        public int postureFamily;
        public Vector3 velocity;
        public float desiredTurn,side,lookYaw;
        public Intent intent;
        public long Searches,Ticks,Bytes;
        public float LastCost {get;private set;}
        public bool Allowed(AnimationLabMotionSet.Entry e)
        {
            if(!e.usable||e.clip==null||e.frames==null||e.frames.Length<3)return false;
            // The four added locomotion rows are the Normal trial. Keep Situations' old
            // candidate set; its original shared Idle remains available.
            if(!refinePhase&&postureFamily==0&&e.postureFamily==1&&(e.uses&Use.Idle)==0)return false;
            if(refinePhase&&postureFamily>0&&(intent==Intent.Rest||intent==Intent.Travel||intent==Intent.Brake)&&e.postureFamily!=postureFamily)return false;
            switch(intent)
            {
                case Intent.Rest:return (e.uses&Use.Idle)!=0;
                case Intent.Look:return (e.uses&Use.Look)!=0;
                case Intent.SideStep:return (e.uses&Use.Strafe)!=0&&Mathf.Sign(e.frames[e.frames.Length-1].root.x)==Mathf.Sign(side);
                case Intent.Pivot:return (e.uses&Use.Pivot)!=0&&Mathf.Sign(e.netYaw)==Mathf.Sign(desiredTurn);
                case Intent.Brake:return (e.uses&(Use.Stop|Use.Idle))!=0;
                default:
                    if((e.uses&Use.MovingTurn)!=0)return Mathf.Abs(desiredTurn)>(refinePhase?3:12)&&Mathf.Sign(e.netYaw)==Mathf.Sign(desiredTurn);
                    if(refinePhase&&cruise&&(e.uses&Use.Start)!=0)return false;
                    if((e.uses&Use.Start)!=0&&velocity.magnitude>.8f&&e!=continuation)return false;
                    return (e.uses&(Use.Forward|Use.Start))!=0&&(e.uses&Use.Stop)==0;
            }
        }
        public float Cost(AnimationLabMotionSet.Entry e,float t)
        {
            var f=e.At(t);
            float cost=(pose.leftFoot-f.leftFoot).sqrMagnitude+(pose.rightFoot-f.rightFoot).sqrMagnitude;
            cost+=.45f*((pose.leftKnee-f.leftKnee).sqrMagnitude+(pose.rightKnee-f.rightKnee).sqrMagnitude);
            cost+=.2f*(pose.hips-f.hips).sqrMagnitude;
            cost+=.035f*((pose.leftVelocity-f.leftVelocity).sqrMagnitude+(pose.rightVelocity-f.rightVelocity).sqrMagnitude);
            float bodyError=Mathf.DeltaAngle(pose.bodyYaw,f.bodyYaw)*Mathf.Deg2Rad;cost+=.7f*bodyError*bodyError;
            if(intent==Intent.Look){float lookError=Mathf.DeltaAngle(f.lookYaw,lookYaw)*Mathf.Deg2Rad;cost+=2*lookError*lookError;}
            if(intent==Intent.Pivot){e.Delta(t,e.duration,out _,out float remaining);float turnError=Mathf.DeltaAngle(remaining,desiredTurn)*Mathf.Deg2Rad;cost+=1.5f*turnError*turnError;}
            cost+=.5f*(Mathf.Abs(pose.leftSupport-f.leftSupport)+Mathf.Abs(pose.rightSupport-f.rightSupport));
            bool opposite=pose.leftSupport>.8f&&pose.rightSupport<.2f&&f.rightSupport>.8f&&f.leftSupport<.2f||pose.rightSupport>.8f&&pose.leftSupport<.2f&&f.leftSupport>.8f&&f.rightSupport<.2f;
            if(opposite)cost+=4; // Inferred single support must not be swapped for a tiny trajectory gain.
            e.Delta(t,t+.06f,out var v,out _);v=Quaternion.Euler(0,-f.yaw,0)*v/.06f;v.y=0;
            cost+=.18f*(v-velocity).sqrMagnitude;
            for(int h=0;h<Horizons.Length;h++)
            {
                e.Delta(t,t+Horizons[h],out var p,out float yaw);p=Quaternion.Euler(0,-f.yaw,0)*p;p.y=0;
                cost+=1.8f*(p-future[h]).sqrMagnitude;
                float angle=Mathf.DeltaAngle(yaw,futureYaw[h])*Mathf.Deg2Rad;cost+=.6f*angle*angle;
            }
            // Do not enter a non-looping action after its usable movement has already ended.
            if(!e.loop&&t>e.duration-.35f&&intent!=Intent.Rest)cost+=2;
            return cost;
        }
        public Result Find(AnimationLabMotionSet set)
        {
            long began=System.Diagnostics.Stopwatch.GetTimestamp(),allocated=System.GC.GetAllocatedBytesForCurrentThread();
            var best=new Result{entry=-1,cost=float.PositiveInfinity};
            for(int e=0;e<set.entries.Length;e++)
            {
                var row=set.entries[e];if(!Allowed(row))continue;
                int end=row.frames.Length-(row.loop?1:Mathf.CeilToInt(.35f*row.sampleRate));
                // 15 Hz database search; playback still samples the full 30 Hz data.
                for(int f=0;f<end;f+=2)
                {
                    float t=f/row.sampleRate,cost=Cost(row,t);
                    if(cost<best.cost){best=new Result{entry=e,time=t,cost=cost,valid=true};}
                }
            }
            // Refine the selected phase, without increasing the whole database search rate.
            if(best.valid&&refinePhase)
            {
                var row=set.entries[best.entry];float center=best.time;
                for(int i=-8;i<=8;i++)
                {
                    float at=center+i/(row.sampleRate*4);
                    if(at<0||at>row.duration-(row.loop?0:.35f))continue;
                    float cost=Cost(row,at);if(cost<best.cost){best.time=at;best.cost=cost;}
                }
            }
            LastCost=best.cost;Searches++;Ticks+=System.Diagnostics.Stopwatch.GetTimestamp()-began;Bytes+=System.GC.GetAllocatedBytesForCurrentThread()-allocated;
            return best;
        }
    }
}
