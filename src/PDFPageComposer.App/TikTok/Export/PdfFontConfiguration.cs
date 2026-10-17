using PdfSharp.Fonts;
using Serilog;

namespace PDFPageComposer.App.TikTok.Export;

/// <summary>
/// Registers <see cref="WindowsFontResolver"/> with PDFsharp once per process so separator pages can draw
/// Vietnamese text. PDFsharp locks its font settings after the first read, so this class only ever writes them.
/// Safe to call from any thread and any number of times.
/// </summary>
internal static class PdfFontConfiguration
{
    private static readonly object SyncRoot = new();
    private static bool configured;

    public static void EnsureConfigured()
    {
        if (Volatile.Read(ref configured))
        {
            return;
        }

        lock (SyncRoot)
        {
            if (configured)
            {
                return;
            }

            try
            {
                GlobalFontSettings.FontResolver = WindowsFontResolver.Instance;
            }
            catch (InvalidOperationException ex)
            {
                // Another component configured PDFsharp fonts first; rendering will use that configuration.
                Log.Warning(ex, "Could not register the PDFsharp font resolver; using the existing font configuration.");
            }

            Volatile.Write(ref configured, true);
        }
    }
}
