using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NeuroDocs.Models;
using NeuroDocs.Services;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Net.Http;

namespace NeuroDocs;

public partial class MainWindow : Window
{
    private static readonly string RutaPlantilla =
        Path.Combine(AppContext.BaseDirectory, "Plantillas", "PlantillaPlanRehabilitacion.docx");
    private void ConfiguracionButton_Click(object sender, RoutedEventArgs e) =>
    new ConfiguracionWindow { Owner = this }.ShowDialog();

    private readonly PdfTextExtractor _extractor = new();
    private readonly HistoriaClinicaParser _historiaParser = new();
    private readonly OrdenServicioParser _ordenParser = new();
    private readonly GeneradorPlanWord _generador = new();
    private readonly ConvertidorPdfWord _convertidorPdf = new();
    private readonly FirmaExtractor _firmaExtractor = new();
    private readonly string?[] _rutasInformes = new string?[2];
    private string? _rutaHistoria;
    private string? _rutaOrden;
    private (DatosPaciente Paciente, DatosOrden Orden, Firma? Firma)? _lectura;
    private ResultadoWindow? _ventanaResultado;

    public MainWindow()
    {
        InitializeComponent();
        InicializarEmisionPlan();
    }

    // ───────────── Selección de archivos ─────────────

    private void SeleccionarHistoriaButton_Click(object sender, RoutedEventArgs e)
    {
        if (SeleccionarPdf("Seleccionar historia clínica (ECC)") is not { } ruta) return;

        _rutaHistoria = ruta;
        MostrarRuta(HistoriaRutaText, ruta);
        ActualizarEstado();
    }

    private void SeleccionarOrdenButton_Click(object sender, RoutedEventArgs e)
    {
        if (SeleccionarPdf("Seleccionar orden de servicio (CUPS)") is not { } ruta) return;

        _rutaOrden = ruta;
        MostrarRuta(OrdenRutaText, ruta);
        ActualizarEstado();
    }

    private static string? SeleccionarPdf(string titulo)
    {
        var dialogo = new OpenFileDialog { Title = titulo, Filter = "Archivos PDF (*.pdf)|*.pdf" };
        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }

    private static void MostrarRuta(TextBlock destino, string ruta)
    {
        destino.Text = Path.GetFileName(ruta);
        destino.ToolTip = ruta;
    }

    private void ActualizarEstado()
    {
        LeerButton.IsEnabled = _rutaHistoria is not null && _rutaOrden is not null;
        _lectura = null;
        GenerarButton.IsEnabled = false;
        _ventanaResultado?.Close(); // si cambian los archivos, la vista previa ya no es válida
    }
    // ───────────── Informes para la IA ─────────────

    private TextBlock[] TextosInformes => [Informe1RutaText, Informe2RutaText];

    private void SeleccionarInforme_Click(object sender, RoutedEventArgs e)
    {
        int indice = IndiceInforme(sender);
        if (SeleccionarPdf($"Seleccionar informe {indice + 1} para la IA") is not { } ruta) return;

        _rutasInformes[indice] = ruta;
        MostrarRuta(TextosInformes[indice], ruta);
        ActualizarEstadoIA();
    }

    private void QuitarInforme_Click(object sender, RoutedEventArgs e)
    {
        int indice = IndiceInforme(sender);

        _rutasInformes[indice] = null;
        TextosInformes[indice].Text = "Ninguno";
        TextosInformes[indice].ToolTip = null;
        ActualizarEstadoIA();
    }

    private static int IndiceInforme(object sender) => int.Parse((string)((FrameworkElement)sender).Tag);

    private void ActualizarEstadoIA() =>
        SugerirIAButton.IsEnabled = _rutasInformes.Any(r => r is not null);

    // ───────────── Emisión del plan ─────────────

    private void InicializarEmisionPlan()
    {
        var hoy = DateTime.Today;

        MesCombo.ItemsSource = Enumerable.Range(1, 12)
            .Select(m => new OpcionMes(m, FormatoFecha.NombreMes(m)))
            .ToList();
        AnioCombo.ItemsSource = Enumerable.Range(hoy.Year - 1, 3).ToList();

        MesCombo.SelectedValue = hoy.Month;
        AnioCombo.SelectedItem = hoy.Year;
    }

    private DateOnly EmisionPlan => new((int)AnioCombo.SelectedItem, (int)MesCombo.SelectedValue, 1);

