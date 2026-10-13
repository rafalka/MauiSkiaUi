namespace MauiSkiaUi;

/// <summary>
/// Which items of a list or a wheel can be chosen: <c>null</c> (all), or one flag per item (an item past the flags is enabled).
/// Disabled items show, but taps, the keyboard and type-ahead pass them by.
/// </summary>
internal static class SkUiItemStates
{
    public static bool IsEnabled(IReadOnlyList<bool>? states, int index) => states is null || index < 0 || index >= states.Count || states[index];

    /// <summary>
    /// The enabled item nearest to <paramref name="target"/>: <paramref name="target"/> itself, else the first one going
    /// <paramref name="step"/> (+1 or -1) from it, else the first one going the other way; -1 when none is enabled.
    /// <paramref name="wrap"/>: the items repeat (a looping wheel).
    /// </summary>
    public static int Nearest(IReadOnlyList<bool>? states, int count, int target, int step, bool wrap = false)
    {
        if (count <= 0)
            return -1;
        target = wrap ? (target % count + count) % count : Math.Clamp(target, 0, count - 1);
        if (IsEnabled(states, target))
            return target;
        foreach (var direction in new[] { step >= 0 ? 1 : -1, step >= 0 ? -1 : 1 })
            for (var offset = 1; offset < count; offset++)
            {
                var index = target + direction * offset;
                if (wrap)
                    index = (index % count + count) % count;
                else if (index < 0 || index >= count)
                    break;
                if (IsEnabled(states, index))
                    return index;
            }
        return -1;
    }
}
