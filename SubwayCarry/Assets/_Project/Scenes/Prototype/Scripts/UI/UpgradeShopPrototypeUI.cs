using SubwayCarry.Prototype.Delivery;
using UnityEngine;
using UnityEngine.UI;

namespace SubwayCarry.UI
{
    /// <summary>
    /// 강화 구매 최소 테스트용 UI.
    /// 버튼 하나로 지정한 능력치 강화를 구매 시도하고, 결과(성공/실패, 현재 레벨, 남은 현금)를 텍스트로 보여준다.
    /// </summary>
    public sealed class UpgradeShopPrototypeUI : MonoBehaviour
    {
        [Header("연결할 시스템 (Gameplay Services 오브젝트 드래그)")]
        [SerializeField] private UpgradeShopService upgradeShopService;
        [SerializeField] private EconomyService economyService;

        [Header("테스트할 능력치")]
        [SerializeField] private UpgradeStat statToTest = UpgradeStat.Stamina;

        [Header("UI 요소 (Inspector에서 연결)")]
        [SerializeField] private Button purchaseButton;
        [SerializeField] private Text resultText;

        private void Awake()
        {
            if (purchaseButton != null)
            {
                purchaseButton.onClick.AddListener(OnPurchaseClicked);
            }
        }

        private void OnPurchaseClicked()
        {
            if (upgradeShopService == null)
            {
                return;
            }

            bool success = upgradeShopService.TryPurchase(statToTest);
            int level = upgradeShopService.GetCurrentLevel(statToTest);
            int cash = economyService != null ? economyService.CurrentEconomyState.CurrentCash : -1;

            if (resultText == null)
            {
                return;
            }

            resultText.text = success
                ? $"{statToTest} 강화 구매 성공! 현재 레벨: {level} / 남은 현금: {cash}원"
                : $"{statToTest} 강화 구매 실패 (현금 부족 또는 최대 레벨) / 현재 레벨: {level} / 현금: {cash}원";
        }
    }
}
