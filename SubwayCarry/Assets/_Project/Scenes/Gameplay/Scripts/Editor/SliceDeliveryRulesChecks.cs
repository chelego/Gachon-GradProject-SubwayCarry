using System;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    public static class SliceDeliveryRulesChecks
    {
        static int checks;
#if UNITY_EDITOR
        [UnityEditor.MenuItem("SubwayCarry/Art Map Slice/Validate Delivery Rules (no Play Mode)")]
#endif
        public static void Run()
        {
            checks = 0;
            Require(Math.Abs(SlicePresentationRules.FloorHalfWidth(2.45f*.984f,.5f)-2.505f)<.0001f, "Floor sprite spans five projected grid cells");
            Require(Math.Abs(SlicePresentationRules.FloorHalfWidth(2.3f*.984f,.5f)-2.505f)<.0001f, "Tactile grout stays connected to ordinary floor");
            Require(Math.Abs(SlicePresentationRules.FloorHalfWidth(2.45f*.984f*2,1)-5.005f)<.0001f, "Floor grid span follows map scale");
            Require(Math.Abs(SlicePresentationRules.FloorHalfWidth(2.4f,0)-2.405f)<.0001f, "Degenerate grid axis retains visible floor width");
            Require(!SlicePresentationRules.AllowsFarePassage(-.4f,.4f,3,.28f,false), "Closed fare gate blocks entering");
            Require(!SlicePresentationRules.AllowsFarePassage(.4f,-.4f,3,.28f,false), "Closed fare gate blocks leaving");
            Require(SlicePresentationRules.AllowsFarePassage(-.4f,.4f,3,.28f,true), "Valid open gate lane");
            Require(!SlicePresentationRules.AllowsFarePassage(-.4f,.4f,6,.28f,true), "Open gate never opens the glass barrier");
            Require(!SlicePresentationRules.AllowsFarePassage(-.4f,-.1f,3,.28f,false), "Feet radius stops before the flap plane");
            Require(SlicePresentationRules.AllowsFarePassage(-.1f,-.2f,3,.28f,false), "Contact recovery can move away without freezing");
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
            Require(SlicePresentationRules.TrainOffset(false, 0, -1, 100) == 100, "Waiting train is outside the scene");
            Require(SlicePresentationRules.TrainOffset(true, .5f, 3, 100) == 0, "Docked doors align regardless of elapsed travel");
            Require(SlicePresentationRules.TrainOffset(false, 1, -1, 100) == 0, "Approach reaches the exact stop");
            Require(SlicePresentationRules.TrainOffset(false, 0, 7, 100) == -100, "Departure fully clears the scene");
            Require(SlicePresentationRules.TrainOffset(false, 0, 20, 100) == -100, "Departed train does not wrap early");
            float previous = 100;
            for (int i = 1; i <= 100; i++)
            {
                float position = SlicePresentationRules.TrainOffset(false, i / 100f, -1, 100);
                Require(position <= previous && position >= 0, "Approach is monotonic without passing the door");
                previous = position;
            }
            previous = 0;
            for (int i = 1; i <= 100; i++)
            {
                float position = SlicePresentationRules.TrainOffset(false, 0, i * .1f, 100);
                Require(position <= previous && position >= -100, "Departure accelerates away without restarting at zero");
                previous = position;
            }
            foreach (var spans in new[] { new[] { 200f, 400f }, new[] { 400f, 400f }, new[] { 800f, 400f }, new[] { 2949f, 454f }, new[] { 4196f, 289f } })
                foreach (float offset in new[] { -100000f, -25f, 0, 25f, 100000f })
                {
                    float p = SlicePresentationRules.ClampMapPan(offset, spans[0], spans[1]);
                    Require(spans[0] <= spans[1] ? Math.Abs(p - (spans[1] - spans[0]) * .5f) < .001f
                        : p <= 0 && p + spans[0] >= spans[1], "Map cannot be dragged into an empty phone viewport");
                }
            Require(SlicePresentationRules.ZoomPan(0, 0, 0, 1) == 0, "Initial zoom cannot divide by zero");
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"Delivery/presentation rules: {checks} checks passed. No Play Mode or visual validation performed.");
#endif
        }
        static void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks++; }
#if !UNITY_EDITOR
        public static int Main() { Run(); Console.WriteLine($"Delivery/presentation rules: {checks} checks passed."); return 0; }
#endif
    }
}
