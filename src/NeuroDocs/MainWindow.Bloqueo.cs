using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NeuroDocs.Services;

namespace NeuroDocs;

/// <summary>
/// Contraseña de acceso: se crea la primera vez que se abre la app y se pide al abrirla, al pulsar
/// "Bloquear" (o Ctrl+L) y tras unos minutos sin uso. Bloquear no borra nada de lo cargado.
/// </summary>
public partial class MainWindow
{
    private const int IntentosAntesDeEsperar = 5;
    private static readonly TimeSpan EsperaTrasFallos = TimeSpan.FromSeconds(30);

    private bool _bloqueada;
    private bool _creandoContrasena;
    private int _intentosFallidos;
    private DateTime _esperaHasta;
    private DateTime _ultimaActividad = DateTime.Now;
    private int _minutosBloqueo = AjustesUsuario.MinutosBloqueoPorDefecto;
    private readonly DispatcherTimer _relojBloqueo = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>Elementos cuya animación de entrada espera a que se desbloquee la app al abrirla.</summary>
    private List<FrameworkElement>? _entradaPendiente;

    private void InicializarBloqueo()
    {
        var ajustes = AjustesUsuario.Cargar();
        _minutosBloqueo = ajustes.MinutosBloqueoEfectivos;
        MostrarBloqueo(crear: !ajustes.TieneContrasena, animar: false);

        // Cualquier uso del teclado o el ratón cuenta como actividad.
        PreviewMouseMove += (_, _) => _ultimaActividad = DateTime.Now;
        PreviewMouseDown += (_, _) => _ultimaActividad = DateTime.Now;
        PreviewKeyDown += (_, _) => _ultimaActividad = DateTime.Now;

        _relojBloqueo.Tick += RelojBloqueo_Tick;
        _relojBloqueo.Start();
    }

    private void RelojBloqueo_Tick(object? sender, EventArgs e)
    {
        if (_bloqueada)
        {
            ActualizarEspera();
            return;
        }
        if (_minutosBloqueo > 0 && DateTime.Now - _ultimaActividad >= TimeSpan.FromMinutes(_minutosBloqueo))
            Bloquear();
    }

    /// <summary>Tras cambiar la Configuración, el nuevo tiempo de bloqueo se aplica de inmediato.</summary>
    private void RecargarAjustesBloqueo() => _minutosBloqueo = AjustesUsuario.Cargar().MinutosBloqueoEfectivos;

