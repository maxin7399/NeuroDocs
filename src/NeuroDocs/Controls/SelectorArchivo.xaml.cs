using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace NeuroDocs.Controls;

/// <summary>
/// Espacio para un PDF: selección por diálogo o arrastrando el archivo, con indicador de cargado
/// y botón opcional para quitarlo.
/// </summary>
public partial class SelectorArchivo : UserControl
{
    private const string TextoVacio = "Ningún archivo · arrastra un PDF aquí";
    private string? _ruta;

    /// <summary>Se dispara al seleccionar, soltar o quitar un archivo. La ruta nueva está en <see cref="Ruta"/>.</summary>
    public event EventHandler? RutaCambiada;

    public SelectorArchivo()
    {
        InitializeComponent();
        ActualizarVista();
    }

    public string Titulo
    {
        get => TituloText.Text;
        set => TituloText.Text = value;
    }

    public string Icono
    {
        get => IconoText.Text;
        set => IconoText.Text = value;
    }

    public bool PermiteQuitar { get; set; }

    public string? Ruta
    {
        get => _ruta;
        private set
        {
            _ruta = value;
            ActualizarVista();
            RutaCambiada?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ActualizarVista()
    {
        bool hayArchivo = _ruta is not null;

        ArchivoText.Text = hayArchivo ? Path.GetFileName(_ruta) : TextoVacio;
        ArchivoText.ToolTip = _ruta;
        CheckText.Visibility = hayArchivo ? Visibility.Visible : Visibility.Collapsed;
        QuitarButton.Visibility = hayArchivo && PermiteQuitar ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SeleccionarButton_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = $"Seleccionar: {Titulo}",
            Filter = "Archivos PDF (*.pdf)|*.pdf"
        };
        if (dialogo.ShowDialog() == true) Ruta = dialogo.FileName;
    }

    private void QuitarButton_Click(object sender, RoutedEventArgs e) => Ruta = null;

    // ───────────── Arrastrar y soltar ─────────────

    private static string? PrimerPdf(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] archivos
            ? archivos.FirstOrDefault(a => Path.GetExtension(a).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            : null;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        bool esPdf = PrimerPdf(e) is not null;
        e.Effects = esPdf ? DragDropEffects.Copy : DragDropEffects.None;
        Contenedor.BorderBrush = esPdf ? (Brush)FindResource("AcentoBrush") : Brushes.Transparent;
        Contenedor.Background = esPdf ? (Brush)FindResource("AcentoSuaveBrush") : Brushes.Transparent;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => QuitarResaltado();

    private void OnDrop(object sender, DragEventArgs e)
    {
        QuitarResaltado();
        if (PrimerPdf(e) is { } ruta) Ruta = ruta;
        e.Handled = true;
    }

    private void QuitarResaltado()
    {
        Contenedor.BorderBrush = Brushes.Transparent;
        Contenedor.Background = Brushes.Transparent;
    }
}