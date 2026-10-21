using UnityEngine;

namespace SubwayCarry.Transit
{
    public sealed class GachonHubEscalator : MonoBehaviour
    {
        public CharacterController visitor;
        public Transform[] treads;
        public float run = 16f, rise = 7f, width = 1.25f, speed = .7f;
        public bool upwards = true;
        private float phase;
        private void Update()
        {
            float direction = upwards ? 1 : -1;
            phase = Mathf.Repeat(phase + direction * speed * Time.deltaTime, run);
            if (treads != null)
                for (int i = 0; i < treads.Length; i++)
                {
                    float z = Mathf.Repeat(i * run / treads.Length + phase, run);
                    treads[i].localPosition = new Vector3(0, z * rise / run - .08f, z);
                }
            if (!visitor || !visitor.enabled) return;
            Vector3 p = transform.InverseTransformPoint(visitor.transform.position);
            float surface = Mathf.Clamp01(p.z / run) * rise;
            if (Mathf.Abs(p.x) < width * .5f && p.z >= -.15f && p.z <= run + .15f &&
                Mathf.Abs(p.y - surface) < .5f)
            {
                Vector3 motion = new Vector3(0, rise / run, 1) * (direction * speed * Time.deltaTime);
                // The capsule reaches the landing's vertical face before its centre reaches the end
                // of the incline. Lift through the comb-plate transition before carrying forward.
                if (upwards && p.z > run - .5f)
                    motion.y = Mathf.Max(motion.y, rise + .06f - p.y);
                visitor.Move(transform.TransformDirection(motion));
            }
        }
    }
}

