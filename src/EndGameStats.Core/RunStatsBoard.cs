namespace EndGameStats.Core;

public sealed class RunStatsBoard
{
    private readonly Dictionary<string, PlayerRunStats> _players =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<PlayerRunStats> Players => _players.Values;
    public double UnattributedValuableDamage { get; private set; }
    public double UnattributedExtractedValue { get; private set; }

    public PlayerRunStats GetOrAddPlayer(string playerId, string displayName)
    {
        if (_players.TryGetValue(playerId, out var player))
        {
            player.Rename(displayName);
            return player;
        }

        player = new PlayerRunStats(playerId, displayName);
        _players.Add(playerId, player);
        return player;
    }

    public void AddUnattributedDamage(double valueLost)
    {
        if (double.IsNaN(valueLost) || double.IsInfinity(valueLost) || valueLost < 0)
            throw new ArgumentOutOfRangeException(nameof(valueLost));
        UnattributedValuableDamage += valueLost;
    }

    public void AddUnattributedExtractedValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));

        UnattributedExtractedValue += value;
    }

    public IReadOnlyList<PlayerRunStats> Leaders(Func<PlayerRunStats, double> value,
        bool lowerIsBetter = false, Func<PlayerRunStats, bool>? eligible = null)
    {
        var candidates = _players.Values
            .Where(eligible ?? (_ => true))
            .ToArray();
        if (candidates.Length == 0)
            return Array.Empty<PlayerRunStats>();

        var best = lowerIsBetter
            ? candidates.Min(value)
            : candidates.Max(value);

        return candidates
            .Where(player => NearlyEqual(value(player), best))
            .OrderBy(player => player.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool NearlyEqual(double left, double right)
    {
        var scale = Math.Max(1d, Math.Max(Math.Abs(left), Math.Abs(right)));
        return Math.Abs(left - right) <= 1e-9 * scale;
    }
}
