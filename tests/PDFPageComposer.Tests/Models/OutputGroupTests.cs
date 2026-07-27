using PDFPageComposer.App.Models;

namespace PDFPageComposer.Tests.Models;

public sealed class OutputGroupTests
{
    [Fact]
    public void Output_group_preserves_item_order()
    {
        var groupId = Guid.NewGuid();
        var sourceFileId = Guid.NewGuid();
        var items = new[]
        {
            new OutputPageItem(Guid.NewGuid(), groupId, sourceFileId, 3),
            new OutputPageItem(Guid.NewGuid(), groupId, sourceFileId, 4),
            new OutputPageItem(Guid.NewGuid(), groupId, sourceFileId, 1)
        };

        var group = new OutputGroup(groupId, "Group 1", DateTimeOffset.UtcNow, items);

        Assert.Equal([3, 4, 1], group.Items.Select(item => item.SourcePageNumber));
    }

    [Fact]
    public void Output_item_gets_independent_identity_from_source_page_reference()
    {
        var sourceFileId = Guid.NewGuid();
        var first = new OutputPageItem(Guid.NewGuid(), Guid.NewGuid(), sourceFileId, 1);
        var second = new OutputPageItem(Guid.NewGuid(), Guid.NewGuid(), sourceFileId, 1);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.SourceFileId, second.SourceFileId);
        Assert.Equal(first.SourcePageNumber, second.SourcePageNumber);
    }

    [Fact]
    public void Output_group_preserves_display_color()
    {
        var group = new OutputGroup(Guid.NewGuid(), "Group 1", DateTimeOffset.UtcNow, [], "#059669");

        Assert.Equal("#059669", group.ColorHex);
    }
}
