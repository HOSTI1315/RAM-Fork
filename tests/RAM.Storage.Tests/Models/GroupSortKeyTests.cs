using RAM.Core.Models;

namespace RAM.Storage.Tests.Models;

public class GroupSortKeyTests
{
    [Theory]
    [InlineData("01 Farming", 1)]
    [InlineData("02 Trading", 2)]
    [InlineData("100 Bots", 100)]
    [InlineData("Trading", int.MaxValue)]
    [InlineData("", int.MaxValue)]
    [InlineData("  03 Indented", 3)]
    public void SortKey_extracts_leading_number(string name, int expected)
    {
        var group = new Group { Name = name };
        Assert.Equal(expected, group.SortKey);
    }

    [Fact]
    public void Groups_sort_by_numeric_prefix_then_name()
    {
        var groups = new List<Group>
        {
            new() { Name = "Trading" },
            new() { Name = "01 Farming" },
            new() { Name = "10 Bots" },
            new() { Name = "02 PvP" },
        };

        var sorted = groups.OrderBy(g => g.SortKey).ThenBy(g => g.Name).ToList();

        Assert.Equal("01 Farming", sorted[0].Name);
        Assert.Equal("02 PvP", sorted[1].Name);
        Assert.Equal("10 Bots", sorted[2].Name);
        Assert.Equal("Trading", sorted[3].Name);
    }
}
