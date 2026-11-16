using System;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Simulator-trained residual. No video, pose model or person data is
    // loaded by the game. The motor owns all clearance and braking limits.
    internal static class Metro3DQueueMotionPolicy
    {
        [Serializable]
        sealed class Weights
        {
            public int version;
            public float[] residual;
        }

        const int GapBins = 8, RelativeBins = 8, LeadBins = 7;
        static float[] table;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            loaded = false;
            table = null;
        }

        internal static float Correction(float gapError, float relativeSpeed, float leadSpeed)
        {
            if (!loaded)
            {
                loaded = true;
                var asset = Resources.Load<TextAsset>("PassengerAI/QueueMotionPolicy");
                if (asset != null)
                {
                    var weights = JsonUtility.FromJson<Weights>(asset.text);
                    if (weights?.version == 1 && weights.residual?.Length == GapBins * RelativeBins * LeadBins &&
                        Array.TrueForAll(weights.residual, value => float.IsFinite(value) && Mathf.Abs(value) <= .131f))
                        table = weights.residual;
                }
            }
            if (table == null) return 0;

            float gap = Mathf.InverseLerp(-.28f, .65f, gapError) * (GapBins - 1);
            float relative = Mathf.InverseLerp(-1f, 1.05f, relativeSpeed) * (RelativeBins - 1);
            float lead = Mathf.InverseLerp(0, 1.30f, leadSpeed) * (LeadBins - 1);
            int i = (int)gap, j = (int)relative, k = (int)lead;
            int i2 = Mathf.Min(i + 1, GapBins - 1), j2 = Mathf.Min(j + 1, RelativeBins - 1);
            float atLow = Mathf.Lerp(Read(i, j, k, lead - k), Read(i, j2, k, lead - k), relative - j);
            float atHigh = Mathf.Lerp(Read(i2, j, k, lead - k), Read(i2, j2, k, lead - k), relative - j);
            return Mathf.Lerp(atLow, atHigh, gap - i);
        }

        static float Read(int gap, int relative, int lead, float blend)
        {
            int start = (gap * RelativeBins + relative) * LeadBins;
            return Mathf.Lerp(table[start + lead], table[start + Mathf.Min(lead + 1, LeadBins - 1)], blend);
        }
    }
}
