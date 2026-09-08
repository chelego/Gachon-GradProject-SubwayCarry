using UnityEngine;

namespace SubwayCarry.AI.V2
{
    // Shared place-independent executor. No map names, spawn deadlines, random patrol points or timed seat eviction.
    public sealed class PassengerAiV2ContextualActivity : MonoBehaviour
    {
        PassengerAiV2Agent agent;
        IPassengerAiV2Place place;
        PassengerAiV2RuntimePersonality personality;
        PassengerAiV2ActivitySettings settings;
        PassengerAiV2InteriorSpotSmartObject reserved, failedSpot;
        Vector2 target, progressPosition;
        float nextTick, heldAt, lastRelocation = -1000, obstructedFor, lastProgress, failedUntil, retryAt;
        bool preparing, hasExit, hasFacility, routeSuspended;
        Vector2 committedExit;
        int routeFailures;
        float wakeStagger;
        public PassengerAiV2LocalGoal Goal { get; private set; }
        public PassengerAiV2ActivityState State { get; private set; }
        public PassengerAiV2DecisionReason LastReason { get; private set; }
        public int DecisionCount { get; private set; }
        public int TargetChangeCount { get; private set; }
        public bool Sitting => State == PassengerAiV2ActivityState.Staying && reserved != null && reserved.Kind == PassengerAiV2InteriorSpotKind.Seat;
        public bool Leaning => State == PassengerAiV2ActivityState.Staying && reserved != null && reserved.Kind == PassengerAiV2InteriorSpotKind.Lean;
        public bool IsAtExit => State == PassengerAiV2ActivityState.AtExit;

        public void Initialize(PassengerAiV2Agent passenger, IPassengerAiV2Place environment,
            PassengerAiV2RuntimePersonality profile, PassengerAiV2ActivitySettings tuning, PassengerAiV2LocalGoal goal, float stagger01)
        {
            agent = passenger; place = environment; personality = profile; settings = tuning; Goal = goal;
            if (place == null || settings == null) throw new System.ArgumentNullException("Place and activity settings are required.");
            wakeStagger = Mathf.Clamp01(stagger01) * 0.2f; place.Changed += OnPlaceChanged;
            agent.SetAutoLoop(false); agent.SetHoldPosition(true);
            State = PassengerAiV2ActivityState.Choosing; LastReason = PassengerAiV2DecisionReason.Initial;
            nextTick = Time.time + Mathf.Clamp01(stagger01) * settings.decisionInterval;
        }

        void Update()
        {
            if (agent == null || place == null || Time.time < nextTick) return;
            Tick(Time.time);
        }
        void OnPlaceChanged()
        {
            nextTick = Mathf.Min(nextTick, Time.time + wakeStagger);
            if (!routeSuspended) return;
            routeSuspended = false; routeFailures = 0; retryAt = Time.time;
            State = PassengerAiV2ActivityState.Choosing;
        }
#if UNITY_EDITOR
        public void TickForValidation(float now) { Tick(now); }
#endif

        void Tick(float now)
        {
            float interval = State == PassengerAiV2ActivityState.Staying ? settings.stayingPerceptionInterval : settings.decisionInterval;
            nextTick = now + interval;
            bool shouldPrepare = PassengerAiV2ActivityPolicy.ShouldPrepare(Goal, place.Context);
            if (!preparing && shouldPrepare)
            {
                preparing = true; LastReason = PassengerAiV2DecisionReason.RelevantService; Release(); Choose(now); return;
            }
            if (preparing && !shouldPrepare)
            {
                // Missed service / departed train: release the door area and settle again, without walking through closed doors.
                preparing = false; Release(); State = PassengerAiV2ActivityState.Choosing; retryAt = now;
            }
            switch (State)
            {
                case PassengerAiV2ActivityState.Choosing:
                    if (now >= retryAt) Choose(now);
                    break;
                case PassengerAiV2ActivityState.Moving:
                case PassengerAiV2ActivityState.Preparing:
                case PassengerAiV2ActivityState.Leaving:
                    TickMove(now);
                    break;
                case PassengerAiV2ActivityState.Settling:
                    if (agent.Velocity.sqrMagnitude <= 0.01f)
                    {
                        heldAt = now;
                        State = preparing ? PassengerAiV2ActivityState.ReadyForTraversal : PassengerAiV2ActivityState.Staying;
                    }
                    break;
                case PassengerAiV2ActivityState.Staying:
                    bool valid = !hasFacility || (reserved != null && reserved.HasReservation(agent) && place.CanUse(reserved, false));
                    bool obstruction = !Sitting && !Leaning && place.IsObstructingFlow(agent.Position, agent);
                    obstructedFor = obstruction ? obstructedFor + interval : 0;
                    if (PassengerAiV2ActivityPolicy.MayRelocate(Sitting || Leaning, valid, now - heldAt, obstructedFor, now - lastRelocation, settings))
                    {
                        LastReason = valid ? PassengerAiV2DecisionReason.PersistentObstruction : PassengerAiV2DecisionReason.FacilityLost;
                        lastRelocation = now; obstructedFor = 0; Release(); Choose(now);
                    }
                    else LastReason = PassengerAiV2DecisionReason.KeepCurrent;
                    break;
                case PassengerAiV2ActivityState.ReadyForTraversal:
                    if (hasFacility && (reserved == null || !reserved.HasReservation(agent) || !place.CanUse(reserved, true))) { Release(); Choose(now); }
                    break;
            }
        }