    // ───────────── Lectura ─────────────
    private async void SugerirIAButton_Click(object sender, RoutedEventArgs e)
    {
        var rutas = _rutasInformes.OfType<string>().ToList();
        if (rutas.Count == 0) return;

        bool hayTexto = !string.IsNullOrWhiteSpace(ConservadosTextBox.Text)
                     || !string.IsNullOrWhiteSpace(AlteradosTextBox.Text);
        if (hayTexto &&
            MessageBox.Show("Esto reemplazará lo que hay en Conservados y Alterados. ¿Continuar?", "Sugerir con IA",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        SugerirIAButton.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var clasificador = new ClasificadorFuncionesGemini(ConfiguracionGemini.Cargar());
            var funciones = await clasificador.ClasificarAsync(rutas);

            ConservadosTextBox.Text = string.Join(Environment.NewLine, funciones.Conservados);
            AlteradosTextBox.Text = string.Join(Environment.NewLine, funciones.Alterados);
        }
        catch (HttpRequestException ex)
        {
            MostrarError($"No se pudo conectar con Gemini. Revisa la conexión a internet.\n\n{ex.Message}");
        }
        catch (TaskCanceledException)
        {
            MostrarError("Gemini tardó demasiado en responder. Intenta de nuevo.");
        }
        catch (IOException ex)
        {
            MostrarError($"No se pudo leer uno de los informes. Si está abierto en otro programa, ciérralo.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            MostrarError(ex.Message);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            ActualizarEstadoIA();
        }
    }
    private async void LeerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rutaHistoria is not { } rutaHistoria || _rutaOrden is not { } rutaOrden) return;

        LeerButton.IsEnabled = false;
        try
        {
            var (paciente, orden, firma) = await Task.Run(() =>
            {
                var lineasHistoria = _extractor.ExtractLines(rutaHistoria);
                return (
                    _historiaParser.Parse(lineasHistoria),
                    _ordenParser.Parse(_extractor.ExtractLines(rutaOrden)),
                    _firmaExtractor.Extraer(rutaHistoria, lineasHistoria));
            });

            _lectura = (paciente, orden, firma);
            MostrarResultado(Formatear(ConstruirPlan(paciente, orden, firma), ValidadorDocumentos.Validar(paciente, orden)));
            GenerarButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudieron leer los documentos:\n{ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LeerButton.IsEnabled = true;
        }
    }

    // ───────────── Generación ─────────────

    private async void GenerarButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lectura is not { } lectura || _rutaHistoria is not { } rutaHistoria) return;

