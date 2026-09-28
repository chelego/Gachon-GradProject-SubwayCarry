namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Pure calculations shared by the UI and non-Unity regression checks.
    public static class SlicePresentationRules
    {
        public static int DamageStage(float health) => health <= 0 ? 3 : health <= 40 ? 2 : health < 80 ? 1 : 0;
        public static bool ShouldCutIn(float oldBox, float oldCake, float box, float cake, float threshold)
            => oldBox - box + oldCake - cake >= threshold || DamageStage(box) > DamageStage(oldBox) || DamageStage(cake) > DamageStage(oldCake);
        public static float ZoomPan(float oldPan, float pointer, float before, float after)
            => before > 0 ? pointer - (pointer - oldPan) * (after / before) : 0;
        public static float ClampMapPan(float pan, float imageSpan, float viewportSpan)
            => imageSpan <= viewportSpan ? (viewportSpan - imageSpan) * .5f
                : System.Math.Max(viewportSpan - imageSpan, System.Math.Min(0, pan));
        public static bool BlocksSimulation(bool phone, bool pause, bool cutIn, bool result) => phone || pause || cutIn || result;
        public static float TrainOffset(bool docked, float approach01, float departureSeconds, float offscreenDistance)
        {
            if (docked) return 0;
            if (approach01 > 0)
            {
                float remaining = 1 - System.Math.Min(1, approach01);
                return offscreenDistance * remaining * remaining * remaining;
            }
            if (departureSeconds < 0) return offscreenDistance;
            float t = System.Math.Min(1, System.Math.Max(0, departureSeconds / 7));
            return -offscreenDistance * t * t * (3 - 2 * t);
        }
    }
}
