namespace BeatmapHarvest.Core.Selection;

/// <summary>
/// Maps a two-thumb slider to a <see cref="NumericRange"/>. A thumb at either end of the track means
/// "no limit" on that side, so the last step reads as "10+" rather than "at most 10".
/// </summary>
public readonly record struct SliderRange(double TrackMin, double TrackMax)
{
    public NumericRange ToRange(double lower, double upper)
    {
        if (lower > upper)
            (lower, upper) = (upper, lower);
        return new NumericRange(
            lower <= TrackMin ? null : Math.Min(lower, TrackMax),
            upper >= TrackMax ? null : Math.Max(upper, TrackMin));
    }

    public (double Lower, double Upper) FromRange(NumericRange range) =>
        (Math.Clamp(range.Min ?? TrackMin, TrackMin, TrackMax), Math.Clamp(range.Max ?? TrackMax, TrackMin, TrackMax));
}
