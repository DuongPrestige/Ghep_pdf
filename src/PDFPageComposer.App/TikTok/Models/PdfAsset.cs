namespace PDFPageComposer.App.TikTok.Models;

/// <summary>
/// A PDF in the print library named "&lt;IDOL&gt; - &lt;N&gt; PAGE - &lt;dd.MM&gt;.pdf" (PRD_TIKTOK TT-01).
/// Idol is normalized; PageCount comes from the file name; ActualPageCount from the PDF itself.
/// Invalid files keep IsValid = false and a Vietnamese Error message; Idol may then be empty.
/// </summary>
public sealed record PdfAsset(
    string FilePath,
    string Idol,
    int PageCount,
    DateOnly FileDate,
    int? ActualPageCount,
    DateTime LastWriteTimeUtc,
    bool IsValid,
    string? Error);
