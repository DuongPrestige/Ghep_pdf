using System.Collections.ObjectModel;

namespace PDFPageComposer.App.Models;

public sealed class OutputGroup
{
    public OutputGroup(Guid id, string name, DateTimeOffset createdAt, IEnumerable<OutputPageItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        CreatedAt = createdAt;
        Items = new ObservableCollection<OutputPageItem>(items);
    }

    public Guid Id { get; }

    public string Name { get; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsCollapsed { get; set; }

    public ObservableCollection<OutputPageItem> Items { get; }
}
