using System.Collections.ObjectModel;

namespace PDFPageComposer.App.Models;

public sealed class OutputGroup
{
    public OutputGroup(Guid id, string name, DateTimeOffset createdAt, IEnumerable<OutputPageItem> items, string colorHex = "#2563EB")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorHex);

        Id = id;
        Name = name;
        CreatedAt = createdAt;
        ColorHex = colorHex;
        Items = new ObservableCollection<OutputPageItem>(items);
    }

    public Guid Id { get; }

    public string Name { get; }

    public DateTimeOffset CreatedAt { get; }

    public string ColorHex { get; }

    public bool IsCollapsed { get; set; }

    public ObservableCollection<OutputPageItem> Items { get; }
}
