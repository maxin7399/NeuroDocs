using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NeuroDocs.Controls;

/// <summary>
/// Panel con borde suave y brillo morado; una luz tenue sigue al mouse por encima del contenido.
/// El estilo (plantilla) está en App.xaml.
/// </summary>
public class Tarjeta : ContentControl
{
    private Border? _luz;
    private RadialGradientBrush? _brochaLuz;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _luz = GetTemplateChild("PART_Luz") as Border;
        _brochaLuz = _luz?.Background as RadialGradientBrush;
        if (_brochaLuz is { IsFrozen: true }) _luz!.Background = _brochaLuz = _brochaLuz.Clone();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_luz is null || _brochaLuz is null) return;

        var p = e.GetPosition(_luz);
        _brochaLuz.Center = p;
        _brochaLuz.GradientOrigin = p;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _luz?.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(250)));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _luz?.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(400)));
    }
}
