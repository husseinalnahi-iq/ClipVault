using ClipVault.UI.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClipVault.UI.Converters;

public sealed class TabToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not NavigationTab tab || parameter is null)
        {
            return Visibility.Collapsed;
        }

        return tab.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}
