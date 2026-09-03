using SubwayCarry.Core.Contracts;
using SubwayCarry.Prototype.Delivery;
using UnityEngine;
using UnityEngine.UI;

namespace SubwayCarry.UI
{
    /// <summary>
    /// 배송 1-1 최소 테스트용 UI.
    /// 버튼 하나로 배송을 시작하고, 진행 상태·정산 결과·현재 현금을 텍스트로 보여준다.
    /// 나중에 진짜 배송 선택/정산 화면을 만들 때 이 스크립트를 참고하거나 교체하면 된다.
    /// </summary>
    public sealed class DeliveryPrototypeUI : MonoBehaviour
    {
        [Header("연결할 배송 시스템 (Gameplay Services 오브젝트 드래그)")]
        [SerializeField] private DeliveryService deliveryService;
        [SerializeField] private EconomyService economyService;

        [Header("시작할 배송 ID")]
        [SerializeField] private string deliveryIdToStart = "1-1";

        [Header("UI 요소 (Inspector에서 연결)")]
        [SerializeField] private Button startDeliveryButton;
        [SerializeField] private Text cashText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text settlementText;

        private void Awake()
        {
            if (startDeliveryButton != null)
            {
                startDeliveryButton.onClick.AddListener(OnStartDeliveryClicked);
            }
        }

        private void OnEnable()
        {
            if (economyService != null)
            {
                economyService.EconomyStateChanged += OnEconomyStateChanged;
            }

            if (deliveryService != null)
            {
                deliveryService.DeliveryStateChanged += OnDeliveryStateChanged;
                deliveryService.DeliverySettled += OnDeliverySettled;
            }

            RefreshCashText();
            RefreshStatusText(deliveryService != null
                ? deliveryService.CurrentDeliveryState.Phase
                : DeliveryPhase.None);

            if (settlementText != null)
            {
                settlementText.text = "정산 결과 없음";
            }
        }

        private void OnDisable()
        {
            if (economyService != null)
            {
                economyService.EconomyStateChanged -= OnEconomyStateChanged;
            }

            if (deliveryService != null)
            {
                deliveryService.DeliveryStateChanged -= OnDeliveryStateChanged;
                deliveryService.DeliverySettled -= OnDeliverySettled;
            }
        }

        private void OnStartDeliveryClicked()
        {
            if (deliveryService == null)
            {
                return;
            }

            bool started = deliveryService.TryStartDelivery(deliveryIdToStart);
            if (!started && statusText != null)
            {
                statusText.text = $"배송 시작 실패 (진행 중이거나 교통비 부족: {deliveryIdToStart})";
            }
        }

        private void OnEconomyStateChanged(EconomyStateSnapshot snapshot)
        {
            RefreshCashText(snapshot);
        }

        private void OnDeliveryStateChanged(DeliveryStateSnapshot snapshot)
        {
            RefreshStatusText(snapshot.Phase);
        }

        private void OnDeliverySettled(DeliverySettlementSnapshot snapshot)
        {
            if (settlementText == null)
            {
                return;
            }

            settlementText.text =
                $"배달 수수료: {snapshot.DeliveryFee}\n" +
                $"배상금: {snapshot.Compensation}\n" +
                $"출발 교통비: {snapshot.OutboundFare}\n" +
                $"복귀 교통비: {snapshot.ReturnFare}\n" +
                $"최종 손익: {snapshot.NetIncome}";
        }

        private void RefreshCashText()
        {
            if (economyService != null)
            {
                RefreshCashText(economyService.CurrentEconomyState);
            }
        }

        private void RefreshCashText(EconomyStateSnapshot snapshot)
        {
            if (cashText != null)
            {
                cashText.text = $"현재 현금: {snapshot.CurrentCash}원";
            }
        }

        private void RefreshStatusText(DeliveryPhase phase)
        {
            if (statusText != null)
            {
                statusText.text = $"배송 상태: {phase}";
            }
        }
    }
}
