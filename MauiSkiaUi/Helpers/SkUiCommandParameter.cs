namespace MauiSkiaUi;

/// <summary>When a command parameter setter can skip a value (issue #114).</summary>
internal static class SkUiCommandParameter
{
    /// <summary>
    /// Whether <paramref name="value"/> is no change from <paramref name="current"/>: the same object, or an equal primitive,
    /// enum, string or well-known value type, which the command cannot tell apart from the old one (a boxed int set again on
    /// every bind changes nothing). Any other object that only Equals the old one is a change the command needs: a list row
    /// replaced by a new instance with the same Id, also a struct whose Equals compares only some fields.
    /// </summary>
    public static bool IsSame(object? current, object? value) =>
        ReferenceEquals(current, value) || (IsPlainValue(current) && Equals(current, value));

    private static bool IsPlainValue(object? value) =>
        value is string or Enum or decimal or DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Guid
        || (value is not null && value.GetType().IsPrimitive);
}