        void Choose(float now)
        {
            DecisionCount++;
            if (Goal == PassengerAiV2LocalGoal.PassThrough)
            {
                if (!hasExit) hasExit = place.TryGetExit(agent.Position, out committedExit);
                if (hasExit) Move(committedExit, PassengerAiV2ActivityState.Leaving, now);
                else StayAndRetry(now);
                return;
            }
            bool canRemain = !preparing && LastReason != PassengerAiV2DecisionReason.PersistentObstruction && place.CanStand(agent.Position, agent);
            float stayScore = canRemain ? PassengerAiV2ActivityPolicy.Score(PassengerAiV2InteriorSpotKind.Stand, personality,
                place.Context.Kind, 0.25f, 0, place.CrowdCost(agent.Position, agent), settings)
                + (LastReason == PassengerAiV2DecisionReason.Initial ? 0 : settings.stayBonus) : float.NegativeInfinity;
            float bestScore = float.NegativeInfinity;
            PassengerAiV2InteriorSpotSmartObject best = null;
            var spots = place.Spots;
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                if (s == null || (s == failedSpot && now < failedUntil) || !s.IsAvailableFor(agent) || !place.CanUse(s, preparing)) continue;
                float distance = Vector2.Distance(agent.Position, s.UsePosition);
                float score = preparing ? 1 - distance * settings.distanceCost - place.CrowdCost(s.UsePosition, agent) * settings.crowdCost
                    : PassengerAiV2ActivityPolicy.Score(s.Kind, personality, place.Context.Kind, s.Comfort, distance, place.CrowdCost(s.UsePosition, agent), settings);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            float threshold = LastReason == PassengerAiV2DecisionReason.Initial ? 0 : settings.switchAdvantage;
            if (best != null && (!canRemain || bestScore > stayScore + threshold) && best.RequestUse(agent, 0, Vector2.Distance(agent.Position, best.UsePosition)))
            {
                reserved = best; hasFacility = true; Move(best.UsePosition, preparing ? PassengerAiV2ActivityState.Preparing : PassengerAiV2ActivityState.Moving, now); return;
            }
            if (canRemain) { Hold(now); return; }
            // No free chair does not imply a patrol. Find one safe standing area, then keep it.
            if (!preparing && place.TryFindStandingPosition(agent.Position, agent, out Vector2 stand))
            { Move(stand, PassengerAiV2ActivityState.Moving, now); return; }
            StayAndRetry(now);
        }

        void TickMove(float now)
        {
            if (hasFacility && (reserved == null || !reserved.HasReservation(agent) || !place.CanUse(reserved, preparing)))
            { LastReason = PassengerAiV2DecisionReason.FacilityLost; Release(); Choose(now); return; }
            if (agent.HasReachedTarget || (agent.Position - target).sqrMagnitude <= settings.arrivalRadius * settings.arrivalRadius)
            {
                if (State == PassengerAiV2ActivityState.Leaving) { agent.SetHoldPosition(true); State = PassengerAiV2ActivityState.AtExit; return; }
                if (reserved != null && !reserved.MarkOccupied(agent)) { Release(); StayAndRetry(now); return; }
                Hold(now); routeFailures = 0; return;
            }
            if ((agent.Position - progressPosition).sqrMagnitude > 0.25f) { progressPosition = agent.Position; lastProgress = now; }
            if (now - lastProgress < settings.routeStallSeconds) return;
            LastReason = PassengerAiV2DecisionReason.RouteFailure; failedSpot = reserved; failedUntil = now + settings.failedSpotCooldown;
            routeFailures++; Release();
            // One failed spot is remembered. Do not switch direction at every social-navigation tick.
            if (routeFailures < 3) StayAndRetry(now);
            else { routeSuspended = true; agent.SetHoldPosition(true); State = PassengerAiV2ActivityState.WaitingForRoute; retryAt = float.PositiveInfinity; }
        }

        void Move(Vector2 position, PassengerAiV2ActivityState next, float now)
        {
            target = position; progressPosition = agent.Position; lastProgress = now;
            State = next; TargetChangeCount++; agent.SetMovementTarget(target);
        }
        void Hold(float now) { agent.SetHoldPosition(true); heldAt = now; State = PassengerAiV2ActivityState.Settling; }
        void StayAndRetry(float now) { agent.SetHoldPosition(true); State = PassengerAiV2ActivityState.Choosing; retryAt = now + settings.retryCooldown; }
        void Release() { if (reserved != null) reserved.Release(agent); reserved = null; hasFacility = false; }
        void OnDestroy() { if (place != null) place.Changed -= OnPlaceChanged; Release(); }
    }
}
