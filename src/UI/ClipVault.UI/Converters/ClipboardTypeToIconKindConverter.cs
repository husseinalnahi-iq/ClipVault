using ClipVault.Core.Domain.Enums;
using MahApps.Metro.IconPacks;
using System.Globalization;
using System.Windows.Data;

namespace ClipVault.UI.Converters;

public sealed class ClipboardTypeToIconKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipboardItemType type)
        {
            return type == ClipboardItemType.Image ? PackIconLucideKind.Image : PackIconLucideKind.Type;
        }

        return PackIconLucideKind.Type;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}
