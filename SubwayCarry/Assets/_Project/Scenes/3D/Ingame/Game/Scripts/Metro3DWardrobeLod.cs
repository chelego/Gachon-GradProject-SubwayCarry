using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Display-only LOD. It never changes the actor, skeleton, Animator, capsule,
    // journey or persistent identity. Meshes/materials belong to the shared cache.
    public sealed class Metro3DWardrobeLod : MonoBehaviour
    {
        SkinnedMeshRenderer skin;
        Mesh[] meshes;
        Material[] nearMaterials,distantMaterials;
        int level;
        float nextSample;
        static Camera view;
        static float nextCameraSample;
        internal int Level => level;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCameraCache() { view=null;nextCameraSample=0; }

        internal void Configure(SkinnedMeshRenderer target, Mesh[] shared, Material[] nearSlots, Material[] distantSlots)
        {
            skin=target;meshes=shared;nearMaterials=nearSlots;distantMaterials=distantSlots;level=0;
            skin.sharedMaterials=nearMaterials;
            skin.sharedMesh=meshes[0];skin.localBounds=meshes[0].bounds;
            nextSample=Time.unscaledTime+((GetInstanceID()&31)/32f)*.3f;
            enabled=true;
        }

        internal void Clear() { meshes=null;skin=null;nearMaterials=null;distantMaterials=null;enabled=false; }

        void Update()
        {
            float now=Time.unscaledTime;
            if(now<nextSample||skin==null||meshes==null)return;
            nextSample=now+.3f;
            // A single cached query for the crowd also follows observer cameras.
            if(now>=nextCameraSample){view=Camera.main;nextCameraSample=now+.5f;}
            if(view==null)return;
            SelectDistance(Vector3.Distance(view.transform.position,skin.bounds.center));
        }

        internal void SelectDistance(float distance)
        {
            if(meshes==null||skin==null)return;
            int next=level;
            // Hysteresis prevents repeated swaps while waiting on a boundary.
            if(level==0&&distance>11)next=distance>23?2:1;
            else if(level==1)next=distance<9?0:distance>23?2:1;
            else if(level==2&&distance<21)next=distance<9?0:1;
            if(next==level)return;
            skin.sharedMaterials=next==0?nearMaterials:distantMaterials;
            level=next;skin.sharedMesh=meshes[level];skin.localBounds=meshes[level].bounds;
        }
    }
}
