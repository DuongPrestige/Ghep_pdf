using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.Tests.Services;

public sealed class DuplexLayoutTests
{
    [Fact]
    public void CreateBackItems_repeats_a_single_page_to_match_side_1()
    {
        var fileId = Guid.NewGuid();
        var pairId = Guid.NewGuid();

        var items = DuplexLayout.CreateBackItems(pairId, [new SourcePdfPage(Guid.NewGuid(), fileId, 1)], frontPageCount: 3);

        Assert.Equal(3, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal(pairId, item.GroupId);
            Assert.Equal(fileId, item.SourceFileId);
            Assert.Equal(1, item.SourcePageNumber);
        });
        Assert.Equal(3, items.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void CreateBackItems_keeps_several_pages_as_chosen()
    {
        var fileId = Guid.NewGuid();
        var pages = new[] { new SourcePdfPage(Guid.NewGuid(), fileId, 2), new SourcePdfPage(Guid.NewGuid(), fileId, 1) };

        var items = DuplexLayout.CreateBackItems(Guid.NewGuid(), pages, frontPageCount: 3);

        Assert.Equal([2, 1], items.Select(item => item.SourcePageNumber));
    }

    [Fact]
    public void DescribeProblem_lists_pairs_missing_side_2_or_with_different_page_counts()
    {
        var complete = CreatePair(front: 3, back: 3);
        var missingBack = CreatePair(front: 4, back: 0);
        var mismatched = CreatePair(front: 6, back: 5);

        Assert.Null(DuplexLayout.DescribeProblem([complete]));
        Assert.Equal(
            "Chưa xuất được: thiếu mặt 2: cặp 2; lệch số trang: cặp 3",
            DuplexLayout.DescribeProblem([complete, missingBack, mismatched]));
        Assert.Equal("Khay 2 mặt đang trống", DuplexLayout.DescribeProblem([]));
        Assert.Equal("Lệch: mặt 1 có 6 trang, mặt 2 có 5 trang", DuplexLayout.DescribePair(mismatched));
        Assert.Equal("Chờ mặt 2", DuplexLayout.DescribePair(missingBack));
        Assert.Equal("Khớp", DuplexLayout.DescribePair(complete));
    }

    [Fact]
    public void ToSideGroups_keeps_pair_order_for_each_side()
    {
        var first = CreatePair(front: 2, back: 2, frontPageStart: 1, backPageStart: 10);
        var second = CreatePair(front: 1, back: 1, frontPageStart: 5, backPageStart: 20);

        var fronts = DuplexLayout.ToSideGroups([first, second], DuplexSide.Front);
        var backs = DuplexLayout.ToSideGroups([first, second], DuplexSide.Back);

        Assert.Equal([1, 2, 5], fronts.SelectMany(group => group.Items).Select(item => item.SourcePageNumber));
        Assert.Equal([10, 11, 20], backs.SelectMany(group => group.Items).Select(item => item.SourcePageNumber));
        Assert.Equal(["Cặp 1 · mặt 1", "Cặp 2 · mặt 1"], fronts.Select(group => group.Name));
        Assert.Equal(
            [1, 2, 5, 10, 11, 20],
            DuplexLayout.ToExportGroups([first, second]).SelectMany(group => group.Items).Select(item => item.SourcePageNumber));
    }

    private static DuplexPair CreatePair(int front, int back, int frontPageStart = 1, int backPageStart = 1)
    {
        var pairId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        return new DuplexPair(
            pairId,
            "#2563EB",
            Enumerable.Range(frontPageStart, front).Select(page => new OutputPageItem(Guid.NewGuid(), pairId, fileId, page)),
            Enumerable.Range(backPageStart, back).Select(page => new OutputPageItem(Guid.NewGuid(), pairId, fileId, page)));
    }
}
