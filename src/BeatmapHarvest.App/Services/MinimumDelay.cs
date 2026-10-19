namespace BeatmapHarvest.App.Services;

/// <summary>
/// Keeps a loader on screen for at least a moment. Cached or fast loads would otherwise finish before the first frame
/// (or flash for a few milliseconds), so the user can't tell anything happened.
/// </summary>
public static class MinimumDelay
{
    public static readonly TimeSpan Loader = TimeSpan.FromMilliseconds(100);

    public static async Task<T> AtLeast<T>(this Task<T> task, TimeSpan? minimum = null)
    {
        await Task.WhenAll(task, Task.Delay(minimum ?? Loader)).ConfigureAwait(true);
        return await task.ConfigureAwait(true);
    }

    public static async Task AtLeast(this Task task, TimeSpan? minimum = null) =>
        await Task.WhenAll(task, Task.Delay(minimum ?? Loader)).ConfigureAwait(true);
}
