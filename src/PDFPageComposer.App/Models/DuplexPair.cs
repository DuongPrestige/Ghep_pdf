using System.Collections.ObjectModel;

namespace PDFPageComposer.App.Models;

/// <summary>
/// One order in the "Ghép 2 mặt" tray: the pages printed on side 1 and the pages printed on the back (side 2).
/// Page k of side 2 is printed on the back of page k of side 1, so both sides must have the same page count.
/// Output items use the pair id as their group id.
/// </summary>
public sealed class DuplexPair
{
    public DuplexPair(
        Guid id,
        string colorHex,
        IEnumerable<OutputPageItem> frontItems,
        IEnumerable<OutputPageItem>? backItems = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(colorHex);

        Id = id;
        ColorHex = colorHex;
        FrontItems = new ObservableCollection<OutputPageItem>(frontItems);
        BackItems = new ObservableCollection<OutputPageItem>(backItems ?? []);
    }

    public Guid Id { get; }

    public string ColorHex { get; }

    public ObservableCollection<OutputPageItem> FrontItems { get; }

    public ObservableCollection<OutputPageItem> BackItems { get; }

    public bool HasBack => BackItems.Count > 0;

    /// <summary>Both sides have pages and the same page count, so the pair can be exported.</summary>
    public bool IsComplete => FrontItems.Count > 0 && FrontItems.Count == BackItems.Count;

    public IEnumerable<OutputPageItem> AllItems => FrontItems.Concat(BackItems);
}

public enum DuplexSide
{
    Front = 1,
    Back = 2
}

/// <summary>What the "Thay" / "Thêm" button of a side does with the selected pages.</summary>
public enum DuplexEditMode
{
    Replace,
    Append
}
