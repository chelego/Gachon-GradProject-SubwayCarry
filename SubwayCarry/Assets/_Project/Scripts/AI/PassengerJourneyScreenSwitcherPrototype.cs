using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class PassengerJourneyScreenSwitcherPrototype : MonoBehaviour
    {
        [SerializeField] private GameObject initialScreen;
        [SerializeField] private GameObject[] screens;

        public GameObject CurrentScreen { get; private set; }

        public void Configure(GameObject firstScreen, GameObject[] availableScreens)
        {
            initialScreen = firstScreen;
            screens = availableScreens;
            Activate(initialScreen);
        }

        public void Activate(GameObject targetScreen)
        {
            if (screens == null || targetScreen == null)
            {
                return;
            }

            foreach (GameObject screen in screens)
            {
                if (screen != null)
                {
                    screen.SetActive(screen == targetScreen);
                }
            }

            CurrentScreen = targetScreen;
        }

        private void Awake()
        {
            Activate(initialScreen);
        }
    }
}
