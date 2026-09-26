namespace OpenCodeTray;

/// <summary>Single usage window (rolling 5h, weekly or monthly).</summary>
public sealed class UsageWindow
{
    public required string Status { get; init; }
    public required double Percent { get; init; }
    public required DateTimeOffset ResetsAt { get; init; }

    public bool IsRateLimited => string.Equals(Status, "rate-limited", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Snapshot of all OpenCode Go usage limits at a point in time.</summary>
public sealed class UsageSnapshot
{
    public required UsageWindow Rolling { get; init; }
    public required UsageWindow Weekly { get; init; }
    public required UsageWindow Monthly { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }

    public bool IsAnyRateLimited => Rolling.IsRateLimited || Weekly.IsRateLimited || Monthly.IsRateLimited;
}
