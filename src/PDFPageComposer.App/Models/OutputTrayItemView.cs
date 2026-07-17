namespace PDFPageComposer.App.Models;

public sealed record OutputTrayItemView(
    int OutputIndex,
    string GroupName,
    string SourceDisplayName,
    int SourcePageNumber,
    SourcePdfPage? SourcePage,
    OutputGroup Group,
    OutputPageItem Item)
{
    public bool IsPreviewSelected { get; init; }
}
