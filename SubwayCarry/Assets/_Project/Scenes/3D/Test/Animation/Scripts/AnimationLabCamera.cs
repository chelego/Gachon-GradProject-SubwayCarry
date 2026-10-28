using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [DefaultExecutionOrder(400)]
    public sealed class AnimationLabCamera : MonoBehaviour
    {
        public AnimationLabCharacter character;
        public Transform reviewTarget;
        public bool inspectOnly;
        public LayerMask environment=1;
        public float yaw,pitch=15,distance=3.4f,lookHeight=1.15f;
        CursorLockMode previousLock;
        bool previousVisible,ownsCursor;
        int inputFrame=-1;
        public Vector3 FlatForward=>Quaternion.Euler(0,yaw,0)*Vector3.forward;
        void OnEnable()
        {
            if(Application.isBatchMode)return;
            previousLock=Cursor.lockState;previousVisible=Cursor.visible;ownsCursor=true;
            Cursor.lockState=inspectOnly?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=inspectOnly;
        }
        public void CaptureInput()
        {
            if(character==null||inputFrame==Time.frameCount)return;
            inputFrame=Time.frameCount;
            if(inspectOnly&&AnimationLabReviewMenu.PointerOverMenu)return;
            if(inspectOnly&&Mouse.current!=null){if(Mouse.current.rightButton.isPressed){var d=Mouse.current.delta.ReadValue();yaw+=d.x*.16f;pitch=Mathf.Clamp(pitch-d.y*.16f,-28,70);}distance=Mathf.Clamp(distance-Mouse.current.scroll.ReadValue().y*.003f,.7f,7);if(Keyboard.current!=null){if(Keyboard.current.pageUpKey.isPressed)lookHeight=Mathf.Min(1.8f,lookHeight+Time.unscaledDeltaTime);if(Keyboard.current.pageDownKey.isPressed)lookHeight=Mathf.Max(.3f,lookHeight-Time.unscaledDeltaTime);}return;}
            if(!character.Automated&&Mouse.current!=null) {
                if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){bool locked=Cursor.lockState==CursorLockMode.Locked;Cursor.lockState=locked?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=locked;}
                if(Mouse.current.rightButton.wasPressedThisFrame){Cursor.lockState=inspectOnly?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=inspectOnly;}
                if(Cursor.lockState==CursorLockMode.Locked){var delta=Mouse.current.delta.ReadValue();yaw+=delta.x*.12f;pitch=Mathf.Clamp(pitch-delta.y*.12f,-28,70);}
                distance=Mathf.Clamp(distance-Mouse.current.scroll.ReadValue().y*.003f,1.2f,7);
                if(Keyboard.current!=null){if(Keyboard.current.pageUpKey.isPressed)lookHeight=Mathf.Min(1.8f,lookHeight+Time.unscaledDeltaTime);if(Keyboard.current.pageDownKey.isPressed)lookHeight=Mathf.Max(.3f,lookHeight-Time.unscaledDeltaTime);}
            }
        }
        void LateUpdate()
        {
            if(character==null)return;
            CaptureInput();
            var focus=(reviewTarget!=null?reviewTarget.position:character.transform.position)+Vector3.up*lookHeight;
            var rotation=Quaternion.Euler(pitch,yaw,0);var backward=rotation*Vector3.back;
            float safe=distance;
            if(Physics.SphereCast(focus,.12f,backward,out var hit,distance,environment,QueryTriggerInteraction.Ignore))safe=Mathf.Max(.2f,hit.distance-.08f);
            transform.SetPositionAndRotation(focus+backward*safe,rotation);
        }
        void OnDisable(){if(ownsCursor){Cursor.lockState=previousLock;Cursor.visible=previousVisible;ownsCursor=false;}}
    }
}
