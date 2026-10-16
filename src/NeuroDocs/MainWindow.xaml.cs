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
    private readonly LectorOrden _lectorOrden = new();
    private readonly FirmaExtractor _firmaExtractor = new();
    private readonly GeneradorPlanWord _generador = new();
    private readonly ConvertidorPdfWord _convertidorPdf = new();

    private (DatosPaciente Paciente, DatosOrden Orden, Firma? Firma)? _lectura;
    private ResultadoWindow? _ventanaResultado;
    private int _versionLectura;   // descarta lecturas viejas si cambian los archivos mientras se leen
    private Action? _accionAviso;
    private static readonly string RutaPlantillaInforme =
    Path.Combine(AppContext.BaseDirectory, "Plantillas", "PlantillaInformeEvolucion.docx");

    private readonly GeneradorInformeWord _generadorInforme = new();
    private TipoInforme? _tipoRedactado; // con qué tipo se redactó el texto actual

    private TipoInforme TipoSeleccionado =>
        TipoFinalRadio.IsChecked == true ? TipoInforme.Final : TipoInforme.EvolucionMensual;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{InfoAplicacion.Version}";
        VersionText.ToolTip = InfoAplicacion.VersionCompleta;
        InicializarEmisionPlan();
        ActualizarConteos();
        ActualizarConteosInforme();
    }

    // ───────────── Paso 1: documentos del paciente ─────────────

    private async void DocumentoPaciente_RutaCambiada(object? sender, EventArgs e)
    {
        // Un ECC nuevo es un paciente nuevo: los informes y las funciones del anterior no deben arrastrarse.
        if (sender == HistoriaSelector) LimpiarDatosDelPacienteAnterior();

        await LeerDocumentosAsync();
    }

    private void LimpiarDatosDelPacienteAnterior()
    {
        Informe1Selector.Limpiar();
        Informe2Selector.Limpiar();
        ConservadosTextBox.Clear();
        AlteradosTextBox.Clear();

        EvaluacionSelector.Limpiar();
        CondicionTextBox.Clear();
        PlanTextBox.Clear();
        TipoMensualRadio.IsChecked = true;
        _tipoRedactado = null;
    }

    /// <summary>Lee ECC y CUPS automáticamente en cuanto ambos están seleccionados.</summary>
    private async Task LeerDocumentosAsync()
    {
        int version = ++_versionLectura;

        _lectura = null;
        ActualizarBotonesGenerar();
        _ventanaResultado?.Close();
        OcultarAviso();

        if (HistoriaSelector.Ruta is not { } rutaHistoria || OrdenSelector.Ruta is not { } rutaOrden)
        {
            ResumenBorder.Visibility = Visibility.Collapsed;
            return;
        }

        MostrarResumenCargando();

        try
        {
            var lectura = await Task.Run(() =>
            {
                var lineasHistoria = _extractor.ExtractLines(rutaHistoria);
                _lectorOrden.TryParse(_extractor.ExtractLines(rutaOrden), out var ordenLeida);
                return (
                    Paciente: _historiaParser.Parse(lineasHistoria),
                    Orden: ordenLeida,
                    Firma: _firmaExtractor.Extraer(rutaHistoria, lineasHistoria));
            });

            if (version != _versionLectura) return;

            if (lectura.Orden is not { } orden)
            {
                ResumenBorder.Visibility = Visibility.Collapsed;
                MostrarAviso(TipoAviso.Error, LectorOrden.MensajeNoReconocido);
                return;
            }

            _lectura = (lectura.Paciente, orden, lectura.Firma);
            MostrarResumen(lectura.Paciente, orden);
            ActualizarBotonesGenerar();

            var advertencias = ValidadorDocumentos.Validar(lectura.Paciente, orden).ToList();
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
    private void ActualizarBotonesGenerar()
    {
        bool listo = _lectura is not null;
        GenerarButton.IsEnabled = listo;
        GenerarInformeButton.IsEnabled = listo;
    }
    private void ModoTabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Solo cambios de pestaña, no selecciones de controles internos que burbujean hasta aquí.
        if (e.Source != ModoTabs || PlanIzquierda is null || InformeIzquierda is null) return;

        bool informe = ModoTabs.SelectedIndex == 1;
        PlanIzquierda.Visibility = informe ? Visibility.Collapsed : Visibility.Visible;
        InformeIzquierda.Visibility = informe ? Visibility.Visible : Visibility.Collapsed;
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

        SeleccionarMesActual();
    }

    private void SeleccionarMesActual()
    {
        MesCombo.SelectedValue = DateTime.Today.Month;
        AnioCombo.SelectedItem = DateTime.Today.Year;
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
        catch (Exception ex) 
        { 
            MostrarErrorIA(ex); 
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
        if (!ExistePlantilla(RutaPlantilla)) return;

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

        await GenerarPdfAsync(
            NombreSugerido(rutaHistoria, plan.EmisionPlan, "PlanReha"),
            GuardarWordCheckBox.IsChecked == true,
            rutaDocx => _generador.Generar(RutaPlantilla, rutaDocx, plan));
    }

    /// <summary>Pide dónde guardar, crea el Word (temporal o junto al PDF) y lo exporta a PDF.</summary>
    private async Task GenerarPdfAsync(string nombreSugerido, bool conservarWord, Action<string> crearDocx)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Guardar documento",
            Filter = "Documento PDF (*.pdf)|*.pdf",
            FileName = nombreSugerido + ".pdf",
            InitialDirectory = HistoriaSelector.Ruta is { } ruta ? Path.GetDirectoryName(ruta) : null
        };
        if (dialogo.ShowDialog() != true) return;

        string rutaPdf = dialogo.FileName;
        string rutaDocx = conservarWord
            ? Path.ChangeExtension(rutaPdf, ".docx")
            : Path.Combine(Path.GetTempPath(), $"NeuroDocs_{Guid.NewGuid():N}.docx");

        GenerarButton.IsEnabled = false;
        GenerarInformeButton.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
        MostrarAviso(TipoAviso.Informacion, "Generando el documento…");
        try
        {
            await Task.Run(() => crearDocx(rutaDocx));
            await _convertidorPdf.ConvertirAsync(rutaDocx, rutaPdf);

            MostrarAviso(TipoAviso.Exito, $"Documento guardado: {Path.GetFileName(rutaPdf)}",
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
            MostrarAviso(TipoAviso.Error, $"No se pudo generar el documento: {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
            ActualizarBotonesGenerar();

            if (!conservarWord)
            {
                try { File.Delete(rutaDocx); } catch (IOException) { }
            }
        }
    }

    private bool ExistePlantilla(string ruta)
    {
        if (File.Exists(ruta)) return true;

        MostrarAviso(TipoAviso.Error, $"No se encontró la plantilla en: {ruta}");
        return false;
    }

    private DatosPlan ConstruirPlan(DatosPaciente paciente, DatosOrden orden, Firma? firma) =>
        new(paciente, orden, EmisionPlan,
            new FuncionesEvaluadas(
                ListaTexto.ParsearItems(ConservadosTextBox.Text),
                ListaTexto.ParsearItems(AlteradosTextBox.Text)),
            firma);

    /// <summary>"OscarminMuñozJimenezECC.pdf" → "OscarminMuñozJimenezPlanRehaSeptiembre2026"</summary>
    /// <summary>"OscarminMuñozJimenezECC.pdf" + "EVM" → "OscarminMuñozJimenezEVMOctubre2026"</summary>
    private static string NombreSugerido(string rutaHistoria, DateOnly emision, string tipoDocumento)
    {
        string nombre = Path.GetFileNameWithoutExtension(rutaHistoria);
        if (nombre.EndsWith("ECC", StringComparison.OrdinalIgnoreCase)) nombre = nombre[..^3];

        return $"{nombre}{tipoDocumento}{FormatoFecha.NombreMes(emision.Month)}{emision.Year}";
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
    private void LimpiarButton_Click(object sender, RoutedEventArgs e)
    {
        bool hayListas = !string.IsNullOrWhiteSpace(ConservadosTextBox.Text)
              || !string.IsNullOrWhiteSpace(AlteradosTextBox.Text)
              || !string.IsNullOrWhiteSpace(CondicionTextBox.Text)
              || !string.IsNullOrWhiteSpace(PlanTextBox.Text);

        if (hayListas &&
            MessageBox.Show("Se quitarán todos los documentos y se borrarán las listas de Conservados y Alterados. ¿Continuar?",
                            "Limpiar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        // Al vaciar los selectores se disparan sus eventos: se ocultan el resumen y los avisos,
        // se desactiva Generar y se cierra la ventana de datos.
        HistoriaSelector.Limpiar();
        OrdenSelector.Limpiar();
        LimpiarDatosDelPacienteAnterior(); // informes para la IA + listas
        SeleccionarMesActual();
        OcultarAviso();
    }

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

        sb.AppendLine(plan.Orden.Tipo == TipoOrden.OrdenCobro ? "\nORDEN DE COBRO" : "\nSOLICITUD DE ORDEN DE SERVICIO");
        sb.AppendLine($"Sesiones mensuales: {plan.Orden.SesionesMensuales?.ToString() ?? NoEncontrado}");
        sb.AppendLine($"Meses de terapia: {plan.Orden.Meses?.ToString() ?? NoEncontrado}");
        if (plan.Orden.CantidadAutorizada is { } cantidad) sb.AppendLine($"Cantidad autorizada: {cantidad}");
        sb.AppendLine(plan.Orden.Cie10Codigo is not { } codigo
            ? $"CIE-10: {NoEncontrado}"
            : plan.Orden.Cie10Descripcion is { } descripcion ? $"CIE-10: {codigo} - {descripcion}" : $"CIE-10: {codigo}");

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
    // ───────────── Informe de evolución ─────────────

    private void Evaluacion_RutaCambiada(object? sender, EventArgs e) =>
        RedactarIAButton.IsEnabled = EvaluacionSelector.Ruta is not null;

    private void TipoInforme_Checked(object sender, RoutedEventArgs e)
    {
        if (_tipoRedactado is { } redactado && redactado != TipoSeleccionado)
        {
            MostrarAviso(TipoAviso.Advertencia,
                "Cambiaste el tipo de informe después de redactarlo. Vuelve a pulsar «Redactar con IA» para que el Plan actual corresponda (continuar o cerrar el proceso).");
        }
    }

    private async void RedactarIAButton_Click(object sender, RoutedEventArgs e)
    {
        if (EvaluacionSelector.Ruta is not { } ruta) return;

        bool hayTexto = !string.IsNullOrWhiteSpace(CondicionTextBox.Text) || !string.IsNullOrWhiteSpace(PlanTextBox.Text);
        if (hayTexto &&
            MessageBox.Show("Esto reemplazará la Condición actual y el Plan actual. ¿Continuar?", "Redactar con IA",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var tipo = TipoSeleccionado;
        RedactarIAButton.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
        MostrarAviso(TipoAviso.Informacion, "Leyendo la evaluación y consultando a Gemini…");
        try
        {
            string evoluciones = LectorEvaluacion.LeerTexto(ruta); // hilo de UI: el RTF lo exige y es instantáneo

            string? genero = _lectura?.Paciente.Campos.GetValueOrDefault(CamposPaciente.Genero);
            string? diagnostico = _lectura is { } l ? ResolverDiagnostico.Texto(l.Paciente, l.Orden) : null;

            var redactor = new RedactorInformeGemini(ConfiguracionGemini.Cargar());
            var texto = await redactor.RedactarAsync(evoluciones, tipo, genero,
                                                     string.IsNullOrWhiteSpace(diagnostico) ? null : diagnostico);

            CondicionTextBox.Text = texto.CondicionActual;
            PlanTextBox.Text = texto.PlanActual;
            _tipoRedactado = tipo;

            MostrarAviso(TipoAviso.Exito, "Informe redactado. Revisa la Condición actual y el Plan actual antes de generar.");
        }
        catch (Exception ex)
        {
            MostrarErrorIA(ex);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            RedactarIAButton.IsEnabled = EvaluacionSelector.Ruta is not null;
        }
    }

    private async void GenerarInformeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lectura is not { } lectura || HistoriaSelector.Ruta is not { } rutaHistoria) return;
        if (!ExistePlantilla(RutaPlantillaInforme)) return;

        string condicion = CondicionTextBox.Text.Trim();
        string planActual = PlanTextBox.Text.Trim();

        if ((condicion.Length == 0 || planActual.Length == 0) &&
            MessageBox.Show("La Condición actual o el Plan actual están vacíos. ¿Generar de todas formas?",
                            "Informe incompleto", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var informe = new DatosInforme(lectura.Paciente, lectura.Orden, EmisionPlan,
                                       DateOnly.FromDateTime(DateTime.Today), condicion, planActual);

        await GenerarPdfAsync(
            NombreSugerido(rutaHistoria, informe.EmisionPlan, "EVM"),
            GuardarWordInformeCheckBox.IsChecked == true,
            rutaDocx => _generadorInforme.Generar(RutaPlantillaInforme, rutaDocx, informe));
    }

    private void Informe_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        ActualizarConteosInforme();

    private void ActualizarConteosInforme()
    {
        if (CondicionConteo is null || PlanConteo is null) return; // TextChanged puede llegar antes de crear los contadores

        CondicionConteo.Text = DescribirPalabras(CondicionTextBox.Text);
        PlanConteo.Text = DescribirPalabras(PlanTextBox.Text);
    }

    private static string DescribirPalabras(string texto)
    {
        int cantidad = texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return cantidad == 1 ? "1 palabra" : $"{cantidad} palabras";
    }

    /// <summary>Mensaje de error común para las dos funciones de IA.</summary>
    private void MostrarErrorIA(Exception ex) => MostrarAviso(TipoAviso.Error, ex switch
    {
        HttpRequestException => $"No se pudo conectar con Gemini. Revisa la conexión a internet. ({ex.Message})",
        TaskCanceledException => "Gemini tardó demasiado en responder. Intenta de nuevo.",
        FileNotFoundException => ex.Message,
        IOException => $"No se pudo leer el archivo. Si está abierto en otro programa, ciérralo. ({ex.Message})",
        _ => ex.Message
    });

}