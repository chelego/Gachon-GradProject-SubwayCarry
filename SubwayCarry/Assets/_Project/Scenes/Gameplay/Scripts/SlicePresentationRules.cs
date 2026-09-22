namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Pure calculations shared by the UI and non-Unity regression checks.
    public static class SlicePresentationRules
    {
        public static int DamageStage(float health) => health <= 0 ? 3 : health <= 40 ? 2 : health < 80 ? 1 : 0;
        public static bool ShouldCutIn(float oldBox, float oldCake, float box, float cake, float threshold)
            => oldBox - box + oldCake - cake >= threshold || DamageStage(box) > DamageStage(oldBox) || DamageStage(cake) > DamageStage(oldCake);
        public static float ZoomPan(float oldPan, float pointer, float before, float after)
            => pointer - (pointer - oldPan) * (after / before);
        public static bool BlocksSimulation(bool phone, bool pause, bool cutIn, bool result) => phone || pause || cutIn || result;
    }
}