    private void BloquearButton_Click(object sender, RoutedEventArgs e) => Bloquear();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_bloqueada && e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Bloquear();
            e.Handled = true;
        }
    }

    private void Bloquear()
    {
        if (_bloqueada) return;
        _ventanaResultado?.Close(); // muestra datos del paciente
        MostrarBloqueo(crear: false, animar: true);
    }

    private void MostrarBloqueo(bool crear, bool animar)
    {
        _bloqueada = true;
        _creandoContrasena = crear;
        RaizGrid.IsEnabled = false; // nada detrás del bloqueo recibe el teclado

        BloqueoTituloText.Text = crear ? "Crea una contraseña" : "NeuroDocs está bloqueado";
        BloqueoSubtituloText.Text = crear
            ? $"La pedirá cada vez que se abra o se bloquee la app. Mínimo {ContrasenaAcceso.LongitudMinima} caracteres. No se puede recuperar desde la app."
            : "Escribe la contraseña para continuar.";
        ContrasenaEtiqueta.Text = crear ? "Nueva contraseña" : "Contraseña";
        ConfirmarPanel.Visibility = crear ? Visibility.Visible : Visibility.Collapsed;
        EntrarTexto.Text = crear ? "Crear y entrar" : "Entrar";
        ContrasenaBox.Clear();
        ConfirmarBox.Clear();
        MostrarErrorBloqueo(null);
        ActualizarEspera();

        BloqueoOverlay.BeginAnimation(OpacityProperty, null);
        BloqueoOverlay.Opacity = 1;
        BloqueoOverlay.Visibility = Visibility.Visible;
        if (animar && Animar) Animacion(BloqueoOverlay, OpacityProperty, 0, 1, 0, 250);

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => ContrasenaBox.Focus());
    }

    private void ContrasenaBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (_creandoContrasena && sender == ContrasenaBox)
            ConfirmarBox.Focus(); // primero se pasa a "repetir"
        else
            EntrarButton_Click(sender, e);
        e.Handled = true;
    }

    private async void EntrarButton_Click(object sender, RoutedEventArgs e)
    {
        string contrasena = ContrasenaBox.Password;

        if (_creandoContrasena)
        {
            if (contrasena.Length < ContrasenaAcceso.LongitudMinima)
            {
                MostrarErrorBloqueo($"La contraseña debe tener al menos {ContrasenaAcceso.LongitudMinima} caracteres.");
                return;
            }
            if (contrasena != ConfirmarBox.Password)
            {
                MostrarErrorBloqueo("Las contraseñas no coinciden.");
                ConfirmarBox.Clear();
                ConfirmarBox.Focus();
                return;
            }

            try
            {
                var ajustes = AjustesUsuario.Cargar();
                ajustes.EstablecerContrasena(contrasena);
                ajustes.Guardar();
            }
            catch (IOException ex)
            {
                MostrarErrorBloqueo($"No se pudo guardar la contraseña: {ex.Message}");
                return;
            }
            Desbloquear();
            return;
        }

        if (DateTime.Now < _esperaHasta || contrasena.Length == 0) return;

        EntrarButton.IsEnabled = false;
        var ajustesGuardados = AjustesUsuario.Cargar();
        // PBKDF2 tarda unas décimas de segundo: fuera del hilo de la interfaz.
        bool correcta = await Task.Run(() => ajustesGuardados.VerificarContrasena(contrasena));
        EntrarButton.IsEnabled = true;

        if (correcta)
        {
            _intentosFallidos = 0;
            Desbloquear();
            return;
        }

        _intentosFallidos++;
        ContrasenaBox.Clear();
        Sacudir();
        if (_intentosFallidos >= IntentosAntesDeEsperar)
        {
            _intentosFallidos = 0;
            _esperaHasta = DateTime.Now + EsperaTrasFallos;
            ActualizarEspera();
        }
        else
        {
            MostrarErrorBloqueo("Contraseña incorrecta.");
            ContrasenaBox.Focus();
        }
    }

    /// <summary>Tras varios intentos fallidos se espera unos segundos, con cuenta atrás.</summary>
    private void ActualizarEspera()
    {
        bool esperando = DateTime.Now < _esperaHasta;
        ContrasenaBox.IsEnabled = !esperando;
        EntrarButton.IsEnabled = !esperando;
        if (esperando)
        {
            int segundos = (int)Math.Ceiling((_esperaHasta - DateTime.Now).TotalSeconds);
            MostrarErrorBloqueo($"Demasiados intentos. Espera {segundos} s para volver a intentarlo.");
        }
        else if (BloqueoErrorText.Text.StartsWith("Demasiados"))
        {
            MostrarErrorBloqueo(null);
            ContrasenaBox.Focus();
        }
    }

    private void MostrarErrorBloqueo(string? mensaje)
    {
        BloqueoErrorText.Text = mensaje ?? "";
        BloqueoErrorText.Visibility = mensaje is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Desbloquear()
    {
        _bloqueada = false;
        _creandoContrasena = false;
        _ultimaActividad = DateTime.Now;
        ContrasenaBox.Clear();
        ConfirmarBox.Clear();
        RaizGrid.IsEnabled = true;

        if (Animar)
        {
            var salir = Crear(1, 0, 0, 220, Suave);
            salir.Completed += (_, _) => { if (!_bloqueada) BloqueoOverlay.Visibility = Visibility.Collapsed; };
            BloqueoOverlay.BeginAnimation(OpacityProperty, salir);
        }
        else
        {
            BloqueoOverlay.Visibility = Visibility.Collapsed;
        }

        // Al abrir la app, el contenido entra recién cuando se desbloquea.
        if (_entradaPendiente is { } elementos)
        {
            _entradaPendiente = null;
            int i = 0;
            foreach (var el in elementos) Entrar(el, 120 + i++ * 70);
        }
    }

    private void Sacudir()
    {
        if (!Animar) return;
        var sacudida = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(360) };
        double[] pasos = [0, -10, 9, -7, 5, -3, 0];
        for (int i = 0; i < pasos.Length; i++)
            sacudida.KeyFrames.Add(new LinearDoubleKeyFrame(pasos[i], KeyTime.FromPercent(i / (double)(pasos.Length - 1))));
        BloqueoSacudida.BeginAnimation(TranslateTransform.XProperty, sacudida);
    }
}
