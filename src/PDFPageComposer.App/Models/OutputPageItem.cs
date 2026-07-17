namespace PDFPageComposer.App.Models;

public sealed class OutputPageItem
{
    public OutputPageItem(Guid id, Guid groupId, Guid sourceFileId, int sourcePageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourcePageNumber);

        Id = id;
        GroupId = groupId;
        SourceFileId = sourceFileId;
        SourcePageNumber = sourcePageNumber;
    }

    public Guid Id { get; }

    public Guid GroupId { get; }

    public Guid SourceFileId { get; }

    public int SourcePageNumber { get; }
}
