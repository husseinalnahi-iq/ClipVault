using ClipVault.UI.ViewModels;
using System.Globalization;
using System.Windows.Data;

namespace ClipVault.UI.Converters;

public sealed class TabMatchToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not NavigationTab tab || parameter is null)
        {
            return false;
        }

        return tab.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null && Enum.TryParse(parameter.ToString(), true, out NavigationTab tab))
        {
            return tab;
        }

        return System.Windows.Data.Binding.DoNothing;
    }
}
