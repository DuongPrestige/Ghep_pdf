namespace PDFPageComposer.App.Interfaces;

public interface IClipboardService
{
    /// <exception cref="InvalidOperationException">
    /// The clipboard is held by another application; the message is user-facing Vietnamese text.
    /// </exception>
    void SetText(string text);
}
