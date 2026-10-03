using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using NeuroDocs.Models;
using static NeuroDocs.Services.TextoUtil;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace NeuroDocs.Services;

public sealed class GeneradorPlanWord
{
    public void Generar(string rutaPlantilla, string rutaSalida, DatosPlan plan)
    {
        // Se trabaja sobre una copia: la plantilla original nunca se modifica.
        File.Copy(rutaPlantilla, rutaSalida, overwrite: true);

        using var documento = WordprocessingDocument.Open(rutaSalida, isEditable: true);
        var main = documento.MainDocumentPart
            ?? throw new InvalidOperationException("La plantilla no tiene contenido.");
        var body = main.Document.Body
            ?? throw new InvalidOperationException("La plantilla no tiene cuerpo.");

        LlenarIdentificacion(body, plan.Paciente);

        LlenarCampo(body, ["Emisión del Plan"], FormatoFecha.MesAnio(plan.EmisionPlan), negrita: true);
        LlenarCampo(body, ["Diagnóstico"], string.Join("; ", plan.Paciente.Diagnosticos.Select(QuitarPuntoFinal)));
        LlenarCampo(body, ["CIE-10"], plan.Orden.Cie10Codigo ?? "", negrita: true);
        LlenarCampo(body, ["Tiempo de terapia solicitado"], FormatearTiempo(plan.Orden));

        LlenarFunciones(main, body, plan.Funciones);

        var materiales = EliminarFirmasPredeterminadas(body);
        if (plan.Firma is { } firma)
        {
            InsertarFirma(main, materiales, firma);
        }
        EliminarImagenesHuerfanas(main);

        main.Document.Save();
    }
    
    // ───────────── Firma del profesional ─────────────

    private const long EmuPorPunto = 12700; // unidad interna de Word para dibujos
    private const double EscalaFirma = 0.85; // 1.0 = mismo tamaño que en el ECC

    private static void InsertarFirma(MainDocumentPart main, Paragraph materiales, Firma firma)
    {
        var parteImagen = main.AddImagePart(firma.Formato == FormatoImagen.Png ? ImagePartType.Png : ImagePartType.Jpeg);
        using (var stream = new MemoryStream(firma.Imagen))
        {
            parteImagen.FeedData(stream);
        }
        string relacionId = main.GetIdOfPart(parteImagen);

        // Imagen: alineada a la derecha, con espacio respecto a "Materiales" (720 twips = 36 pt).
        var parrafoImagen = CrearParrafoDerecha(materiales, espacioAntesTwips: 720);
        parrafoImagen.Append(new Run(CrearDibujo(
            relacionId,
            (long)(firma.AnchoPt * EscalaFirma * EmuPorPunto),
            (long)(firma.AltoPt * EscalaFirma * EmuPorPunto),
            SiguienteIdDibujo(main))));

        OpenXmlElement ultimo = materiales.InsertAfterSelf(parrafoImagen);

        // Nombre, título y registro: misma fuente y tamaño que "Materiales:", en negrita.
        var formatoBase = materiales.Elements<Run>().FirstOrDefault()?.RunProperties;
        foreach (var linea in firma.Lineas)
        {
            var propiedades = formatoBase?.CloneNode(true) as RunProperties ?? new RunProperties();
            propiedades.Bold = new Bold();
            propiedades.BoldComplexScript = new BoldComplexScript();

            var parrafo = CrearParrafoDerecha(materiales, espacioAntesTwips: 0);
            parrafo.Append(new Run(propiedades, new Text(linea) { Space = SpaceProcessingModeValues.Preserve }));
            ultimo = ultimo.InsertAfterSelf(parrafo);
        }
    }

    /// <summary>Párrafo alineado a la derecha que hereda el estilo del párrafo de referencia.</summary>
    private static Paragraph CrearParrafoDerecha(Paragraph referencia, int espacioAntesTwips)
    {
        var pPr = referencia.ParagraphProperties?.CloneNode(true) as ParagraphProperties ?? new ParagraphProperties();
        pPr.NumberingProperties = null;
        pPr.Indentation = null;
        pPr.Justification = new Justification { Val = JustificationValues.Right };
        pPr.SpacingBetweenLines = new SpacingBetweenLines
        {
            Before = espacioAntesTwips.ToString(),
            After = "0",
            BeforeAutoSpacing = false,
            AfterAutoSpacing = false
        };
        return new Paragraph(pPr);
    }

