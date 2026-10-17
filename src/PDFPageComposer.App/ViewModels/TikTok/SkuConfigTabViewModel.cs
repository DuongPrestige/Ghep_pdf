using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>Tab "Cấu hình SKU": confirm suggestions, edit SKUs, ignore whole products, manage idol aliases (TASK-067).</summary>
public sealed partial class SkuConfigTabViewModel : ObservableObject
{
    private readonly TikTokWorkspace workspace;
    private List<SkuRowViewModel> allRows = [];

    public SkuConfigTabViewModel(TikTokWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        this.workspace = workspace;
    }

    public TikTokWorkspace Workspace => workspace;

    public ObservableCollection<SkuRowViewModel> Rows { get; } = [];

    public IReadOnlyList<SkuRowViewModel> AllRows => allRows;

    public ObservableCollection<IdolAlias> Aliases { get; } = [];

    public IReadOnlyList<TikTokOption<SkuProcessType>> ProcessTypes => TikTokLabels.ProcessTypeOptions;

    public string Summary
    {
        get
        {
            var unconfigured = allRows.Count(row => !row.IsConfirmed);
            return $"{allRows.Count} SKU, {unconfigured} chưa cấu hình";
        }
    }

    [ObservableProperty]
    private bool showUnconfiguredOnly = true;

    [ObservableProperty]
    private string? searchText;

    [ObservableProperty]
    private string? newAlias;

    [ObservableProperty]
    private string? newAliasIdol;

    partial void OnShowUnconfiguredOnlyChanged(bool value) => ApplyFilter();

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    /// <summary>Rebuilds the rows from the workspace snapshot (after a reload). Unsaved edits are discarded.</summary>
    public void Refresh()
    {
        allRows = workspace.Skus.Select(sku => new SkuRowViewModel(sku)).ToList();
        Aliases.Clear();
        foreach (var alias in workspace.Aliases)
        {
            Aliases.Add(alias);
        }

        ApplyFilter();
        OnPropertyChanged(nameof(AllRows));
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    private Task ConfirmRowAsync(SkuRowViewModel? row) =>
        row is null ? Task.CompletedTask : ConfirmRowsAsync([row]);

    [RelayCommand]
    private Task ConfirmSelectedAsync()
    {
        var selected = Rows.Where(row => row.IsSelected).ToList();
        if (selected.Count == 0)
        {
            workspace.ShowError("Chưa chọn SKU nào để xác nhận.");
            return Task.CompletedTask;
        }

        return ConfirmRowsAsync(selected);
    }

    [RelayCommand]
    private void SelectAllVisible()
    {
        foreach (var row in Rows)
        {
            row.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in allRows)
        {
            row.IsSelected = false;
        }
    }

    /// <summary>Sets every SKU of the same product (compared after normalization) to "Bỏ qua" and confirms them.</summary>
    [RelayCommand]
    private Task IgnoreProductAsync(SkuRowViewModel? row)
    {
        if (row is null)
        {
            return Task.CompletedTask;
        }

        var product = TextNormalizer.Normalize(row.ProductName);
        var rows = allRows
            .Where(candidate => string.Equals(TextNormalizer.Normalize(candidate.ProductName), product, StringComparison.Ordinal))
            .ToList();
        foreach (var candidate in rows)
        {
            candidate.ProcessType = SkuProcessType.Ignore;
        }

        return ConfirmRowsAsync(rows);
    }

    /// <summary>Re-runs the suggester for unconfirmed rows the user has not edited (for example after adding an alias).</summary>
    [RelayCommand]
    private void RefreshSuggestions()
    {
        var suggester = workspace.Services.Suggester;
        var aliases = workspace.Aliases;
        var updated = 0;
        foreach (var row in allRows.Where(row => !row.IsConfirmed && !row.HasChanges))
        {
            row.ApplySuggestion(suggester.Suggest(row.ProductName, row.VariationName, aliases));
            updated++;
        }

        workspace.StatusText = $"Đã gợi ý lại {updated} SKU chưa cấu hình.";
    }

    [RelayCommand]
    private Task AddAliasAsync()
    {
        var aliasKey = TextNormalizer.IdolKey(NewAlias);
        var idol = TextNormalizer.Normalize(NewAliasIdol);
        if (aliasKey.Length == 0 || idol.Length == 0)
        {
            workspace.ShowError("Nhập cả cách viết (alias) và mã idol.");
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang lưu alias...",
            async cancellationToken =>
            {
                var repository = workspace.Services.Repository;
                await Task.Run(() => repository.SaveIdolAliasAsync(new IdolAlias(aliasKey, idol), cancellationToken), cancellationToken);
                await ReloadAliasesAsync(cancellationToken);
                NewAlias = null;
                NewAliasIdol = null;
                RefreshSuggestions();
                workspace.StatusText = $"Đã thêm alias {aliasKey} → {idol}.";
            },
            "AddIdolAlias");
    }

    [RelayCommand]
    private Task DeleteAliasAsync(IdolAlias? alias)
    {
        if (alias is null)
        {
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang xóa alias...",
            async cancellationToken =>
            {
                var repository = workspace.Services.Repository;
                await Task.Run(() => repository.DeleteIdolAliasAsync(alias.Alias, cancellationToken), cancellationToken);
                await ReloadAliasesAsync(cancellationToken);
                workspace.StatusText = $"Đã xóa alias {alias.Alias}.";
            },
            "DeleteIdolAlias");
    }

    private async Task ReloadAliasesAsync(CancellationToken cancellationToken)
    {
        await workspace.ReloadAliasesAsync(cancellationToken);
        Aliases.Clear();
        foreach (var alias in workspace.Aliases)
        {
            Aliases.Add(alias);
        }
    }

    private Task ConfirmRowsAsync(IReadOnlyList<SkuRowViewModel> rows)
    {
        var errors = rows.Select(row => row.Validate()).OfType<string>().ToList();
        var valid = rows.Where(row => row.Validate() is null).ToList();
        if (valid.Count == 0)
        {
            workspace.ShowError(string.Join(Environment.NewLine, errors));
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang lưu cấu hình SKU...",
            async cancellationToken =>
            {
                var now = workspace.Now;
                var configs = valid.Select(row => row.ToConfirmed(now)).ToList();
                var repository = workspace.Services.Repository;
                await Task.Run(() => repository.SaveSkusAsync(configs, cancellationToken), cancellationToken);

                for (var index = 0; index < valid.Count; index++)
                {
                    valid[index].AcceptSaved(configs[index]);
                }

                workspace.ApplySavedSkus(configs);
                ApplyFilter();
                OnPropertyChanged(nameof(Summary));
                await workspace.RefreshOrdersAsync(cancellationToken);

                workspace.StatusText = $"Đã xác nhận {configs.Count} SKU.";
                if (errors.Count > 0)
                {
                    workspace.ShowError($"{errors.Count} SKU chưa lưu được:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
                }
            },
            "SaveSkus");
    }

    private void ApplyFilter()
    {
        var search = TextNormalizer.Normalize(SearchText);
        Rows.Clear();
        foreach (var row in allRows)
        {
            if (ShowUnconfiguredOnly && row.IsConfirmed && !row.HasChanges)
            {
                continue;
            }

            if (search.Length > 0 && !Matches(row, search))
            {
                continue;
            }

            Rows.Add(row);
        }
    }

    private static bool Matches(SkuRowViewModel row, string search) =>
        row.SkuId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        TextNormalizer.Normalize(row.ProductName).Contains(search, StringComparison.Ordinal) ||
        TextNormalizer.Normalize(row.VariationName).Contains(search, StringComparison.Ordinal);
}
