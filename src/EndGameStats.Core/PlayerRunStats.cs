namespace EndGameStats.Core;

public sealed class PlayerRunStats
{
    public PlayerRunStats(string playerId, string displayName)
    {
        PlayerId = string.IsNullOrWhiteSpace(playerId)
            ? throw new ArgumentException("A stable player ID is required.", nameof(playerId))
            : playerId;
        DisplayName = displayName ?? string.Empty;
    }

    public string PlayerId { get; }
    public string DisplayName { get; private set; }
    public int Deaths { get; private set; }
    public int TeammatesRescued { get; private set; }
    public int EnemyKills { get; private set; }
    public double HaulWork { get; private set; }
    public double ExtractedValue { get; private set; }
    public double ValuableDamage { get; private set; }

    public void Rename(string displayName) => DisplayName = displayName ?? string.Empty;

    public void RecordDeath() => Deaths++;

    public void RecordRescue() => TeammatesRescued++;

    public void RecordEnemyKill() => EnemyKills++;

    public void AddHaulWork(double currentValue, double distanceMetres, int grabberCount = 1)
    {
        if (!IsFiniteNonNegative(currentValue))
            throw new ArgumentOutOfRangeException(nameof(currentValue));
        if (!IsFiniteNonNegative(distanceMetres))
            throw new ArgumentOutOfRangeException(nameof(distanceMetres));
        if (grabberCount < 1)
            throw new ArgumentOutOfRangeException(nameof(grabberCount));

        HaulWork += currentValue * distanceMetres / grabberCount;
    }

    public void AddValuableDamage(double valueLost)
    {
        if (!IsFiniteNonNegative(valueLost))
            throw new ArgumentOutOfRangeException(nameof(valueLost));

        ValuableDamage += valueLost;
    }

    public void AddExtractedValue(double value)
    {
        if (!IsFiniteNonNegative(value))
            throw new ArgumentOutOfRangeException(nameof(value));

        ExtractedValue += value;
    }

    private static bool IsFiniteNonNegative(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
}
