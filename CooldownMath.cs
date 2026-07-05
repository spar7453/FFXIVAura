namespace FFXIVAura;

internal static class CooldownMath
{
    public static (float DisplayTotal, float Remaining) GetTiming(
        float total,
        float elapsed,
        uint charges,
        uint maxCharges,
        float configuredCooldown)
    {
        total = Math.Max(0f, total);

        if (maxCharges <= 1)
        {
            var remaining = total <= 0f || elapsed <= 0f ? 0f : Math.Max(0f, total - elapsed);
            return (total, remaining);
        }

        var chargeTotal = GetChargeCooldownTotal(total, maxCharges, configuredCooldown);
        if (chargeTotal <= 0f)
            return (chargeTotal, 0f);

        if (charges >= maxCharges)
            return (chargeTotal, 0f);

        if (elapsed <= 0f)
            return (chargeTotal, chargeTotal);

        var elapsedInCharge = elapsed;
        if (elapsedInCharge > chargeTotal)
            elapsedInCharge %= chargeTotal;

        var remainingInCharge = Math.Max(0f, chargeTotal - elapsedInCharge);
        return (chargeTotal, remainingInCharge);
    }

    private static float GetChargeCooldownTotal(float total, uint maxCharges, float configuredCooldown)
    {
        if (total <= 0f || maxCharges <= 1)
            return total;

        if (configuredCooldown <= 0f)
            return total;

        var perChargeFromTotal = total / maxCharges;
        return Math.Abs(perChargeFromTotal - configuredCooldown) < Math.Abs(total - configuredCooldown)
            ? perChargeFromTotal
            : total;
    }
}
