using System;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Pure rules are shared by the live journey and the non-visual regression check.
    public static class SliceDeliveryRules
    {
        public readonly struct Settlement
        {
            public readonly bool Success, UsedInsurance, UsedFareSupport;
            public readonly int Fee, Compensation, ReturnFare, NetIncome;
            public Settlement(bool success, bool insurance, bool support, int fee, int compensation, int outbound, int returnFare)
            {
                Success = success; UsedInsurance = insurance; UsedFareSupport = support;
                Fee = fee; Compensation = compensation; ReturnFare = returnFare; NetIncome = fee - compensation - outbound - returnFare;
            }
        }
        public static Settlement Evaluate(float box, float cake, bool missedStop, int value, int fee, int paidOutbound, int returnFare, bool insurance, bool fareSupport)
        {
            if (float.IsNaN(box) || float.IsInfinity(box) || float.IsNaN(cake) || float.IsInfinity(cake)) throw new ArgumentException("Invalid durability");
            if (value < 0 || fee < 0 || paidOutbound < 0 || returnFare < 0) throw new ArgumentOutOfRangeException("Amounts must be non-negative");
            cake = Math.Max(0, Math.Min(100, cake)); box = Math.Max(0, Math.Min(100, box));
            bool success = !missedStop && cake >= 20;
            int compensation = (int)Math.Round(value * (1 - cake / 100.0), MidpointRounding.AwayFromZero);
            bool insured = insurance && compensation > 0;
            if (insured) compensation = 0;
            int actualReturn = success && box >= 100 && cake >= 100 ? 0 : returnFare;
            bool supported = fareSupport && actualReturn > 0;
            if (supported) actualReturn = 0;
            return new Settlement(success, insured, supported, success ? fee : 0, compensation, paidOutbound, actualReturn);
        }
    }
}