        if (!File.Exists(RutaPlantilla))
        {
            MessageBox.Show($"No se encontró la plantilla en:\n{RutaPlantilla}", "Plantilla no encontrada",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var plan = ConstruirPlan(lectura.Paciente, lectura.Orden, lectura.Firma);

        if (plan.Funciones.Conservados.Count == 0 && plan.Funciones.Alterados.Count == 0 &&
            MessageBox.Show("No ingresaste funciones conservadas ni alteradas. ¿Generar de todas formas?",
                            "Funciones vacías", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (plan.Firma is null &&
            MessageBox.Show("No se encontró la firma en la historia clínica. ¿Generar el plan sin firma?",
                            "Firma no encontrada", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var dialogo = new SaveFileDialog
        {
            Title = "Guardar plan de rehabilitación",
            Filter = "Documento PDF (*.pdf)|*.pdf",
            FileName = NombreSugerido(rutaHistoria, plan.EmisionPlan) + ".pdf",
            InitialDirectory = Path.GetDirectoryName(rutaHistoria)
        };
        if (dialogo.ShowDialog() != true) return;

        string rutaPdf = dialogo.FileName;
        bool conservarWord = GuardarWordCheckBox.IsChecked == true;
        string rutaDocx = conservarWord
            ? Path.ChangeExtension(rutaPdf, ".docx")
            : Path.Combine(Path.GetTempPath(), $"NeuroDocs_{Guid.NewGuid():N}.docx");

        GenerarButton.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait; // Word tarda unos segundos en abrir y exportar
        try
        {
            await Task.Run(() => _generador.Generar(RutaPlantilla, rutaDocx, plan));
            await _convertidorPdf.ConvertirAsync(rutaDocx, rutaPdf);

            Mouse.OverrideCursor = null;
            if (MessageBox.Show("Plan generado correctamente. ¿Deseas abrir el PDF?", "NeuroDocs",
                                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(rutaPdf) { UseShellExecute = true });
            }
        }
        catch (IOException ex)
        {
            MostrarError($"No se pudo guardar el Word. Si está abierto, ciérralo e intenta de nuevo.\n\n{ex.Message}");
        }
        catch (COMException ex)
        {
            MostrarError($"Word no pudo exportar el PDF. Si el PDF está abierto en otro programa, ciérralo e intenta de nuevo.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo generar el plan:\n{ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
            GenerarButton.IsEnabled = true;

            if (!conservarWord)
            {
                try { File.Delete(rutaDocx); } catch (IOException) { } // temporal: si falla el borrado, no es crítico
            }
        }
    }

    private static void MostrarError(string mensaje) =>
        MessageBox.Show(mensaje, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

    private DatosPlan ConstruirPlan(DatosPaciente paciente, DatosOrden orden, Firma? firma) =>
    new(paciente, orden, EmisionPlan,
        new FuncionesEvaluadas(
            ListaTexto.ParsearItems(ConservadosTextBox.Text),
            ListaTexto.ParsearItems(AlteradosTextBox.Text)),
        firma);

    /// <summary>"OscarminMuñozJimenezECC.pdf" → "OscarminMuñozJimenezPlanRehaSeptiembre2026"</summary>
    private static string NombreSugerido(string rutaHistoria, DateOnly emision)
    {
        string nombre = Path.GetFileNameWithoutExtension(rutaHistoria);
        if (nombre.EndsWith("ECC", StringComparison.OrdinalIgnoreCase)) nombre = nombre[..^3];

        return $"{nombre}PlanReha{FormatoFecha.NombreMes(emision.Month)}{emision.Year}";
    }

    // ───────────── Vista previa ─────────────
    private void MostrarResultado(string texto)
    {
        if (_ventanaResultado is null)
        {
            _ventanaResultado = new ResultadoWindow { Owner = this };
            _ventanaResultado.Closed += (_, _) => _ventanaResultado = null;
            _ventanaResultado.Show();
        }

        _ventanaResultado.MostrarContenido(texto);
        _ventanaResultado.Activate();
    }
    private static string Formatear(DatosPlan plan, IReadOnlyList<string> advertencias)
    {
        const string NoEncontrado = "(no encontrado)";
        var sb = new StringBuilder();

        if (advertencias.Count > 0)
        {
            sb.AppendLine("⚠ ADVERTENCIAS");
            foreach (var a in advertencias) sb.AppendLine($"  {a}");
            sb.AppendLine();
        }

        sb.AppendLine("IDENTIFICACIÓN (historia clínica)\n");
        foreach (var campo in CamposPaciente.Todos)
        {
            string valor = plan.Paciente.Campos.TryGetValue(campo, out var v) ? v : "— (no aparece en el documento)";
            sb.AppendLine($"{campo}: {valor}");
        }

        sb.AppendLine("\nDIAGNÓSTICO (historia clínica)");
        if (plan.Paciente.Diagnosticos.Count == 0) sb.AppendLine(NoEncontrado);
        foreach (var d in plan.Paciente.Diagnosticos) sb.AppendLine($"• {d}");

        sb.AppendLine("\nORDEN DE SERVICIO");
        sb.AppendLine($"Sesiones mensuales: {plan.Orden.SesionesMensuales?.ToString() ?? NoEncontrado}");
        sb.AppendLine($"Meses de terapia: {plan.Orden.Meses?.ToString() ?? NoEncontrado}");
        sb.AppendLine(plan.Orden.Cie10Codigo is null
            ? $"CIE-10: {NoEncontrado}"
            : $"CIE-10: {plan.Orden.Cie10Codigo} - {plan.Orden.Cie10Descripcion}");

        sb.AppendLine("\nPLAN");
        sb.AppendLine($"Emisión del Plan: {FormatoFecha.MesAnio(plan.EmisionPlan)}");

        AgregarLista(sb, "CONSERVADOS", plan.Funciones.Conservados);
        AgregarLista(sb, "ALTERADOS", plan.Funciones.Alterados);

        sb.AppendLine("\nFIRMA (historia clínica)");
        if (plan.Firma is null)
        {
            sb.AppendLine("(no encontrada)");
        }
        else
        {
            foreach (var linea in plan.Firma.Lineas) sb.AppendLine(linea);
            sb.AppendLine($"[Imagen {plan.Firma.Formato}, {plan.Firma.AnchoPt:0} × {plan.Firma.AltoPt:0} pt]");
        }
        return sb.ToString();
    }

    private static void AgregarLista(StringBuilder sb, string titulo, IReadOnlyList<string> items)
    {
        sb.AppendLine($"\n{titulo} ({items.Count})");
        if (items.Count == 0) sb.AppendLine("(sin datos)");
        foreach (var item in items) sb.AppendLine($"• {item}");
    }
}