using SubwayCarry.Prototype.Delivery;
using UnityEngine;
using UnityEngine.UI;

namespace SubwayCarry.UI
{
    /// <summary>
    /// 보험/교통비 지원 서비스 구매 최소 테스트용 UI.
    /// 버튼 하나로 지정한 서비스를 구매 시도하고, 결과(성공/실패, 보유 개수, 남은 현금)를 텍스트로 보여준다.
    /// </summary>
    public sealed class ServiceShopPrototypeUI : MonoBehaviour
    {
        [Header("연결할 시스템 (Gameplay Services 오브젝트 드래그)")]
        [SerializeField] private SchoolServiceShop serviceShop;
        [SerializeField] private EconomyService economyService;

        [Header("테스트할 서비스")]
        [SerializeField] private SchoolServiceType serviceToTest = SchoolServiceType.DeliveryInsurance;

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
            if (serviceShop == null)
            {
                return;
            }

            bool success = serviceShop.TryPurchase(serviceToTest);
            int owned = serviceShop.GetOwnedCount(serviceToTest);
            int cash = economyService != null ? economyService.CurrentEconomyState.CurrentCash : -1;

            if (resultText == null)
            {
                return;
            }

            resultText.text = success
                ? $"{serviceToTest} 구매 성공! 보유 개수: {owned} / 남은 현금: {cash}원"
                : $"{serviceToTest} 구매 실패 (현금 부족) / 보유 개수: {owned} / 현금: {cash}원";
        }
    }
}
