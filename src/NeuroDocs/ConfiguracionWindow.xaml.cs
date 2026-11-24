using System.IO;
using System.Windows;
using NeuroDocs.Services;

namespace NeuroDocs;

public partial class ConfiguracionWindow : Window
{
    private readonly AjustesUsuario _ajustes = AjustesUsuario.Cargar();

    public ConfiguracionWindow()
    {
        InitializeComponent();
        VersionText.Text = $"NeuroDocs {InfoAplicacion.VersionCompleta}";

        ModeloCombo.ItemsSource = ConfiguracionGemini.ModelosSugeridos;
        ModeloCombo.Text = _ajustes.Modelo ?? ConfiguracionGemini.ModeloPorDefecto;

        // La clave nunca se muestra: solo se indica si ya hay una guardada.
        EstadoClaveText.Text = _ajustes.TieneApiKey
            ? "Hay una clave guardada. Deja el campo vacío para conservarla."
            : "No hay ninguna clave guardada.";

        var opciones = new List<(int Minutos, string Texto)> { (0, "Nunca"), (5, "5 minutos"), (10, "10 minutos"),
            (15, "15 minutos"), (30, "30 minutos"), (60, "1 hora") };
        int actuales = _ajustes.MinutosBloqueoEfectivos;
        if (opciones.All(o => o.Minutos != actuales)) opciones.Add((actuales, $"{actuales} minutos")); // valor editado a mano
        MinutosBloqueoCombo.ItemsSource = opciones.OrderBy(o => o.Minutos == 0 ? int.MaxValue : o.Minutos)
                                                  .Select(o => new { o.Minutos, o.Texto }).ToList();
        MinutosBloqueoCombo.SelectedValue = actuales;
    }

    /// <summary>Valida el cambio de contraseña; null si no se pidió cambiarla o si es válido.</summary>
    private string? ErrorCambioContrasena()
    {
        string nueva = ContrasenaNuevaBox.Password;
        if (nueva.Length == 0 && ContrasenaConfirmarBox.Password.Length == 0) return null;

        if (_ajustes.TieneContrasena && !_ajustes.VerificarContrasena(ContrasenaActualBox.Password))
            return "La contraseña actual no es correcta.";
        if (nueva.Length < ContrasenaAcceso.LongitudMinima)
            return $"La nueva contraseña debe tener al menos {ContrasenaAcceso.LongitudMinima} caracteres.";
        if (nueva != ContrasenaConfirmarBox.Password)
            return "Las contraseñas nuevas no coinciden.";
        return null;
    }

    private string ModeloIngresado => ModeloCombo.Text.Trim();

    private string? ClaveParaUsar =>
        string.IsNullOrWhiteSpace(ApiKeyBox.Password) ? _ajustes.ObtenerApiKey() : ApiKeyBox.Password.Trim();

    private async void ProbarButton_Click(object sender, RoutedEventArgs e)
    {
        if (ClaveParaUsar is not { } clave)
        {
            ResultadoPruebaText.Text = "Ingresa una clave para probar la conexión.";
            return;
        }
        if (ModeloIngresado.Length == 0)
        {
            ResultadoPruebaText.Text = "Escribe o selecciona un modelo.";
            return;
        }

        ProbarButton.IsEnabled = false;
        ResultadoPruebaText.Text = "Probando conexión…";
        try
        {
            await ClienteGemini.VerificarAsync(clave, ModeloIngresado);
            ResultadoPruebaText.Text = "✔ Conexión correcta: la clave y el modelo funcionan.";
        }
        catch (Exception ex)
        {
            ResultadoPruebaText.Text = $"✖ {ex.Message}";
        }
        finally
        {
            ProbarButton.IsEnabled = true;
        }
    }

    private void GuardarButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModeloIngresado.Length == 0)
        {
            MessageBox.Show("Escribe o selecciona un modelo.", "Configuración",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ErrorCambioContrasena() is { } error)
        {
            ContrasenaErrorText.Text = error;
            ContrasenaErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password))
        {
            _ajustes.EstablecerApiKey(ApiKeyBox.Password.Trim());
        }
        _ajustes.Modelo = ModeloIngresado;
        if (MinutosBloqueoCombo.SelectedValue is int minutos) _ajustes.MinutosBloqueo = minutos;
        if (ContrasenaNuevaBox.Password.Length > 0) _ajustes.EstablecerContrasena(ContrasenaNuevaBox.Password);

        try
        {
            _ajustes.Guardar();
            DialogResult = true; // cierra la ventana
        }
        catch (IOException ex)
        {
            MessageBox.Show($"No se pudo guardar la configuración:\n{ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}