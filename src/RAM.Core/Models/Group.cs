namespace RAM.Core.Models;

public sealed record Group
{
    public required string Name { get; init; }
    public string? Color { get; init; }

    public int SortKey
    {
        get
        {
            var span = Name.AsSpan().TrimStart();
            var i = 0;
            while (i < span.Length && char.IsDigit(span[i])) i++;
            return i > 0 && int.TryParse(span[..i], out var n) ? n : int.MaxValue;
        }
    }
}
