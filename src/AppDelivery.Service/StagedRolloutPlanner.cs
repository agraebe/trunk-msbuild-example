namespace Contoso.AppDelivery.Service;

/// <summary>Splits a fleet into staged rollout waves (10% / 40% / 100%).</summary>
public sealed class StagedRolloutPlanner
{
    private static readonly double[] WaveFractions = { 0.10, 0.40, 1.00 };

    public IReadOnlyList<int> PlanWaveSizes(int totalDevices)
    {
        if (totalDevices < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDevices));
        }

        var sizes = new List<int>();
        var previousCumulative = 0;
        foreach (var fraction in WaveFractions)
        {
            var cumulative = (int)Math.Ceiling(totalDevices * fraction);
            sizes.Add(cumulative - previousCumulative);
            previousCumulative = cumulative;
        }

        return sizes;
    }
}
