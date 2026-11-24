using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NeuroDocs.Controls;

/// <summary>
/// Radio de esquina = mitad de la altura: forma de píldora real.
/// (Un radio mayor, como 999, hace que WPF deforme el borde en un óvalo.)
/// </summary>
public sealed class RadioPildoraConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double alto && alto > 0 ? new CornerRadius(alto / 2) : new CornerRadius(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
