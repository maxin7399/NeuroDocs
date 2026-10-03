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

        ModeloCombo.ItemsSource = ConfiguracionGemini.ModelosSugeridos;
        ModeloCombo.Text = _ajustes.Modelo ?? ConfiguracionGemini.ModeloPorDefecto;

        // La clave nunca se muestra: solo se indica si ya hay una guardada.
        EstadoClaveText.Text = _ajustes.TieneApiKey
            ? "Hay una clave guardada. Deja el campo vacío para conservarla."
            : "No hay ninguna clave guardada.";
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
            await ClasificadorFuncionesGemini.VerificarAsync(clave, ModeloIngresado);
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

        if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password))
        {
            _ajustes.EstablecerApiKey(ApiKeyBox.Password.Trim());
        }
        _ajustes.Modelo = ModeloIngresado;

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