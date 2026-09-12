namespace SubwayCarry.AI.UtilityJourney
{
    /// <summary>
    /// Read/write boundary between a passenger's utility brain and the scene-specific
    /// world model. The same brain can therefore run in the focused multi-screen
    /// prototype or the whole-line overview without learning scene coordinates.
    /// </summary>
    public interface IUtilityJourneyWorld
    {
        UtilityJourneyArea CurrentArea { get; }
        string CurrentSocialSpaceId { get; }
        bool IsTransitioning { get; }
        bool IsComplete { get; }

        UtilityPassengerFacts GetFacts();

        void ResolveAction(
            UtilityJourneyAction action,
            UtilityJourneyFacilityPrototype facility);
    }
}
