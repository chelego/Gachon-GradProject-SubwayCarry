namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// NPC가 시설의 실제 좌표를 외우지 않고 예약과 사용 가능 여부를 요청하는 공통 계약이다.
    /// 개찰구, 계단, 열차 문과 좌석도 이 계약을 기반으로 확장한다.
    /// </summary>
    public interface IPassengerAiV2SmartObject
    {
        string SmartObjectId { get; }
        int Capacity { get; }
        bool RequestUse(PassengerAiV2Agent agent, int direction, float approachDistance);
        bool HasReservation(PassengerAiV2Agent agent);
        void Release(PassengerAiV2Agent agent);
        void Cancel(PassengerAiV2Agent agent);
    }
}
