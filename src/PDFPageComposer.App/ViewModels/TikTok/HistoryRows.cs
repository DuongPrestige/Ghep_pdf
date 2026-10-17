using System.IO;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.ViewModels.TikTok;

public sealed record ImportBatchRow(ImportBatchRecord Record, string ShopName)
{
    public long ImportId => Record.ImportId;

    public string ImportedAtText => TikTokLabels.FormatDateTime(Record.ImportedAt.ToLocalTime());

    public string FileName => Record.FileName;

    public string ModeText => TikTokLabels.Of(Record.Mode);

    public string Summary =>
        $"{Record.Inserted} đơn mới, {Record.Updated} cập nhật, {Record.AlreadyPrinted} đã in trước đó, " +
        $"{Record.Skipped} dòng bỏ qua, {Record.Deactivated} đơn ẩn";
}

public sealed record PrintBatchRow(PrintBatchRecord Record)
{
    public string BatchId => Record.BatchId;

    public string CreatedAtText => TikTokLabels.FormatDateTime(Record.CreatedAt.ToLocalTime());

    public string FileName => Path.GetFileName(Record.OutputPath);

    public string OutputPath => Record.OutputPath;

    public string SeparatorModeText => TikTokLabels.Of(Record.SeparatorMode);

    public string Summary => $"{Record.OrderCount} đơn, {Record.PageCount} trang";
}

public sealed record PrintBatchItemRow(PrintBatchItemRecord Record)
{
    public int Seq => Record.Seq;

    public string OrderId => Record.OrderId;

    public string SkuId => Record.SkuId;

    public string CopyText => $"{Record.CopyIndex}/{Record.Quantity}";

    public string FileName => Path.GetFileName(Record.PdfAssetPath);

    public string WarningText => Record.Warning is null ? string.Empty : "Trùng hình";
}

public sealed record BatchOrderRow(string OrderId, int CopyCount);
