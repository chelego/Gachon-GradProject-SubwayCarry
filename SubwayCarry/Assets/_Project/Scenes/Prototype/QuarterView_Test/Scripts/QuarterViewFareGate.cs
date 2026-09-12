using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Prototype.QuarterView
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class QuarterViewFareGate : MonoBehaviour
    {
        [SerializeField] private Collider2D passageBarrier;
        [SerializeField] private GameObject barrierVisual;
        [SerializeField] private Renderer cardReaderRenderer;
        [SerializeField] private Material openReaderMaterial;

        private bool playerNearby;

        public bool IsOpen { get; private set; }

        public void Configure(
            Collider2D barrier,
            GameObject visual,
            Renderer reader,
            Material openedMaterial)
        {
            passageBarrier = barrier;
            barrierVisual = visual;
            cardReaderRenderer = reader;
            openReaderMaterial = openedMaterial;
            GetComponent<Collider2D>().isTrigger = true;
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            if (passageBarrier != null) passageBarrier.enabled = false;
            if (barrierVisual != null) barrierVisual.SetActive(false);
            if (cardReaderRenderer != null && openReaderMaterial != null)
            {
                cardReaderRenderer.sharedMaterial = openReaderMaterial;
            }
        }

        private void Update()
        {
            if (!IsOpen && playerNearby && Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                Open();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<QuarterViewTestPlayer>() != null)
            {
                playerNearby = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<QuarterViewTestPlayer>() != null)
            {
                playerNearby = false;
            }
        }
    }
}
