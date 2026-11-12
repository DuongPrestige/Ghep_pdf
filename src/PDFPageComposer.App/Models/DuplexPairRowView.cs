namespace PDFPageComposer.App.Models;

/// <summary>One row of the "Ghép 2 mặt" tray: pair number, both sides and whether the pair can be exported.</summary>
public sealed record DuplexPairRowView(
    int Number,
    string Title,
    DuplexPair Pair,
    IReadOnlyList<DuplexPageView> FrontPages,
    IReadOnlyList<DuplexPageView> BackPages,
    string StatusText,
    bool IsComplete,
    DuplexSide? EditingSide = null,
    DuplexEditMode EditMode = DuplexEditMode.Replace)
{
    public bool IsReplacingFront => IsEditing(DuplexSide.Front, DuplexEditMode.Replace);

    public bool IsReplacingBack => IsEditing(DuplexSide.Back, DuplexEditMode.Replace);

    public bool IsAppendingFront => IsEditing(DuplexSide.Front, DuplexEditMode.Append);

    public bool IsAppendingBack => IsEditing(DuplexSide.Back, DuplexEditMode.Append);

    public string FrontReplaceText => IsReplacingFront ? "Hủy thay" : "Thay";

    public string BackReplaceText => IsReplacingBack ? "Hủy thay" : "Thay";

    public string FrontAppendText => IsAppendingFront ? "Hủy thêm" : "Thêm";

    public string BackAppendText => IsAppendingBack ? "Hủy thêm" : "Thêm";

    public string Header => $"Cặp {Number} · {Title}";

    public string FrontHeader => $"Mặt 1 · {FrontPages.Count} trang";

    public string BackHeader => $"Mặt 2 · {BackPages.Count} trang";

    public bool HasBack => BackPages.Count > 0;

    public bool HasProblem => !IsComplete;

    private bool IsEditing(DuplexSide side, DuplexEditMode mode) => EditingSide == side && EditMode == mode;
}

/// <summary>A page shown in a duplex row; Position is its 1-based place in the side file.</summary>
public sealed record DuplexPageView(
    int Position,
    string SourceDisplayName,
    int SourcePageNumber,
    SourcePdfPage? SourcePage)
{
    public string Caption => $"{SourceDisplayName} · tr {SourcePageNumber}";
}
