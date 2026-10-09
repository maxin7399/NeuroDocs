using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using NeuroDocs.Models;
using NeuroDocs.Services;

namespace NeuroDocs;

public partial class MainWindow : Window
{
    private static readonly string RutaPlantilla =
        Path.Combine(AppContext.BaseDirectory, "Plantillas", "PlantillaPlanRehabilitacion.docx");

    private readonly PdfTextExtractor _extractor = new();
    private readonly HistoriaClinicaParser _historiaParser = new();
    private readonly OrdenServicioParser _ordenParser = new();
    private readonly FirmaExtractor _firmaExtractor = new();
    private readonly GeneradorPlanWord _generador = new();
    private readonly ConvertidorPdfWord _convertidorPdf = new();

    private (DatosPaciente Paciente, DatosOrden Orden, Firma? Firma)? _lectura;
    private ResultadoWindow? _ventanaResultado;
    private int _versionLectura;   // descarta lecturas viejas si cambian los archivos mientras se leen
    private Action? _accionAviso;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{InfoAplicacion.Version}";
        VersionText.ToolTip = InfoAplicacion.VersionCompleta;
        InicializarEmisionPlan();
        ActualizarConteos();
    }

    // ───────────── Paso 1: documentos del paciente ─────────────

    private async void DocumentoPaciente_RutaCambiada(object? sender, EventArgs e) => await LeerDocumentosAsync();

    /// <summary>Lee ECC y CUPS automáticamente en cuanto ambos están seleccionados.</summary>
    private async Task LeerDocumentosAsync()
    {
        _lectura = null;
        GenerarButton.IsEnabled = false;
        _ventanaResultado?.Close();
        OcultarAviso();

        if (HistoriaSelector.Ruta is not { } rutaHistoria || OrdenSelector.Ruta is not { } rutaOrden)
        {
            ResumenBorder.Visibility = Visibility.Collapsed;
            return;
        }

        int version = ++_versionLectura;
        MostrarResumenCargando();

        try
        {
            var lectura = await Task.Run(() =>
            {
                var lineasHistoria = _extractor.ExtractLines(rutaHistoria);
                return (
                    Paciente: _historiaParser.Parse(lineasHistoria),
                    Orden: _ordenParser.Parse(_extractor.ExtractLines(rutaOrden)),
                    Firma: _firmaExtractor.Extraer(rutaHistoria, lineasHistoria));
            });

            if (version != _versionLectura) return;

            _lectura = lectura;
            MostrarResumen(lectura.Paciente, lectura.Orden);
            GenerarButton.IsEnabled = true;

            var advertencias = ValidadorDocumentos.Validar(lectura.Paciente, lectura.Orden).ToList();
            if (lectura.Firma is null)
                advertencias.Add("No se encontró la firma del profesional en la historia clínica.");

            if (advertencias.Count > 0)
                MostrarAviso(TipoAviso.Advertencia, string.Join(Environment.NewLine, advertencias));
        }
        catch (Exception ex)
        {
            if (version != _versionLectura) return;

            ResumenBorder.Visibility = Visibility.Collapsed;
            MostrarAviso(TipoAviso.Error, $"No se pudieron leer los documentos: {ex.Message}");
        }
    }

    private void MostrarResumenCargando()
    {
        ResumenNombreText.Text = "Leyendo documentos…";
        ResumenDetalleText.Text = "";
        VerDatosButton.Visibility = Visibility.Collapsed;
        ResumenBorder.Visibility = Visibility.Visible;
    }

    private void MostrarResumen(DatosPaciente paciente, DatosOrden orden)
    {
        ResumenNombreText.Text = paciente.Campos.TryGetValue(CamposPaciente.Nombre, out var nombre)
            ? nombre
            : "(nombre no encontrado)";

        // "C.C. 8601373, Repelón – Atlántico." → "C.C. 8601373"
        string documento = paciente.Campos.TryGetValue(CamposPaciente.Identificacion, out var id)
            ? id.Split(',')[0].Trim()
            : "Documento no encontrado";

        string terapia = orden is { SesionesMensuales: { } s, Meses: { } m } ? $"{s} sesiones × {m} meses" : "Terapia no encontrada";

        ResumenDetalleText.Text = $"{documento} · CIE-10 {orden.Cie10Codigo ?? "—"} · {terapia}";
        VerDatosButton.Visibility = Visibility.Visible;
    }

    private void VerDatosButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lectura is not { } lectura) return;

        var plan = ConstruirPlan(lectura.Paciente, lectura.Orden, lectura.Firma);
        MostrarResultado(Formatear(plan, ValidadorDocumentos.Validar(lectura.Paciente, lectura.Orden)));
    }

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

    // ───────────── Paso 2: funciones con IA ─────────────

    private List<string> RutasInformes =>
        new[] { Informe1Selector.Ruta, Informe2Selector.Ruta }.OfType<string>().ToList();

    private void Informe_RutaCambiada(object? sender, EventArgs e) =>
        SugerirIAButton.IsEnabled = RutasInformes.Count > 0;

    private async void SugerirIAButton_Click(object sender, RoutedEventArgs e)
    {
        var rutas = RutasInformes;
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
        MostrarAviso(TipoAviso.Informacion, "Consultando a Gemini…");
        try
        {
            var clasificador = new ClasificadorFuncionesGemini(ConfiguracionGemini.Cargar());
            var funciones = await clasificador.ClasificarAsync(rutas);

            ConservadosTextBox.Text = string.Join(Environment.NewLine, funciones.Conservados);
            AlteradosTextBox.Text = string.Join(Environment.NewLine, funciones.Alterados);

            MostrarAviso(TipoAviso.Exito, "Sugerencia lista. Revisa ambas listas antes de generar el plan.");
        }
        catch (HttpRequestException ex)
        {
            MostrarAviso(TipoAviso.Error, $"No se pudo conectar con Gemini. Revisa la conexión a internet. ({ex.Message})");
        }
        catch (TaskCanceledException)
        {
            MostrarAviso(TipoAviso.Error, "Gemini tardó demasiado en responder. Intenta de nuevo.");
        }
        catch (IOException ex)
        {
            MostrarAviso(TipoAviso.Error, $"No se pudo leer uno de los informes. Si está abierto en otro programa, ciérralo. ({ex.Message})");
        }
        catch (Exception ex)
        {
            MostrarAviso(TipoAviso.Error, ex.Message);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            SugerirIAButton.IsEnabled = RutasInformes.Count > 0;
        }
    }

    private void Funciones_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        ActualizarConteos();

    private void ActualizarConteos()
    {
        // TextChanged puede dispararse mientras se construye la ventana, antes de que existan los contadores.
        if (ConservadosConteo is null || AlteradosConteo is null) return;

        ConservadosConteo.Text = DescribirConteo(ListaTexto.ParsearItems(ConservadosTextBox.Text).Count);
        AlteradosConteo.Text = DescribirConteo(ListaTexto.ParsearItems(AlteradosTextBox.Text).Count);
    }

    private static string DescribirConteo(int cantidad) => cantidad == 1 ? "1 ítem" : $"{cantidad} ítems";

    // ───────────── Paso 3: generar ─────────────

    private async void GenerarButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lectura is not { } lectura || HistoriaSelector.Ruta is not { } rutaHistoria) return;

        if (!File.Exists(RutaPlantilla))
        {
            MostrarAviso(TipoAviso.Error, $"No se encontró la plantilla en: {RutaPlantilla}");
            return;
        }

        var plan = ConstruirPlan(lectura.Paciente, lectura.Orden, lectura.Firma);

        if (plan.Funciones.Conservados.Count == 0 && plan.Funciones.Alterados.Count == 0 &&
            MessageBox.Show("No hay funciones conservadas ni alteradas. ¿Generar de todas formas?",
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
        Mouse.OverrideCursor = Cursors.Wait;
        MostrarAviso(TipoAviso.Informacion, "Generando el plan…");
        try
        {
            await Task.Run(() => _generador.Generar(RutaPlantilla, rutaDocx, plan));
            await _convertidorPdf.ConvertirAsync(rutaDocx, rutaPdf);

            MostrarAviso(TipoAviso.Exito, $"Plan guardado: {Path.GetFileName(rutaPdf)}",
                         "Abrir PDF", () => AbrirArchivo(rutaPdf));
        }
        catch (IOException ex)
        {
            MostrarAviso(TipoAviso.Error, $"No se pudo guardar el Word. Si está abierto, ciérralo e intenta de nuevo. ({ex.Message})");
        }
        catch (COMException ex)
        {
            MostrarAviso(TipoAviso.Error, $"Word no pudo exportar el PDF. Si el PDF está abierto en otro programa, ciérralo. ({ex.Message})");
        }
        catch (Exception ex)
        {
            MostrarAviso(TipoAviso.Error, $"No se pudo generar el plan: {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
            GenerarButton.IsEnabled = _lectura is not null;

            if (!conservarWord)
            {
                try { File.Delete(rutaDocx); } catch (IOException) { }
            }
        }
    }

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

    private static void AbrirArchivo(string ruta) =>
        Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });

    // ───────────── Barra de avisos ─────────────

    private enum TipoAviso { Informacion, Exito, Advertencia, Error }

    private static readonly Dictionary<TipoAviso, (string Icono, string ClaveFondo, string ClaveIcono)> EstilosAviso = new()
    {
        [TipoAviso.Informacion] = ("\uE946", "AvisoInfoFondoBrush", "AvisoInfoIconoBrush"),
        [TipoAviso.Exito] = ("\uE930", "AvisoExitoFondoBrush", "AvisoExitoIconoBrush"),
        [TipoAviso.Advertencia] = ("\uE7BA", "AvisoAdvertenciaFondoBrush", "AvisoAdvertenciaIconoBrush"),
        [TipoAviso.Error] = ("\uEA39", "AvisoErrorFondoBrush", "AvisoErrorIconoBrush"),
    };

    private void MostrarAviso(TipoAviso tipo, string mensaje, string? textoAccion = null, Action? accion = null)
    {
        var (icono, claveFondo, claveIcono) = EstilosAviso[tipo];

        AvisoBorder.Background = (Brush)FindResource(claveFondo);
        AvisoIconoText.Text = icono;
        AvisoIconoText.Foreground = (Brush)FindResource(claveIcono);
        AvisoText.Text = mensaje;

        _accionAviso = accion;
        AvisoAccionButton.Content = textoAccion;
        AvisoAccionButton.Visibility = accion is null ? Visibility.Collapsed : Visibility.Visible;

        AvisoBorder.Visibility = Visibility.Visible;
    }

    private void OcultarAviso()
    {
        AvisoBorder.Visibility = Visibility.Collapsed;
        _accionAviso = null;
    }

    private void AvisoAccionButton_Click(object sender, RoutedEventArgs e) => _accionAviso?.Invoke();

    private void CerrarAvisoButton_Click(object sender, RoutedEventArgs e) => OcultarAviso();

    // ───────────── Ventanas auxiliares ─────────────

    private void ConfiguracionButton_Click(object sender, RoutedEventArgs e) =>
        new ConfiguracionWindow { Owner = this }.ShowDialog();

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

        sb.AppendLine($"\nDIAGNÓSTICO (fuente: {plan.OrigenDiagnostico})");
        sb.AppendLine(plan.Diagnostico.Length > 0 ? plan.Diagnostico : NoEncontrado);

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
            sb.AppendLine(NoEncontrado);
        }
        else
        {
            if (plan.Firma.Lineas.Count == 0) sb.AppendLine("(nombre y registro incluidos dentro de la imagen)");
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