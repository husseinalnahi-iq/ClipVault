using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipVault.UI.ViewModels;

public partial class CategoryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _color;

    [ObservableProperty]
    private int _sortOrder;

    public override string ToString() => Name;
}
