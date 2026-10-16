using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace NeuroDocs.Controls;

/// <summary>Espacio para un archivo: diálogo o arrastrar y soltar, con indicador de cargado y botón opcional para quitarlo.</summary>
public partial class SelectorArchivo : UserControl
{
    private const string IconoDocumento = "\uE8A5";
    private const string IconoCompletado = "\uE930";

    private string? _ruta;
    private string _iconoVacio = IconoDocumento;
    private string _extensiones = "pdf";

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
        get => _iconoVacio;
        set { _iconoVacio = value; ActualizarVista(); }
    }

    public bool PermiteQuitar { get; set; }

    /// <summary>Filtro del diálogo de selección.</summary>
    public string Filtro { get; set; } = "Archivos PDF (*.pdf)|*.pdf";

    /// <summary>Extensiones aceptadas al arrastrar, separadas por comas: "pdf" o "rtf,docx,pdf".</summary>
    public string Extensiones
    {
        get => _extensiones;
        set { _extensiones = value; ActualizarVista(); }
    }

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

    public void Limpiar() => Ruta = null;

    private void ActualizarVista()
    {
        bool hayArchivo = _ruta is not null;

        IconoText.Text = hayArchivo ? IconoCompletado : _iconoVacio;
        IconoText.Foreground = (Brush)FindResource(hayArchivo ? "ExitoBrush" : "TextoSecundarioBrush");

        ArchivoText.Text = hayArchivo
            ? Path.GetFileName(_ruta)
            : _extensiones == "pdf" ? "Ningún archivo · arrastra un PDF aquí" : "Ningún archivo · arrastra el archivo aquí";
        ArchivoText.ToolTip = _ruta;
        QuitarButton.Visibility = hayArchivo && PermiteQuitar ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SeleccionarButton_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog { Title = $"Seleccionar: {Titulo}", Filter = Filtro };
        if (dialogo.ShowDialog() == true) Ruta = dialogo.FileName;
    }

    private void QuitarButton_Click(object sender, RoutedEventArgs e) => Ruta = null;

    // ───────────── Arrastrar y soltar ─────────────

    private bool EsAceptado(string ruta) =>
        _extensiones.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(ext => Path.GetExtension(ruta).Equals("." + ext, StringComparison.OrdinalIgnoreCase));

    private string? PrimerAceptado(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] archivos ? archivos.FirstOrDefault(EsAceptado) : null;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        bool valido = PrimerAceptado(e) is not null;
        e.Effects = valido ? DragDropEffects.Copy : DragDropEffects.None;
        Contenedor.BorderBrush = valido ? (Brush)FindResource("AcentoBrush") : Brushes.Transparent;
        Contenedor.Background = valido ? (Brush)FindResource("AcentoSuaveBrush") : Brushes.Transparent;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => QuitarResaltado();

    private void OnDrop(object sender, DragEventArgs e)
    {
        QuitarResaltado();
        if (PrimerAceptado(e) is { } ruta) Ruta = ruta;
        e.Handled = true;
    }

    private void QuitarResaltado()
    {
        Contenedor.BorderBrush = Brushes.Transparent;
        Contenedor.Background = Brushes.Transparent;
    }
}