using System.Collections;
using UnityEngine;

namespace SubwayCarry.Prototype.QuarterView
{
    [DisallowMultipleComponent]
    public sealed class QuarterViewTestFlow : MonoBehaviour
    {
        [SerializeField] private GameObject concourseMap;
        [SerializeField] private GameObject platformMap;
        [SerializeField] private QuarterViewTestPlayer player;
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.42f;
        [SerializeField] private Transform upboundPlatformSpawn;
        [SerializeField] private Transform downboundPlatformSpawn;
        [SerializeField] private Transform upboundConcourseSpawn;
        [SerializeField] private Transform downboundConcourseSpawn;

        private bool transitioning;

        public GameObject CurrentMap { get; private set; }
        public bool IsTransitioning => transitioning;
        public string CurrentMapName => CurrentMap != null ? CurrentMap.name : string.Empty;

        public void Configure(
            GameObject concourse,
            GameObject platform,
            QuarterViewTestPlayer controlledPlayer,
            CanvasGroup overlay,
            Transform upperPlatformSpawn,
            Transform lowerPlatformSpawn,
            Transform upperConcourseSpawn,
            Transform lowerConcourseSpawn)
        {
            concourseMap = concourse;
            platformMap = platform;
            player = controlledPlayer;
            fadeOverlay = overlay;
            upboundPlatformSpawn = upperPlatformSpawn;
            downboundPlatformSpawn = lowerPlatformSpawn;
            upboundConcourseSpawn = upperConcourseSpawn;
            downboundConcourseSpawn = lowerConcourseSpawn;
            ActivateOnly(concourseMap);
        }

        public bool TryTravel(GameObject destinationMap, Transform destinationSpawn)
        {
            if (transitioning || destinationMap == null || destinationSpawn == null || player == null)
            {
                return false;
            }

            StartCoroutine(TravelRoutine(destinationMap, destinationSpawn));
            return true;
        }

        public bool DebugGoToPlatform(bool upbound)
        {
            return TryTravel(platformMap, upbound ? upboundPlatformSpawn : downboundPlatformSpawn);
        }

        public bool DebugReturnToConcourse(bool upbound)
        {
            return TryTravel(concourseMap, upbound ? upboundConcourseSpawn : downboundConcourseSpawn);
        }

        private IEnumerator Start()
        {
            ActivateOnly(concourseMap);
            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 1f;
                fadeOverlay.blocksRaycasts = true;
            }

            player?.SetMovementEnabled(false);
            yield return null;
            yield return Fade(1f, 0f);
            player?.SetMovementEnabled(true);
        }

        private IEnumerator TravelRoutine(GameObject destinationMap, Transform destinationSpawn)
        {
            transitioning = true;
            player.SetMovementEnabled(false);
            yield return Fade(0f, 1f);

            ActivateOnly(destinationMap);
            player.Teleport(destinationSpawn.position);
            yield return null;
            yield return Fade(1f, 0f);

            player.SetMovementEnabled(true);
            transitioning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (fadeOverlay == null)
            {
                yield break;
            }

            fadeOverlay.blocksRaycasts = true;
            fadeOverlay.alpha = from;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }

            fadeOverlay.alpha = to;
            fadeOverlay.blocksRaycasts = to > 0.001f;
        }

        private void ActivateOnly(GameObject target)
        {
            if (concourseMap != null) concourseMap.SetActive(target == concourseMap);
            if (platformMap != null) platformMap.SetActive(target == platformMap);
            CurrentMap = target;
        }
    }
}
