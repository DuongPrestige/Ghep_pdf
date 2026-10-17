namespace PDFPageComposer.App.Models;

/// <summary>Drag and drop of an output page onto another one: placed before the target, or after it when PlaceAfter.</summary>
public sealed record OutputItemMoveRequest(OutputPageItem SourceItem, OutputPageItem TargetItem, bool PlaceAfter = false);
