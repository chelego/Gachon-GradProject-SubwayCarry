using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SubwayCarry.AI.V2;
using SubwayCarry.Gameplay;
using SubwayCarry.Core.Contracts;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    public sealed class SliceGameController : MonoBehaviour
    {
        private const float FacilityDistanceTieEpsilon = 0.0001f;

        public SliceMap[] maps;
        public GameObject playerPrefab, packagePrefab;
        public Material playerOutline;
        public PassengerAiV2PersonalityProfile baselineProfile;
        public PassengerAiV2ActivitySettings activitySettings;
        [Range(0, 30)] public int waitingPassengerCapacity = 10;
        public Sprite[] idleSprites, walkSprites;
        public Camera gameplayCamera;
        [Range(4, 40)] public int maximumPassengers = 18;
        [Min(0.5f)] public float spawnInterval = 1.5f;
        public int seed = 2701;
        public SliceDeliveryCatalog deliveryCatalog;
        SliceJourneyController journey;
        SliceSceneOcclusion occlusion;
        public Bounds InteractionBounds(Vector2 point) => occlusion != null ? occlusion.InteractionBounds(point) : new Bounds(point, Vector3.one);
        GameObject player, actorsRoot;
        PlayerController playerInput;
        PlayerPosture posture;
        PackageDurability durability;
        PlayerBalance balance;
        SlicePlayerBoundary boundary;
        Rigidbody2D playerBody;
        PassengerAiV2CrowdManager crowd;
        PassengerAiV2Agent playerProxy;
        PassengerAiV2InteriorSpotSmartObject playerFacility;
        bool facilityApproach;
        public bool PlayerLeavingFacility { get; private set; }
        public bool IsUsingFacility => playerFacility != null || facilityApproach;
        SlicePlaceEnvironment environment;
        PassengerAiV2InteriorSpotSmartObject[] spots;
        readonly List<SlicePassengerIntent> passengers = new List<SlicePassengerIntent>(40);
        readonly List<PassengerAiV2Agent> nearby = new List<PassengerAiV2Agent>(20);
        readonly Dictionary<int, int> aboard = new Dictionary<int, int>();
        readonly List<KeyValuePair<int, int>> alighted = new List<KeyValuePair<int, int>>();
        readonly Dictionary<SliceMap, SlicePlatformTrain> platformTrains = new Dictionary<SliceMap, SlicePlatformTrain>();
        public SlicePlatformTrain PlatformTrain => platformTrains.TryGetValue(CurrentMap, out var view) ? view : null;
        public SlicePlatformTrain TrainAtMap(int index) => platformTrains.TryGetValue(maps[index], out var view) ? view : null;
        public IEnumerable<KeyValuePair<int, int>> AboardPassengers => aboard;
        public int AllocatePassengerId() => ++serial;
        public Color PassengerColor(int id) => Personality(id).DebugColor;
        public void RememberTrainPassenger(int id, int destination) { aboard[id] = destination; }
        public void ClearDepartedTrain() { aboard.Clear(); }
        public void InitializePlatformTrains(SliceJourneyController flow)
        {
            foreach (int i in new[] { 0, 2, 6 })
            {
                var view = maps[i].gameObject.AddComponent<SlicePlatformTrain>();
                platformTrains.Add(maps[i], view); view.Initialize(this, flow, maps[i]);
            }
        }
        public bool SpawnPlatformAlighter(int id, int destination, Vector2 cabin, Vector2 landing, SlicePlatformTrain trainView)
        {
            var intent = SpawnPassenger(false, id, destination, true, cabin);
            if (intent == null) return false;
            intent.BeginPlatformAlighting(cabin, landing, trainView); return true;
        }
        Sprite sharedSprite;
        Texture2D whiteTexture;
        int currentMap, serial, exited, nearestPortal = -1;
        float nextSpawn, nextHud, fade, portalReadyAt;
        bool changing, releasingPlayerFacility;
        string hud = "", prompt = "";
        GUIStyle hudStyle;
        public SliceMap CurrentMap => maps[currentMap];
        public int CurrentMapIndex => currentMap;
        public GameObject Player => player;
        public PackageDurability Package => durability;
        public PlayerPosture Posture => posture;
        public PlayerBalance Balance => balance;
        public int PassengerCount => passengers.Count;
        public int ApproachingStopIndex => journey != null ? journey.ApproachingStopIndex : 0;
        public bool DoorsOpen => journey != null && journey.CurrentDoorState.State == TrainDoorState.Open;
        public float DoorSecondsRemaining => journey != null ? journey.SecondsRemaining : 0;
        public bool HasDoorPassage
        {
            get { foreach (var p in passengers) if (p != null && p.IsCrossingDoor) return true; return PlatformTrain != null && PlatformTrain.HasCrossingAlighters; }
        }
        public void ResetPassengerJourney() { aboard.Clear(); alighted.Clear(); }
        public void PassengerTraversed(int id, int destination, bool boarding)
        {
            if (boarding) aboard[id] = destination;
            else { aboard.Remove(id); if (currentMap == 1) alighted.Add(new KeyValuePair<int, int>(id, destination)); }
            exited++;
        }
        public bool IsChangingMap => changing;
        public bool HasActivePlayerFacility => playerFacility != null;
        public bool PortalInteractionReady => Time.time >= portalReadyAt;
        public Vector2 ActivePlayerFacilityPosition => playerFacility != null
            ? playerFacility.UsePosition
            : PlayerPosition;

        public bool TryUseFacility(CarryPosture target)
        {
            if (facilityApproach || changing || posture.IsTransitioning) return false;
            if (target == CarryPosture.Standing || target == CarryPosture.OverheadCarry)
            {
                if (!posture.TryTransition(target)) return false;
                if (deliveryCatalog != null && playerFacility != null) { StartCoroutine(LeaveFacility()); return true; }
                ReleasePlayerFacility(); return true;
            }
            PassengerAiV2InteriorSpotKind kind = target == CarryPosture.Sitting ? PassengerAiV2InteriorSpotKind.Seat : target == CarryPosture.Leaning ? PassengerAiV2InteriorSpotKind.Lean : PassengerAiV2InteriorSpotKind.Stand;
            PassengerAiV2InteriorSpotSmartObject best = null; float distance = 1.25f * 1.25f;
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                // Holding support also needs a real handrail anchor, not an arbitrary standing space.
                if (deliveryCatalog != null && !CurrentMap.interests[i].hasPoseAnchor) continue;
                float d = (spot.UsePosition - playerBody.position).sqrMagnitude;
                if (spot.Kind == kind && spot.IsAvailableFor(playerProxy) && d < distance &&
                    (deliveryCatalog == null || CurrentMap.Grid.ClearLine(playerBody.position, spot.UsePosition))) { distance = d; best = spot; }
            }
            if (best == null || !best.RequestUse(playerProxy, 0, Mathf.Sqrt(distance))) return false;
            if (deliveryCatalog != null)
            {
                ReleasePlayerFacility(); playerFacility = best;
                best.RequestUse(playerProxy, 0, 0);
                StartCoroutine(ApproachFacility(best, target)); return true;
            }
            if (!posture.TryTransition(target)) { if (best != playerFacility) best.Release(playerProxy); return false; }
            ReleasePlayerFacility(); playerFacility = best;
            best.RequestUse(playerProxy, 0, 0); best.MarkOccupied(playerProxy);
            return true;
        }

        public bool TryUseNearestFacility(out CarryPosture target)
        {
            target = CarryPosture.Standing;
            PassengerAiV2InteriorSpotSmartObject best = FindNearestFacility(null, out float distance);
            if (best == null) return false;
            target = GetPosture(best.Kind);
            return TryBeginFacilityUse(best, target, distance);
        }

        public bool TryGetNearestFacility(out Vector2 position, out CarryPosture target)
        {
            position = PlayerPosition;
            target = CarryPosture.Standing;
            PassengerAiV2InteriorSpotSmartObject best = FindNearestFacility(null, out _);
            if (best == null) return false;
            position = best.UsePosition;
            target = GetPosture(best.Kind);
            return true;
        }

        public bool TryReleasePlayerFacility()
        {
            if (playerFacility == null || facilityApproach || posture.IsTransitioning) return false;
            if (posture.CurrentState == CarryPosture.Fallen)
            {
                ReleasePlayerFacility();
                return true;
            }

            if (posture.CurrentState == CarryPosture.Standing)
            {
                ReleasePlayerFacility();
                return true;
            }

            if (!posture.TryTransition(CarryPosture.Standing)) return false;
            if (deliveryCatalog != null)
                StartCoroutine(LeaveFacility());
            else
                releasingPlayerFacility = true;
            return true;
        }

        PassengerAiV2InteriorSpotSmartObject FindNearestFacility(
            PassengerAiV2InteriorSpotKind? requiredKind,
            out float distance)
        {
            float maximumDistance = 1.25f * 1.25f;
            distance = maximumDistance;
            if (spots == null || playerBody == null || playerFacility != null || facilityApproach ||
                changing || posture == null || posture.IsTransitioning ||
                posture.CurrentState != CarryPosture.Standing)
            {
                return null;
            }

            PassengerAiV2InteriorSpotSmartObject best = null;
            for (int index = 0; index < spots.Length; index++)
            {
                PassengerAiV2InteriorSpotSmartObject spot = spots[index];
                if (spot == null || spot.Kind == PassengerAiV2InteriorSpotKind.DoorPrepare ||
                    (requiredKind.HasValue && spot.Kind != requiredKind.Value) ||
                    !spot.IsAvailableFor(playerProxy))
                {
                    continue;
                }

                if (deliveryCatalog != null)
                {
                    if (!spot.isActiveAndEnabled || CurrentMap.interests == null ||
                        index >= CurrentMap.interests.Length ||
                        !CurrentMap.interests[index].hasPoseAnchor ||
                        !CurrentMap.Grid.ClearLine(playerBody.position, spot.UsePosition))
                    {
                        continue;
                    }
                }

                float candidateDistance = (spot.UsePosition - playerBody.position).sqrMagnitude;
                if (candidateDistance >= maximumDistance) continue;
                if (best != null)
                {
                    if (candidateDistance > distance + FacilityDistanceTieEpsilon) continue;
                    if (Mathf.Abs(candidateDistance - distance) <= FacilityDistanceTieEpsilon &&
                        GetFacilityPriority(spot.Kind) >= GetFacilityPriority(best.Kind))
                    {
                        continue;
                    }
                }

                distance = candidateDistance;
                best = spot;
            }

            return best;
        }

        bool TryBeginFacilityUse(
            PassengerAiV2InteriorSpotSmartObject facility,
            CarryPosture target,
            float distance)
        {
            if (facility == null ||
                !facility.RequestUse(playerProxy, 0, Mathf.Sqrt(distance)))
            {
                return false;
            }

            playerFacility = facility;
            releasingPlayerFacility = false;
            if (deliveryCatalog != null)
            {
                StartCoroutine(ApproachFacility(facility, target));
                return true;
            }

            if (!facility.MarkOccupied(playerProxy))
            {
                ReleasePlayerFacility();
                return false;
            }

            if (!posture.TryTransition(target))
            {
                ReleasePlayerFacility();
                return false;
            }

            AlignPlayerWithFacility(facility, target);
            return true;
        }

        IEnumerator LeaveFacility()
        {
            PlayerLeavingFacility = true;
            facilityApproach = true;
            boundary.locked = true;
            playerInput.enabled = false;
            float elapsed = 0;
            while (elapsed < deliveryCatalog.facilityPoseSeconds + .05f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            ReleasePlayerFacility();
            PlayerLeavingFacility = false;
            facilityApproach = false;
            SetInputBlocked(journey != null && journey.InputBlocked);
        }

        IEnumerator ApproachFacility(
            PassengerAiV2InteriorSpotSmartObject facility,
            CarryPosture target)
        {
            facilityApproach = true;
            boundary.locked = true;
            boundary.ClearImpulse();
            playerInput.enabled = false;
            float elapsed = 0;
            while ((playerBody.position - facility.UsePosition).sqrMagnitude > .0025f && elapsed < 2)
            {
                if (posture.CurrentState == CarryPosture.Fallen || balance.IsActive) break;
                Vector2 next = Vector2.MoveTowards(
                    playerBody.position,
                    facility.UsePosition,
                    1.8f * Time.deltaTime);
                next = CurrentMap.Grid.Constrain(
                    playerBody.position,
                    next,
                    CurrentMap.characterRadius);
                playerBody.position = next;
                player.transform.position = next;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if ((playerBody.position - facility.UsePosition).sqrMagnitude < .04f && posture.TryTransition(target)) facility.MarkOccupied(playerProxy);
            else ReleasePlayerFacility();

            facilityApproach = false;
            SetInputBlocked(journey != null && journey.InputBlocked);
        }

        void AlignPlayerWithFacility(
            PassengerAiV2InteriorSpotSmartObject facility,
            CarryPosture target)
        {
            if (facility == null || playerBody == null) return;

            Vector2 usePosition = facility.UsePosition;
            playerBody.position = usePosition;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.transform.position = usePosition;
            if (boundary != null) boundary.ClearImpulse();

            if (playerInput != null &&
                TryGetFacilityFacingDirection(usePosition, target, out Vector2 facing))
            {
                playerInput.SetFacingDirection(facing);
            }

            Physics2D.SyncTransforms();
            PackageImpactSensor[] sensors =
                playerBody.GetComponentsInChildren<PackageImpactSensor>(true);
            for (int index = 0; index < sensors.Length; index++)
            {
                if (!sensors[index].enabled) continue;
                sensors[index].enabled = false;
                sensors[index].enabled = true;
            }
        }

        bool TryGetFacilityFacingDirection(
            Vector2 usePosition,
            CarryPosture target,
            out Vector2 facing)
        {
            facing = Vector2.zero;
            if (target == CarryPosture.HoldingSupport || maps == null ||
                currentMap < 0 || currentMap >= maps.Length || maps[currentMap] == null)
            {
                return false;
            }

            SliceSolidFootprint[] footprints = maps[currentMap].solidFootprints;
            if (footprints == null || footprints.Length == 0) return false;

            float nearestDistance = 2.25f;
            Vector2 nearestCenter = Vector2.zero;
            bool found = false;
            for (int index = 0; index < footprints.Length; index++)
            {
                float candidateDistance =
                    (usePosition - footprints[index].center).sqrMagnitude;
                if (candidateDistance >= nearestDistance) continue;
                nearestDistance = candidateDistance;
                nearestCenter = footprints[index].center;
                found = true;
            }

            if (!found) return false;
            facing = usePosition - nearestCenter;
            return facing.sqrMagnitude > 0.0001f;
        }

        static int GetFacilityPriority(PassengerAiV2InteriorSpotKind kind)
        {
            switch (kind)
            {
                case PassengerAiV2InteriorSpotKind.Seat:
                    return 0;
                case PassengerAiV2InteriorSpotKind.Lean:
                    return 1;
                default:
                    return 2;
            }
        }

        static CarryPosture GetPosture(PassengerAiV2InteriorSpotKind kind)
        {
            return kind == PassengerAiV2InteriorSpotKind.Seat
                ? CarryPosture.Sitting
                : kind == PassengerAiV2InteriorSpotKind.Lean
                    ? CarryPosture.Leaning
                    : CarryPosture.HoldingSupport;
        }

        public bool TryGetFacilityHint(out SliceInterest data)
        {
            data = default; float distance = 1.5625f; bool found = false;
            if (spots == null || facilityApproach) return false;
            for (int i = 0; i < spots.Length; i++)
            {
                var s = spots[i];
                if ((s.Kind != PassengerAiV2InteriorSpotKind.Seat && s.Kind != PassengerAiV2InteriorSpotKind.Lean && s.Kind != PassengerAiV2InteriorSpotKind.Stand) ||
                    !CurrentMap.interests[i].hasPoseAnchor || !s.IsAvailableFor(playerProxy)) continue;
                float d = (s.UsePosition - PlayerPosition).sqrMagnitude;
                if (d >= distance || !CurrentMap.Grid.ClearLine(PlayerPosition, s.UsePosition)) continue;
                distance = d; data = CurrentMap.interests[i]; found = true;
            }
            return found;
        }
        public bool TryGetOccupiedPose(PassengerAiV2Agent actor, out SliceInterest pose)
        {
            pose = default; if (spots == null) return false;
            for (int i = 0; i < spots.Length; i++)
                if (spots[i].IsOccupied && spots[i].HasReservation(actor == null ? playerProxy : actor) && CurrentMap.interests[i].hasPoseAnchor)
                { pose = CurrentMap.interests[i]; return true; }
            return false;
        }
        void ReleasePlayerFacility()
        {
            if (playerFacility != null) playerFacility.Release(playerProxy);
            playerFacility = null;
            releasingPlayerFacility = false;
        }
        public Vector2 PlayerPosition => playerBody.position;
        public void SetInputBlocked(bool blocked)
        {
            boundary.locked = blocked || changing || facilityApproach;
            playerInput.enabled = !blocked && !changing && !facilityApproach;
        }
        public void NotifyService(PassengerAiV2ServicePhase phase, bool destination)
        { environment.NotifyService(phase, true, destination); }
        public void TravelTo(int mapIndex, Vector2 position)
        {
            if (!changing) StartCoroutine(Transition(new SlicePortal { targetMap = mapIndex, arrival = position }));
        }
        public void TravelVia(SlicePortal portal) { if (!changing) StartCoroutine(Transition(portal)); }

        void Start()
        {
            whiteTexture = new Texture2D(1, 1); whiteTexture.SetPixel(0, 0, Color.white); whiteTexture.Apply();
            sharedSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            if (deliveryCatalog != null)
            {
                foreach (var map in maps)
                    foreach (var source in map.GetComponentsInChildren<SliceStaticImpactSource>(true)) Destroy(source);
                gameObject.AddComponent<SliceTrainLayout>().Build(maps[1], maps[0]);
                System.Array.Resize(ref maps, 7);
                // Separate runtime instances share the teammate's art, not the direction or door state.
                maps[6] = Instantiate(maps[0], transform); maps[6].name = "Gachon_Wangsimni_Bound_Platform";
                maps[6].displayName = "가천대역 · 왕십리 방면"; maps[6].travelDirection = SliceTravelDirection.Wangsimni;
                maps[0].displayName = "가천대역 · 정자 방면"; maps[0].travelDirection = SliceTravelDirection.Jeongja;
                maps[3] = SliceStationLayout.Create(transform, maps[0], "가천대역 대합실", true, 0);
                maps[4] = SliceStationLayout.Create(transform, maps[0], "목적역 대합실", true, 2);
                maps[4].fareGate = SliceTrainLayout.Project(7, 3);
                maps[5] = SliceStationLayout.Create(transform, maps[0], "환승 연결 통로", false, 2);
                foreach (int index in new[] { 0, 2, 6 })
                {
                    SliceFacilityAnchors.RepairImportedFurniture(maps[index]);
                    SliceStationLayout.ConnectPlatformAccess(maps[index], index == 2 ? 4 : 3, SliceTrainLayout.Project(11, 3));
                    SliceFacilityAnchors.BindImported(maps[index]);
                }
                for (int i = 0; i < maps[3].portals.Length; i++)
                { var p = maps[3].portals[i]; p.arrival = maps[p.targetMap].entry; maps[3].portals[i] = p; }
            }
            for (int i = 0; i < maps.Length; i++) maps[i].gameObject.SetActive(false);
            player = Instantiate(playerPrefab); player.name = "PLAYER_CyanOutline"; player.transform.SetParent(transform, true);
            playerBody = player.GetComponent<Rigidbody2D>(); playerBody.gravityScale = 0; playerBody.freezeRotation = true;
            playerInput = player.GetComponent<PlayerController>(); posture = player.GetComponent<PlayerPosture>(); balance = player.GetComponent<PlayerBalance>();
            boundary = player.GetComponent<SlicePlayerBoundary>();
            if (boundary == null) boundary = player.AddComponent<SlicePlayerBoundary>();
            var visual = player.AddComponent<SliceCharacterVisual>(); visual.player = true;
            visual.world = this;
            visual.idleSprites = idleSprites;
            visual.sittingSprites = deliveryCatalog != null ? deliveryCatalog.passengerSittingSprites : null;
            visual.body = player.transform.Find("BodyVisual").GetComponent<SpriteRenderer>(); visual.body.sharedMaterial = playerOutline;
            GameObject package = Instantiate(packagePrefab); durability = package.GetComponent<PackageDurability>();
            player.GetComponent<PlayerPackageCarrier>().SetPackage(package.transform);
            int startMap = deliveryCatalog != null ? 3 : 0;
            ActivateMap(startMap, maps[startMap].entry);
            if (deliveryCatalog != null)
            {
                journey = gameObject.AddComponent<SliceJourneyController>();
                journey.Initialize(this, deliveryCatalog);
                occlusion = gameObject.AddComponent<SliceSceneOcclusion>(); occlusion.Initialize(this);
            }
        }

        void ActivateMap(int index, Vector2 arrival, bool doorArrival = false)
        {
            ReleasePlayerFacility();
            if (currentMap == 1)
                foreach (var passenger in passengers)
                    if (passenger != null && !passenger.HasLeftTrain) aboard[passenger.PassengerId] = passenger.DestinationStop;
            if (actorsRoot != null) { actorsRoot.SetActive(false); Destroy(actorsRoot); }
            passengers.Clear();
            for (int i = 0; i < maps.Length; i++) maps[i].gameObject.SetActive(i == index);
            currentMap = index; CurrentMap.EnsureGrid();
            boundary.grid = CurrentMap.Grid;
            boundary.radius = CurrentMap.characterRadius;
            boundary.journey = journey;
            player.transform.localScale = new Vector3(CurrentMap.characterScale, CurrentMap.characterScale, 1);
            // The character's art size and its ground footprint are separate measurements.
            var playerCollider = player.GetComponent<BoxCollider2D>();
            if (playerCollider != null) playerCollider.size = Vector2.one * (CurrentMap.characterRadius * 1.414214f / CurrentMap.characterScale);
            var playerVisual = player.GetComponent<SliceCharacterVisual>();
            playerVisual.ResetFacilityPose(); playerVisual.map = CurrentMap;
            Vector2 spawn = doorArrival ? arrival : CurrentMap.Grid.Nearest(arrival);
            playerBody.position = spawn; playerBody.linearVelocity = Vector2.zero; player.transform.position = spawn;
            Physics2D.SyncTransforms();
            // Spawning/teleporting is not a physical impact. Reset the sensor's previous-position sample.
            var activeSensors = player.GetComponentsInChildren<PackageImpactSensor>(true);
            for (int i = 0; i < activeSensors.Length; i++) if (activeSensors[i].enabled) { activeSensors[i].enabled = false; activeSensors[i].enabled = true; }
            gameplayCamera.transform.position = new Vector3(spawn.x, spawn.y + 1.2f, -30);
            gameplayCamera.orthographicSize = CurrentMap.cameraSize;
            actorsRoot = new GameObject("Runtime_Passengers_And_SmartObjects"); actorsRoot.transform.SetParent(CurrentMap.transform, false);
            // Art scale is not simulation scale: keep character speed/radius consistent between station and train.
            actorsRoot.transform.localScale = Vector3.one / CurrentMap.transform.lossyScale.x;
            crowd = actorsRoot.AddComponent<PassengerAiV2CrowdManager>();
            spots = new PassengerAiV2InteriorSpotSmartObject[CurrentMap.interests.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var data = CurrentMap.interests[i]; var go = new GameObject("Facility_" + data.kind + "_" + i); go.transform.SetParent(actorsRoot.transform, false);
                go.transform.position = data.position; spots[i] = go.AddComponent<PassengerAiV2InteriorSpotSmartObject>();
                spots[i].Configure("slice_" + currentMap + "_" + i, data.kind, data.position, data.comfort, 0.5f, null);
                if (deliveryCatalog != null && (data.kind == PassengerAiV2InteriorSpotKind.Seat || data.kind == PassengerAiV2InteriorSpotKind.Lean) && !data.hasPoseAnchor)
                    spots[i].enabled = false;
            }
            environment = actorsRoot.AddComponent<SlicePlaceEnvironment>(); environment.Configure(CurrentMap, crowd, spots);
            var proxy = CreateAgent("Player_Perception_Only", spawn, 0);
            playerProxy = proxy;
            proxy.FollowExternalMotion(player.transform, CurrentMap.characterRadius);
            if (index == 1)
            {
                foreach (var record in aboard) SpawnPassenger(true, record.Key, record.Value);
            }
            else if (journey != null && journey.StopIndex > 0)
            {
                foreach (var record in alighted) SpawnPassenger(false, record.Key, record.Value, true);
                alighted.Clear();
            }
            for (int i = passengers.Count; i < Mathf.Min(10, maximumPassengers); i++) SpawnPassenger(true);
            nextSpawn = Time.time + spawnInterval; portalReadyAt = Time.time + 1; nearestPortal = -1;
            if (PlatformTrain != null) PlatformTrain.Reenter();
            if (occlusion != null) occlusion.Rebind();
        }

        PassengerAiV2Agent CreateAgent(string label, Vector2 position, int id)
        {
            var go = new GameObject(label); go.transform.SetParent(actorsRoot.transform, false);
            go.AddComponent<SpriteRenderer>(); go.AddComponent<CircleCollider2D>(); go.AddComponent<Rigidbody2D>();
            var agent = go.AddComponent<PassengerAiV2Agent>();
            var p = Personality(id);
            agent.Initialize(crowd, position, position, p, (id % 17) / 17f,
                CurrentMap.floorBounds.yMin - 1, CurrentMap.floorBounds.yMax + 1, sharedSprite, Color.white);
            agent.SetAutoLoop(false); agent.SetMovementBounds(CurrentMap.floorBounds);
            var follower = new SlicePathFollower(CurrentMap.Grid);
            agent.ConfigureMovementSpace(follower.Resolve, CurrentMap.Grid.Constrain, CurrentMap.characterRadius);
            go.transform.localScale = Vector3.one;
            go.GetComponent<CircleCollider2D>().radius = CurrentMap.characterRadius;
            go.GetComponent<SpriteRenderer>().enabled = false; go.transform.Find("FacingDirection").gameObject.SetActive(false);
            return agent;
        }
        PassengerAiV2RuntimePersonality Personality(int id)
        {
            var profiles = deliveryCatalog != null ? deliveryCatalog.passengerProfiles : null;
            var profile = id > 0 && profiles != null && profiles.Length > 0 ? profiles[id % profiles.Length] : baselineProfile;
            return (profile != null ? profile : baselineProfile).CreateRuntimePersonality(seed + id);
        }

        SlicePassengerIntent SpawnPassenger(bool scatter, int restoredId = 0, int destinationStop = -1, bool disembarked = false, Vector2? exactStart = null)
        {
            for (int i = passengers.Count - 1; i >= 0; i--) if (passengers[i] == null) passengers.RemoveAt(i);
            if (passengers.Count >= maximumPassengers + (disembarked ? 6 : 0) || CurrentMap.exits.Length == 0) return null;
            int id = restoredId > 0 ? restoredId : ++serial;
            var samples = CurrentMap.Grid.SamplePoints;
            Vector2 position = scatter && samples.Count > 0
                ? CurrentMap.Grid.Nearest(samples[(id * 37) % samples.Count])
                : CurrentMap.exits[id % CurrentMap.exits.Length].inside;
            if (disembarked && CurrentMap.portals.Length > 0) position = CurrentMap.Grid.Nearest(CurrentMap.portals[id % CurrentMap.portals.Length].position + new Vector2(.4f, -.6f));
            if (exactStart.HasValue) position = exactStart.Value;
            if ((position - (Vector2)player.transform.position).sqrMagnitude < 1.2f) return null;
            // Initial population is checked directly; later arrivals use the shared spatial hash.
            float spacing = exactStart.HasValue ? .6f : .8944272f;
            for (int i = 0; i < passengers.Count; i++) if (passengers[i] != null && ((Vector2)passengers[i].transform.position - position).sqrMagnitude < spacing * spacing) return null;
            crowd.QueryNearby(position, exactStart.HasValue ? .6f : .8f, null, nearby); if (nearby.Count > 0) return null;
            var agent = CreateAgent("Passenger_" + id, position, id);
            var intent = agent.gameObject.AddComponent<SlicePassengerIntent>();
            int waiting = 0;
            for (int i = 0; i < passengers.Count; i++) if (passengers[i] != null && !passengers[i].IsPassingThrough) waiting++;
            PassengerAiV2LocalGoal goal;
            if (disembarked) goal = PassengerAiV2LocalGoal.PassThrough;
            else if (CurrentMap.placeKind == PassengerAiV2PlaceKind.TrainInterior) goal = PassengerAiV2LocalGoal.RideToDestination;
            else if (waiting >= waitingPassengerCapacity || id % 3 == 0) goal = PassengerAiV2LocalGoal.PassThrough;
            else goal = CurrentMap.placeKind == PassengerAiV2PlaceKind.Platform ? PassengerAiV2LocalGoal.WaitForService : PassengerAiV2LocalGoal.WaitHere;
            if (destinationStop < 0)
            {
                int current = journey != null ? journey.StopIndex : 0;
                int remaining = journey != null && journey.Order != null ? journey.Order.stops.Length - current - 1 : 3;
                destinationStop = current + 1 + id % Mathf.Max(1, remaining);
            }
            intent.Initialize(agent, CurrentMap, this, Personality(id), environment, activitySettings, goal, seed + id, id, destinationStop);
            var bodyObject = new GameObject("Passenger_Art"); bodyObject.transform.SetParent(agent.transform, false);
            var body = bodyObject.AddComponent<SpriteRenderer>(); body.sprite = idleSprites[0]; body.color = Personality(id).DebugColor;
            body.transform.localScale = Vector3.one * CurrentMap.characterScale;
            var visual = agent.gameObject.AddComponent<SliceCharacterVisual>(); visual.body = body; visual.agent = agent; visual.intent = intent;
            visual.world = this;
            visual.idleSprites = idleSprites; visual.walkSprites = walkSprites;
            visual.sittingSprites = deliveryCatalog != null ? deliveryCatalog.passengerSittingSprites : null;
            visual.map = CurrentMap; visual.visualScale = CurrentMap.characterScale;
            passengers.Add(intent);
            return intent;
        }

        void Update()
        {
            if (player == null) return;
            if (playerFacility != null && !facilityApproach && !posture.IsTransitioning &&
                posture.CurrentState != CarryPosture.Sitting && posture.CurrentState != CarryPosture.Leaning && posture.CurrentState != CarryPosture.HoldingSupport) ReleasePlayerFacility();
            if (!changing && Time.time >= nextSpawn && (journey == null || journey.AllowPassengerArrival))
            {
                nextSpawn = Time.time + spawnInterval;
                for (int i = passengers.Count - 1; i >= 0; i--) if (passengers[i] == null) passengers.RemoveAt(i);
                SpawnPassenger(false);
            }
            if (Time.time >= nextHud)
            {
                int staying = 0, passing = 0, changes = 0, routeWaits = 0;
                for (int i = 0; i < passengers.Count; i++) if (passengers[i] != null)
                {
                    var intent = passengers[i];
                    if (intent.IsPassingThrough) passing++;
                    if (intent.State == PassengerAiV2ActivityState.Staying || intent.State == PassengerAiV2ActivityState.ReadyForTraversal) staying++;
                    if (intent.State == PassengerAiV2ActivityState.WaitingForRoute) routeWaits++;
                    changes += intent.TargetChanges;
                }
                nextHud = Time.time + 0.2f; nearestPortal = -1; float closest = 1.6f * 1.6f;
                for (int i = 0; i < CurrentMap.portals.Length; i++) { float d = (CurrentMap.portals[i].position - (Vector2)player.transform.position).sqrMagnitude; if (d < closest) { closest = d; nearestPortal = i; } }
                prompt = nearestPortal >= 0 ? "E : " + CurrentMap.portals[nearestPortal].label : "Move to a cyan doorway marker, then press E";
                hud = CurrentMap.displayName + "   |   Passengers " + passengers.Count + "/" + maximumPassengers + "   Exited " + exited
                    + "\nWASD move | E map doorway | 1 stand / 2 lean / 3 sit / 4 hold / 5 overhead | Wheel zoom"
                    + "\n" + prompt + "\nPosture: " + posture.CurrentState + "   Stamina " + Mathf.RoundToInt(posture.StaminaRatio * 100) + "%"
                    + "   |   Box " + Mathf.RoundToInt(durability.CurrentDurability.BoxDurability) + "   Cake " + Mathf.RoundToInt(durability.CurrentDurability.CakeDurability)
                    + "\nA* " + CurrentMap.Grid.PathRequests + "   Failed " + CurrentMap.Grid.PathFailures + "   Stuck " + crowd.DeadlockedCount
                    + (balance.IsActive ? "   BALANCE: " + balance.CurrentPromptKey : "   6-9: train balance test")
                    + "\nStaying " + staying + "   Passing " + passing + "   Target changes " + changes + "   Route waits " + routeWaits + "   Context " + environment.Context.Service
                    + "\nF6 approach / F7 doors open / F8 departed: context signals ONLY (no train animation)";
            }
            if (journey == null && !changing && Keyboard.current != null)
            {
                if (Keyboard.current.f6Key.wasPressedThisFrame) environment.NotifyService(PassengerAiV2ServicePhase.Approaching, true, true);
                if (Keyboard.current.f7Key.wasPressedThisFrame) environment.NotifyService(PassengerAiV2ServicePhase.DoorsOpen, true, true);
                if (Keyboard.current.f8Key.wasPressedThisFrame) environment.NotifyService(PassengerAiV2ServicePhase.Departed, true, true);
            }
            if (journey == null && !changing && nearestPortal >= 0 && Time.time >= portalReadyAt && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                var portal = CurrentMap.portals[nearestPortal];
                StartCoroutine(Transition(portal));
            }
            if (journey != null && !journey.InputBlocked && !changing && !IsUsingFacility && Time.time >= portalReadyAt && nearestPortal >= 0)
            {
                var portal = CurrentMap.portals[nearestPortal];
                if (portal.guidedTraversal && (portal.position - PlayerPosition).sqrMagnitude < .36f &&
                    Vector2.Dot(playerInput.MoveInput, (portal.traversalEnd - portal.position).normalized) > .25f)
                    journey.TryUseDoor(portal);
            }
            if (Mouse.current != null && (journey == null || !journey.InputBlocked)) gameplayCamera.orthographicSize = Mathf.Clamp(gameplayCamera.orthographicSize - Mouse.current.scroll.ReadValue().y * 0.012f, 4f, 18f);
        }
        void LateUpdate()
        {
            if (player == null || changing) return;
            Vector3 target = new Vector3(player.transform.position.x, player.transform.position.y + 1.2f, -30);
            gameplayCamera.transform.position = Vector3.Lerp(gameplayCamera.transform.position, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        IEnumerator Transition(SlicePortal portal)
        {
            changing = true; boundary.locked = true; playerInput.enabled = false;
            balance.CancelCurrentChallenge();
            var sensors = player.GetComponentsInChildren<PackageImpactSensor>(true);
            for (int i = 0; i < sensors.Length; i++) sensors[i].enabled = false;
            boundary.ClearImpulse();
            if (portal.guidedTraversal)
            {
                playerBody.simulated = false;
                Vector2 start = playerBody.position;
                float duration = Mathf.Max(.5f, portal.traversalSeconds);
                for (float t = 0; t < duration; t += Time.deltaTime)
                {
                    float progress = t / duration;
                    Vector2 p = Vector2.Lerp(start, portal.traversalEnd, Mathf.SmoothStep(0, 1, progress));
                    playerBody.position = p; player.transform.position = p;
                    fade = Mathf.InverseLerp(.8f, 1, progress);
                    yield return null;
                }
            }
            if (!portal.guidedTraversal)
                for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) { fade = t / 0.25f; yield return null; }
            fade = 1; ActivateMap(portal.targetMap, portal.arrival, portal.arriveThroughDoor);
            if (journey != null) journey.RefreshDoorPresentation();
            yield return null;
            if (portal.arriveThroughDoor)
            {
                playerBody.simulated = false; fade = 0;
                Vector2 start = playerBody.position;
                for (float t = 0; t < 1.3f; t += Time.deltaTime)
                {
                    Vector2 p = Vector2.Lerp(start, portal.arrivalTraversalEnd, Mathf.Clamp01(t / 1.3f));
                    playerBody.position = p; player.transform.position = p; yield return null;
                }
                playerBody.position = portal.arrivalTraversalEnd; player.transform.position = portal.arrivalTraversalEnd;
            }
            playerBody.simulated = true;
            for (int i = 0; i < sensors.Length; i++) sensors[i].enabled = true;
            if (!portal.arriveThroughDoor)
                for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) { fade = 1 - t / 0.25f; yield return null; }
            fade = 0; boundary.locked = false; changing = false; playerInput.enabled = true;
            if (journey != null) journey.MapActivated();
        }
        public void ReportPassengerExit() { exited++; }
        public void ForgetPreviousStationAlighting() { alighted.Clear(); }
        void OnGUI()
        {
            if (hudStyle == null) hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
            if (journey == null) { GUI.Box(new Rect(12, 12, 850, 175), GUIContent.none); GUI.Label(new Rect(24, 20, 828, 160), hud, hudStyle); }
            if (fade > 0) { Color previous = GUI.color; GUI.color = new Color(0, 0, 0, fade); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = previous; }
        }
        void OnDestroy() { if (sharedSprite != null) Destroy(sharedSprite); if (whiteTexture != null) Destroy(whiteTexture); }
    }
}
