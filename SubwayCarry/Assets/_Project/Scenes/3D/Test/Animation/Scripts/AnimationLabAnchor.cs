using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Authored contact, approach and exit are separate from decorative geometry.
    public sealed class AnimationLabAnchor : MonoBehaviour
    {
        public enum Kind { Seat,Wall }
        public Kind kind;
        public Transform contact,approach,exit;
        public Collider surface;
        public string label;
        public bool SurfaceValid => contact!=null&&approach!=null&&exit!=null&&surface!=null&&surface.enabled&&!surface.isTrigger;
        public bool Contact(out Vector3 point,out Vector3 normal)
        {
            point=normal=Vector3.zero;
            if(!SurfaceValid)return false;
            var ray=kind==Kind.Seat?new Ray(contact.position+Vector3.up*.25f,Vector3.down):new Ray(contact.position+contact.forward*.3f,-contact.forward);
            if(!surface.Raycast(ray,out var hit,.65f))return false;
            point=hit.point;normal=hit.normal;
            return kind==Kind.Seat?Vector3.Dot(normal,Vector3.up)>.9f:Mathf.Abs(normal.y)<.2f;
        }
        void OnDrawGizmosSelected()
        {
            if(contact!=null){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(contact.position,.06f);Gizmos.DrawRay(contact.position,contact.forward*.5f);}
            if(approach!=null){Gizmos.color=Color.green;Gizmos.DrawWireSphere(approach.position,.22f);}
            if(exit!=null){Gizmos.color=Color.yellow;Gizmos.DrawWireSphere(exit.position,.22f);}
        }
    }
}
