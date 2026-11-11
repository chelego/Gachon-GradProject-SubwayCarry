using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Transit
{
    /// <summary>Local scene preview of a reversible fare-gate passage; no fare/economy side effects.</summary>
    public sealed class GachonHubGate : MonoBehaviour
    {
        public Transform leftWing, rightWing;
        public Transform reader;
        public Transform visitor;
        public TextMesh indicator;
        public float openSeconds = 4f;
        private float remaining;
        private float angle;
        public bool IsOpen => angle > 70f;
        public void RequestPass() { remaining = openSeconds; }
        private void Update()
        {
            if (visitor && reader && Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame &&
                Vector3.Distance(visitor.position + Vector3.up, reader.position) < 2.3f)
                RequestPass();
            // Keep the lane open while a visitor is inside it.
            var p = visitor ? transform.InverseTransformPoint(visitor.position) : Vector3.one * 100;
            if (remaining > 0 && Mathf.Abs(p.x) < .65f && Mathf.Abs(p.z) < 1.15f)
                remaining = Mathf.Max(remaining, .6f);
            remaining = Mathf.Max(0, remaining - Time.deltaTime);
            angle = Mathf.MoveTowards(angle, remaining > 0 ? 87 : 0, Time.deltaTime * 160);
            if (leftWing) leftWing.localRotation = Quaternion.Euler(0, -angle, 0);
            if (rightWing) rightWing.localRotation = Quaternion.Euler(0, angle, 0);
            if (indicator) { indicator.text = remaining > 0 ? "↑" : "E"; indicator.color = remaining > 0 ? Color.green : Color.cyan; }
        }
    }
}

