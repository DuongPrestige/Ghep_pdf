using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PDFPageComposer.App.Models;

public sealed partial class SourcePdfFile : ObservableObject
{
    public SourcePdfFile(
        Guid id,
        string filePath,
        string displayName,
        int pageCount,
        long fileSize,
        string fingerprint,
        bool isEncrypted = false,
        IEnumerable<SourcePdfPage>? pages = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        ArgumentOutOfRangeException.ThrowIfNegative(fileSize);

        Id = id;
        filePathValue = filePath;
        displayNameValue = displayName;
        pageCountValue = pageCount;
        fileSizeValue = fileSize;
        fingerprintValue = fingerprint;
        IsEncrypted = isEncrypted;

        Pages = new ObservableCollection<SourcePdfPage>(
            pages ?? Enumerable.Range(1, pageCount).Select(pageNumber => new SourcePdfPage(Guid.NewGuid(), id, pageNumber)));
    }

    public Guid Id { get; }

    private string filePathValue;

    private string displayNameValue;

    private int pageCountValue;

    private long fileSizeValue;

    private string fingerprintValue;

    public string FilePath => filePathValue;

    public string DisplayName => displayNameValue;

    public int PageCount => pageCountValue;

    public long FileSize => fileSizeValue;

    public string Fingerprint => fingerprintValue;

    public bool IsEncrypted { get; }

    [ObservableProperty]
    private bool isMissing;

    [ObservableProperty]
    private bool isCollapsed;

    public ObservableCollection<SourcePdfPage> Pages { get; }

    public void Relink(string filePath, string displayName, long fileSize, string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfNegative(fileSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        filePathValue = filePath;
        displayNameValue = displayName;
        fileSizeValue = fileSize;
        fingerprintValue = fingerprint;
        IsMissing = false;
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FileSize));
        OnPropertyChanged(nameof(Fingerprint));
    }
}
