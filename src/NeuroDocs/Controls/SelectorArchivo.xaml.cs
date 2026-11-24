using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    private bool _permiteOcultar;

    /// <summary>
    /// Selector adicional: la X se muestra también sin archivo. Al pulsarla se vacía y dispara
    /// <see cref="RutaCambiada"/> con ruta nula, que la ventana usa para ocultarlo.
    /// </summary>
    public bool PermiteOcultar
    {
        get => _permiteOcultar;
        set { _permiteOcultar = value; ActualizarVista(); }
    }

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
        bool recienCargado = hayArchivo && IconoText.Text != IconoCompletado;

        // Vacío: círculo con borde y documento gris. Cargado: círculo lila relleno con check blanco.
        IconoText.Text = hayArchivo ? IconoCompletado : _iconoVacio;
        IconoText.Foreground = hayArchivo ? Brushes.White : (Brush)FindResource("TextoSecundarioBrush");
        IconoCirculo.Background = hayArchivo ? (Brush)FindResource("ExitoBrush") : Brushes.Transparent;
        IconoCirculo.BorderBrush = (Brush)FindResource(hayArchivo ? "ExitoBrush" : "BordeTarjetaBrush");
        if (recienCargado && SystemParameters.ClientAreaAnimation)
        {
            var rebote = new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 }
            };
            IconoEscala.BeginAnimation(ScaleTransform.ScaleXProperty, rebote);
            IconoEscala.BeginAnimation(ScaleTransform.ScaleYProperty, rebote);
        }

        ArchivoText.Text = hayArchivo
            ? Path.GetFileName(_ruta)
            : _extensiones == "pdf" ? "Ningún archivo · arrastra un PDF aquí" : "Ningún archivo · arrastra el archivo aquí";
        ArchivoText.ToolTip = _ruta;
        QuitarButton.Visibility = (hayArchivo && PermiteQuitar) || _permiteOcultar ? Visibility.Visible : Visibility.Collapsed;
        QuitarButton.ToolTip = _permiteOcultar ? "Quitar y ocultar" : "Quitar archivo";
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
        Contenedor.Background = IsMouseOver ? (Brush)FindResource("FondoHoverBrush") : Brushes.Transparent;
    }

    // Fondo lavanda suave al pasar el mouse por la fila.
    private void OnMouseEnter(object sender, MouseEventArgs e) =>
        Contenedor.Background = (Brush)FindResource("FondoHoverBrush");

    private void OnMouseLeave(object sender, MouseEventArgs e) => QuitarResaltado();
}