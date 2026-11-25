namespace Sora.Blazor.Components;

public sealed record CollectionColumn<TItem>(
    string Key,
    string Label,
    Func<TItem, string?> SearchValue,
    Func<TItem, IComparable?> SortValue);