    /// <summary>Word exige que cada dibujo tenga un id único en el documento (incluidos encabezado y pie).</summary>
    private static uint SiguienteIdDibujo(MainDocumentPart main)
    {
        var ids = main.Document.Descendants<DW.DocProperties>()
            .Concat(main.HeaderParts.SelectMany(h => h.Header.Descendants<DW.DocProperties>()))
            .Concat(main.FooterParts.SelectMany(f => f.Footer.Descendants<DW.DocProperties>()))
            .Select(d => d.Id?.Value ?? 0u);

        return ids.DefaultIfEmpty(0u).Max() + 1;
    }

    /// <summary>Imagen "en línea con el texto" (inline): se mueve con el párrafo, como en el plan final.</summary>
    private static Drawing CrearDibujo(string relacionId, long ancho, long alto, uint id) =>
        new(
            new DW.Inline(
                new DW.Extent { Cx = ancho, Cy = alto },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = id, Name = $"Firma {id}" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "firma" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relacionId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = ancho, Cy = alto }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            )
            { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

    private static void LlenarIdentificacion(Body body, DatosPaciente paciente)
    {
        foreach (var campo in CamposPaciente.Todos)
        {
            var parrafo = BuscarParrafo(body, CamposPaciente.Etiquetas[campo]);

            if (paciente.Campos.TryGetValue(campo, out var valor))
            {
                EscribirValor(parrafo, valor, negrita: false);
            }
            else if (CamposPaciente.Opcionales.Contains(campo))
            {
                parrafo.Remove(); // el plan final no muestra la línea si el dato no existe
            }
            else
            {
                EscribirValor(parrafo, "", negrita: false);
            }
        }
    }

    private static void LlenarCampo(Body body, IReadOnlyList<string> etiquetas, string valor, bool negrita = false) =>
        EscribirValor(BuscarParrafo(body, etiquetas), valor, negrita);

    private static Paragraph BuscarParrafo(Body body, IReadOnlyList<string> etiquetas) =>
        body.Descendants<Paragraph>().FirstOrDefault(p => etiquetas.Any(e => EmpiezaConEtiqueta(p.InnerText, e)))
        ?? throw new InvalidOperationException($"La plantilla no contiene la etiqueta \"{etiquetas[0]}:\".");

    /// <summary>
    /// Deja la etiqueta tal cual (con su formato), elimina lo que haya después de los dos puntos
    /// (p. ej. el "." suelto de "Ocupación:." o el "X" de los meses) y agrega el valor.
    /// </summary>
    private static void EscribirValor(Paragraph parrafo, string valor, bool negrita)
    {
        var runEtiqueta = parrafo.Elements<Run>().FirstOrDefault(r => r.InnerText.Contains(':'))
            ?? throw new InvalidOperationException($"Formato inesperado en la línea \"{parrafo.InnerText}\".");

        foreach (var siguiente in runEtiqueta.ElementsAfter().ToList())
        {
            siguiente.Remove();
        }

        string textoEtiqueta = runEtiqueta.InnerText;
        textoEtiqueta = textoEtiqueta[..(textoEtiqueta.IndexOf(':') + 1)] + " ";
        runEtiqueta.RemoveAllChildren<Text>();
        runEtiqueta.AppendChild(new Text(textoEtiqueta) { Space = SpaceProcessingModeValues.Preserve });

        if (string.IsNullOrEmpty(valor)) return;

        // El valor hereda fuente y tamaño de la etiqueta; solo cambia la negrita.
        var propiedades = runEtiqueta.RunProperties?.CloneNode(true) as RunProperties ?? new RunProperties();
        propiedades.Bold = negrita ? new Bold() : null;
        propiedades.BoldComplexScript = negrita ? new BoldComplexScript() : null;

        runEtiqueta.InsertAfterSelf(new Run(propiedades, new Text(valor) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static string FormatearTiempo(DatosOrden orden) =>
        orden is { SesionesMensuales: { } sesiones, Meses: { } meses }
            ? $"{sesiones} SESIONES MENSUALES POR {meses} MESES"
            : "";

    private static string QuitarPuntoFinal(string texto) => texto.TrimEnd().TrimEnd('.');

    // ───────────── Tabla de funciones ─────────────

    private static void LlenarFunciones(MainDocumentPart main, Body body, FuncionesEvaluadas funciones)
    {
        var tabla = body.Descendants<Table>()
            .FirstOrDefault(t => Normalizar(t.InnerText).Contains("aspectos de las funciones evaluadas"))
            ?? throw new InvalidOperationException("La plantilla no contiene la tabla de funciones evaluadas.");

        var celdas = tabla.Elements<TableRow>().Last().Elements<TableCell>().ToList();
        if (celdas.Count < 2)
            throw new InvalidOperationException("La tabla de funciones no tiene las columnas esperadas.");

        int numId = CrearNumeracionVinetas(main);
        LlenarCeldaConVinetas(celdas[0], funciones.Conservados, numId);
        LlenarCeldaConVinetas(celdas[1], funciones.Alterados, numId);
    }

    private static void LlenarCeldaConVinetas(TableCell celda, IReadOnlyList<string> items, int numId)
    {
        if (items.Count == 0) return;

        var originales = celda.Elements<Paragraph>().ToList();
        var modelo = originales[0];

        foreach (var item in items)
        {
            var parrafo = (Paragraph)modelo.CloneNode(true);
            foreach (var hijo in parrafo.ChildElements.Where(h => h is not ParagraphProperties).ToList())
            {
                hijo.Remove();
            }

            var pPr = parrafo.ParagraphProperties ??= new ParagraphProperties();
            pPr.Indentation = null; // la sangría la define la viñeta
            pPr.SpacingBetweenLines = new SpacingBetweenLines
            {
                Before = "0",
                After = "0",
                BeforeAutoSpacing = false,
                AfterAutoSpacing = false
            };
            pPr.NumberingProperties = new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = numId });

            // El texto usa el mismo formato que la marca de párrafo de la celda (fuente y tamaño de la plantilla).
            var propiedades = new RunProperties();
            if (pPr.ParagraphMarkRunProperties is { } marca)
            {
                foreach (var p in marca.ChildElements) propiedades.Append(p.CloneNode(true));
            }
            propiedades.Bold = null;
            propiedades.BoldComplexScript = null;

            parrafo.Append(new Run(propiedades, new Text(item) { Space = SpaceProcessingModeValues.Preserve }));
            celda.Append(parrafo);
        }

        foreach (var original in originales) original.Remove();
    }

    /// <summary>Registra una lista con viñeta "•" (la misma de Word) y devuelve su numId.</summary>
    private static int CrearNumeracionVinetas(MainDocumentPart main)
    {
        var parte = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
        var numeracion = parte.Numbering ??= new Numbering();

        int abstractId = numeracion.Elements<AbstractNum>()
            .Select(a => a.AbstractNumberId?.Value ?? 0).DefaultIfEmpty(-1).Max() + 1;
        int numId = numeracion.Elements<NumberingInstance>()
            .Select(n => n.NumberID?.Value ?? 0).DefaultIfEmpty(0).Max() + 1;

        var definicion = new AbstractNum(
            new MultiLevelType { Val = MultiLevelValues.SingleLevel },
            new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = NumberFormatValues.Bullet },
                new LevelText { Val = "\uF0B7" },
                new LevelJustification { Val = LevelJustificationValues.Left },
                new PreviousParagraphProperties(new Indentation { Left = "720", Hanging = "360" }),
                new NumberingSymbolRunProperties(new RunFonts { Ascii = "Symbol", HighAnsi = "Symbol", Hint = FontTypeHintValues.Default })
            )
            { LevelIndex = 0 }
        )
        { AbstractNumberId = abstractId };

        // El esquema de Word exige que las definiciones (abstractNum) vayan antes de las instancias (num).
        if (numeracion.Elements<AbstractNum>().LastOrDefault() is { } ultima)
            ultima.InsertAfterSelf(definicion);
        else
            numeracion.PrependChild(definicion);

        numeracion.Append(new NumberingInstance(new AbstractNumId { Val = abstractId }) { NumberID = numId });
        return numId;
    }

    // ───────────── Firmas ─────────────
    /// <summary>Elimina los bloques de firma de la plantilla y devuelve el párrafo "Materiales" (punto de inserción).</summary>
    private static Paragraph EliminarFirmasPredeterminadas(Body body)
    {
        var materiales = body.Elements<Paragraph>().FirstOrDefault(p => EmpiezaConEtiqueta(p.InnerText, "Materiales"))
            ?? throw new InvalidOperationException("La plantilla no contiene el párrafo \"Materiales:\".");

        foreach (var elemento in materiales.ElementsAfter().Where(e => e is not SectionProperties).ToList())
        {
            elemento.Remove();
        }

        return materiales;
    }

    /// <summary>Quita del archivo las imágenes de firma que ya no se usan (evita un .docx inflado).</summary>
    private static void EliminarImagenesHuerfanas(MainDocumentPart main)
    {
        var usadas = main.Document.Descendants<A.Blip>()
            .Select(b => b.Embed?.Value)
            .OfType<string>()
            .ToHashSet();

        foreach (var imagen in main.ImageParts.ToList())
        {
            if (!usadas.Contains(main.GetIdOfPart(imagen))) main.DeletePart(imagen);
        }
    }
}