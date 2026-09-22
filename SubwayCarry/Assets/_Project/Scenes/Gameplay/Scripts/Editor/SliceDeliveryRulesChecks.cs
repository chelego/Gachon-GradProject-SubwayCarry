using System;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    public static class SliceDeliveryRulesChecks
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("SubwayCarry/Art Map Slice/Validate Delivery Rules (no Play Mode)")]
#endif
        public static void Run()
        {
            var intact = SliceDeliveryRules.Evaluate(100, 100, false, 25000, 3000, 1500, 1500, true, true);
            Require(intact.Success && intact.NetIncome == 1500 && intact.ReturnFare == 0 && !intact.UsedInsurance && !intact.UsedFareSupport, "Intact/free return");
            var boxOnly = SliceDeliveryRules.Evaluate(99, 100, false, 25000, 3000, 1500, 1500, true, false);
            Require(boxOnly.Success && boxOnly.Compensation == 0 && boxOnly.ReturnFare == 1500 && !boxOnly.UsedInsurance, "Box-only damage");
            var boundary = SliceDeliveryRules.Evaluate(0, 20, false, 25000, 3000, 1500, 1500, false, false);
            Require(boundary.Success && boundary.Compensation == 20000 && boundary.Fee == 3000, "20 percent is a completed delivery");
            var failed = SliceDeliveryRules.Evaluate(0, 19.9f, false, 25000, 3000, 1500, 1500, true, true);
            Require(!failed.Success && failed.Fee == 0 && failed.UsedInsurance && failed.UsedFareSupport && failed.NetIncome == -1500, "Failure and one-use protections");
            var missed = SliceDeliveryRules.Evaluate(100, 100, true, 25000, 3000, 1500, 1500, true, false);
            Require(!missed.Success && missed.ReturnFare == 1500 && missed.Fee == 0 && !missed.UsedInsurance, "Missed required stop");
            for (int cake = 0; cake <= 100; cake++) for (int services = 0; services < 4; services++)
            {
                var result = SliceDeliveryRules.Evaluate(0, cake, false, 25000, 3000, 1500, 1500, (services & 1) != 0, (services & 2) != 0);
                Require(result.Success == (cake >= 20), "Threshold sweep");
                Require(result.NetIncome == result.Fee - result.Compensation - 1500 - result.ReturnFare, "No double-charged outbound fare");
                Require(result.Compensation >= 0 && result.ReturnFare >= 0, "Non-negative costs");
            }
            Require(!SlicePresentationRules.ShouldCutIn(100, 100, 99, 100, 18), "Minor damage does not pause");
            Require(SlicePresentationRules.ShouldCutIn(100, 100, 82, 100, 18), "Large impact cut-in");
            Require(SlicePresentationRules.ShouldCutIn(80, 100, 79, 100, 18), "Box stage crossing cut-in");
            Require(SlicePresentationRules.ShouldCutIn(0, 41, 0, 40, 18), "Cake stage crossing cut-in");
            Require(!SlicePresentationRules.ShouldCutIn(0, 40, 100, 100, 18), "Reset never looks like damage");
            for (int flags = 0; flags < 16; flags++)
                Require(SlicePresentationRules.BlocksSimulation((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0) == (flags != 0), "Nested pauses retain the other pause owners");
            for (int i = 1; i <= 30; i++)
            {
                float before = i * .1f, after = before * 1.6f;
                float pan = SlicePresentationRules.ZoomPan(-120, 250, before, after);
                Require(Math.Abs((250 - pan) / after - 370 / before) < .002f, "Zoom stays anchored under cursor");
            }
#if UNITY_EDITOR
            UnityEngine.Debug.Log("Delivery/presentation rules: 460 scenarios passed. No Play Mode or visual validation performed.");
#endif
        }
        static void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
#if !UNITY_EDITOR
        public static int Main() { Run(); Console.WriteLine("Delivery/presentation rules: 460 scenarios passed."); return 0; }
#endif
    }
}
