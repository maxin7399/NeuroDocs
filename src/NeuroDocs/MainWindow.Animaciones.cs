using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using NeuroDocs.Controls;

namespace NeuroDocs;

/// <summary>Animaciones y estado visual de la ventana principal (entrada, pasos, IA trabajando, avisos).</summary>
public partial class MainWindow
{
    /// <summary>Windows permite desactivar las animaciones (accesibilidad); en ese caso todo aparece sin movimiento.</summary>
    private static bool Animar => SystemParameters.ClientAreaAnimation;

    private static readonly IEasingFunction Suave = new CubicEase { EasingMode = EasingMode.EaseOut };

    // ───────────── Entrada: el logo se enciende en el centro y viaja a la barra ─────────────

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!Animar)
        {
            IntroOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        BarraLogo.Opacity = 0;
        var elementos = ElementosDeEntrada().ToList();
        foreach (var el in elementos) el.Opacity = 0;

        // 1) Se enciende (≈0,45 s): aparece, crece con un pequeño rebote y gana brillo.
        Animacion(IntroLogo, OpacityProperty, 0, 1, 0, 300);
        var rebote = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
        var encender = Crear(0.6, 1, 0, 450, rebote);
        encender.Completed += (_, _) => ViajarALaBarra(elementos);
        IntroEscala.BeginAnimation(ScaleTransform.ScaleYProperty, Crear(0.6, 1, 0, 450, rebote));
        IntroEscala.BeginAnimation(ScaleTransform.ScaleXProperty, encender);
        IntroBrillo.BeginAnimation(DropShadowEffect.BlurRadiusProperty, Crear(0, 46, 0, 450, Suave));
    }

    /// <summary>2) Viaja al logo de la barra mientras se desvanece el fondo; 3) el contenido entra escalonado.</summary>
    private void ViajarALaBarra(List<FrameworkElement> elementos)
    {
        var centroIntro = IntroLogo.TranslatePoint(new Point(IntroLogo.ActualWidth / 2, IntroLogo.ActualHeight / 2), IntroOverlay);
        var centroBarra = BarraLogo.TranslatePoint(new Point(BarraLogo.ActualWidth / 2, BarraLogo.ActualHeight / 2), IntroOverlay);
        double escalaFinal = BarraLogo.ActualWidth / IntroLogo.ActualWidth;
        var viaje = new CubicEase { EasingMode = EasingMode.EaseInOut };

        IntroMovimiento.BeginAnimation(TranslateTransform.XProperty, Crear(0, centroBarra.X - centroIntro.X, 60, 420, viaje));
        IntroMovimiento.BeginAnimation(TranslateTransform.YProperty, Crear(0, centroBarra.Y - centroIntro.Y, 60, 420, viaje));
        IntroEscala.BeginAnimation(ScaleTransform.ScaleXProperty, Crear(1, escalaFinal, 60, 420, viaje));
        IntroEscala.BeginAnimation(ScaleTransform.ScaleYProperty, Crear(1, escalaFinal, 60, 420, viaje));
        IntroBrillo.BeginAnimation(DropShadowEffect.BlurRadiusProperty, Crear(46, 12, 60, 420, viaje));
        Animacion(IntroFondo, OpacityProperty, 1, 0, 40, 360);

        // Al llegar, el logo real toma su lugar.
        var fin = Crear(1, 0, 480, 120, null);
        fin.Completed += (_, _) => IntroOverlay.Visibility = Visibility.Collapsed;
        Animacion(BarraLogo, OpacityProperty, 0, 1, 440, 120);
        IntroLogo.BeginAnimation(OpacityProperty, fin);

        int i = 0;
        foreach (var el in elementos) Entrar(el, 170 + i++ * 70);
    }

    /// <summary>Lo que entra escalonado: los pasos, la tarjeta de documentos y la del modo activo.</summary>
    private IEnumerable<FrameworkElement> ElementosDeEntrada()
    {
        yield return PasosPanel;
        yield return TarjetaDocumentos;
        yield return TarjetaPlan.Visibility == Visibility.Visible ? TarjetaPlan : TarjetaInforme;
    }

    /// <summary>Sube unos píxeles mientras aparece.</summary>
    private static void Entrar(FrameworkElement el, int retardoMs)
    {
        var mover = new TranslateTransform();
        el.RenderTransform = mover;
        mover.BeginAnimation(TranslateTransform.YProperty, Crear(14, 0, retardoMs, 420, Suave));
        Animacion(el, OpacityProperty, 0, 1, retardoMs, 380);
    }

    /// <summary>Aparición breve para contenido que cambia (resumen del paciente, resultados de la IA).</summary>
    private static void Aparecer(FrameworkElement el)
    {
        if (!Animar) return;
        Entrar(el, 0);
    }

    // ───────────── Pasos: pendiente, activo o hecho ─────────────

    private enum EstadoPaso { Pendiente, Activo, Hecho }

    private void ActualizarPasos()
    {
        if (Paso3 is null || ConservadosTextBox is null || CondicionTextBox is null) return; // aún construyéndose

        bool informe = InformeModoRadio.IsChecked == true;
        bool documentos = _lectura is not null;
        bool contenido = informe
            ? !string.IsNullOrWhiteSpace(CondicionTextBox.Text) && !string.IsNullOrWhiteSpace(PlanTextBox.Text)
            : !string.IsNullOrWhiteSpace(ConservadosTextBox.Text) || !string.IsNullOrWhiteSpace(AlteradosTextBox.Text);

        Paso2Texto.Text = informe ? "Evaluación" : "Funciones";
        PintarPaso(Paso1, Paso1Circulo, Paso1Numero, Paso1Texto, documentos ? EstadoPaso.Hecho : EstadoPaso.Activo);
        PintarPaso(Paso2, Paso2Circulo, Paso2Numero, Paso2Texto,
                   !documentos ? EstadoPaso.Pendiente : contenido ? EstadoPaso.Hecho : EstadoPaso.Activo);
        PintarPaso(Paso3, Paso3Circulo, Paso3Numero, Paso3Texto,
                   documentos && contenido ? EstadoPaso.Activo : EstadoPaso.Pendiente);
    }

    private void PintarPaso(Border paso, Border circulo, TextBlock numero, TextBlock texto, EstadoPaso estado)
    {
        var acento = Recurso("AcentoBrush");
        paso.Background = Recurso(estado == EstadoPaso.Pendiente ? "PasoFondoBrush" : "PasoActivoFondoBrush");
        paso.BorderBrush = estado == EstadoPaso.Pendiente ? Recurso("BordeTarjetaBrush") : acento;
        paso.Effect = estado == EstadoPaso.Pendiente ? null
            : new DropShadowEffect { Color = (Color)FindResource("AcentoColor"), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.35 };

        circulo.BorderBrush = estado == EstadoPaso.Pendiente ? Recurso("TextoSecundarioBrush") : acento;
        circulo.Background = estado == EstadoPaso.Hecho ? acento : Brushes.Transparent;
        numero.Text = estado == EstadoPaso.Hecho ? "✓" : numero.Text is "✓" ? NumeroOriginal(numero) : numero.Text;
        numero.Foreground = estado switch
        {
            EstadoPaso.Hecho => Brushes.White,
            EstadoPaso.Activo => acento,
            _ => Recurso("TextoSecundarioBrush")
        };
        texto.Foreground = Recurso(estado == EstadoPaso.Pendiente ? "TextoSecundarioBrush" : "TextoPrincipalBrush");
        texto.FontWeight = estado == EstadoPaso.Activo ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private string NumeroOriginal(TextBlock numero) =>
        numero == Paso1Numero ? "1" : numero == Paso2Numero ? "2" : "3";

    private Brush Recurso(string clave) => (Brush)FindResource(clave);

    // ───────────── IA trabajando: puntos que laten ─────────────

    private static void MostrarPensando(Panel panel, bool visible)
    {
        panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        int i = 0;
        foreach (var punto in panel.Children.OfType<Ellipse>())
        {
            if (!visible || !Animar)
            {
                punto.BeginAnimation(OpacityProperty, null);
                punto.RenderTransform = Transform.Identity;
                continue;
            }

            var escala = new ScaleTransform(0.6, 0.6);
            punto.RenderTransform = escala;
            var latido = new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(500))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(i * 150), EasingFunction = new SineEase()
            };
            escala.BeginAnimation(ScaleTransform.ScaleXProperty, latido);
            escala.BeginAnimation(ScaleTransform.ScaleYProperty, latido);
            punto.BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(500))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(i * 150)
            });
            i++;
        }
    }

    // ───────────── Aviso: entra deslizándose desde abajo ─────────────

    private void AnimarAviso()
    {
        if (!Animar) return;
        var mover = (TranslateTransform)AvisoBorder.RenderTransform;
        var rebote = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 };
        mover.BeginAnimation(TranslateTransform.YProperty, Crear(24, 0, 0, 450, rebote));
        Animacion(AvisoBorder, OpacityProperty, 0, 1, 0, 300);
    }

    // ───────────── Utilidades ─────────────

    private static DoubleAnimation Crear(double desde, double hasta, int retardoMs, int duracionMs, IEasingFunction? curva) =>
        new(desde, hasta, TimeSpan.FromMilliseconds(duracionMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(retardoMs),
            EasingFunction = curva,
            FillBehavior = FillBehavior.HoldEnd
        };

    private static void Animacion(UIElement el, DependencyProperty propiedad, double desde, double hasta, int retardoMs, int duracionMs) =>
        el.BeginAnimation(propiedad, Crear(desde, hasta, retardoMs, duracionMs, Suave));
}
