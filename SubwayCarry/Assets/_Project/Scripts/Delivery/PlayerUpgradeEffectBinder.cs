using SubwayCarry.Gameplay;
using UnityEngine;

namespace SubwayCarry.Delivery
{
    [DisallowMultipleComponent]
    public sealed class PlayerUpgradeEffectBinder : MonoBehaviour
    {
        [SerializeField] private UpgradeShopService upgradeShopService;
        [SerializeField] private PlayerPosture playerPosture;
        [SerializeField] private PlayerBalance playerBalance;
        [SerializeField] private PlayerController playerController;

        private bool subscribed;

        public void Configure(
            UpgradeShopService shopService,
            PlayerPosture posture,
            PlayerBalance balance,
            PlayerController controller)
        {
            Unsubscribe();
            upgradeShopService = shopService;
            playerPosture = posture;
            playerBalance = balance;
            playerController = controller;
            ApplyAllEffects();
            Subscribe();
        }

        private void Awake()
        {
            ResolvePlayerComponents();
            ApplyAllEffects();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void ResolvePlayerComponents()
        {
            if (playerPosture == null)
            {
                playerPosture = GetComponent<PlayerPosture>();
            }

            if (playerBalance == null)
            {
                playerBalance = GetComponent<PlayerBalance>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || subscribed || upgradeShopService == null)
            {
                return;
            }

            upgradeShopService.UpgradePurchased += HandleUpgradePurchased;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || upgradeShopService == null)
            {
                subscribed = false;
                return;
            }

            upgradeShopService.UpgradePurchased -= HandleUpgradePurchased;
            subscribed = false;
        }

        private void ApplyAllEffects()
        {
            if (upgradeShopService == null)
            {
                return;
            }

            ApplyEffect(
                UpgradeStat.Stamina,
                upgradeShopService.GetCurrentEffectValue(UpgradeStat.Stamina));
            ApplyEffect(
                UpgradeStat.Balance,
                upgradeShopService.GetCurrentEffectValue(UpgradeStat.Balance));
            ApplyEffect(
                UpgradeStat.Agility,
                upgradeShopService.GetCurrentEffectValue(UpgradeStat.Agility));
        }

        private void HandleUpgradePurchased(
            UpgradeStat stat,
            int level,
            float effectValue)
        {
            ApplyEffect(stat, effectValue);
        }

        private void ApplyEffect(UpgradeStat stat, float effectValue)
        {
            switch (stat)
            {
                case UpgradeStat.Stamina:
                    playerPosture?.SetStaminaBonusPercent(effectValue);
                    break;

                case UpgradeStat.Balance:
                    playerBalance?.SetBalanceAssistPercent(effectValue);
                    break;

                case UpgradeStat.Agility:
                    playerController?.SetAgilityBonusPercent(effectValue);
                    break;
            }
        }
    }
}
