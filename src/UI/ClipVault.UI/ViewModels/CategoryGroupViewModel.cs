using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ClipVault.UI.ViewModels;

public partial class CategoryGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    public ObservableCollection<ClipboardItemViewModel> Items { get; } = [];
}
