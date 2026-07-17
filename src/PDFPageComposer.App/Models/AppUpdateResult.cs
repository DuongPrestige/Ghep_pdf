namespace PDFPageComposer.App.Models;

public sealed record AppUpdateResult(
    AppUpdateStatus Status,
    string Message,
    AppUpdateManifest? Manifest = null);

public enum AppUpdateStatus
{
    NotConfigured,
    UpToDate,
    UpdateStarted,
    Failed
}
