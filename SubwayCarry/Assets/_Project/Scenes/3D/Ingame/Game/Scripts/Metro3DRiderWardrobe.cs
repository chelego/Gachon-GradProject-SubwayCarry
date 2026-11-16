namespace SubwayCarry.Gameplay3D
{
    internal enum Metro3DRiderLook { Office, Senior, Child, Student }

    internal sealed class Metro3DRiderWardrobe
    {
        internal readonly Metro3DRocketboxCast PublisherCast = new();

        internal static Metro3DRiderLook LookFor(int rider)
        {
            int pattern = (int)((uint)rider * 17 % 13);
            if (pattern == 0) return Metro3DRiderLook.Child;
            if (pattern < 4) return Metro3DRiderLook.Senior;
            return pattern < 8 ? Metro3DRiderLook.Student : Metro3DRiderLook.Office;
        }

        internal void Dispose() => PublisherCast.Dispose();
    }
}
