using System.Runtime.InteropServices;
using System.Windows;
using PDFPageComposer.App.Interfaces;
using Serilog;

namespace PDFPageComposer.App.Services;

public sealed class ClipboardService : IClipboardService
{
    /// <exception cref="InvalidOperationException">Another application holds the clipboard.</exception>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            // copy: true keeps the text after the app exits; WPF already retries a busy clipboard internally.
            Clipboard.SetDataObject(text, copy: true);
        }
        catch (ExternalException exception)
        {
            // Covers COMException CLIPBRD_E_CANT_OPEN. The clipboard content is never logged.
            Log.Warning("Setting clipboard text failed with HResult {HResult}", exception.HResult);
            throw new InvalidOperationException(
                "Không copy được vào clipboard (đang bị ứng dụng khác giữ). Vui lòng thử lại.",
                exception);
        }
    }
}
