using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using NeuroDocs.Models;
using static NeuroDocs.Services.TextoUtil;
using static NeuroDocs.Services.WordUtil;

namespace NeuroDocs.Services;

/// <summary>Informe de evolución: identificación + Condición Actual + Plan Actual. La firma ya viene en la plantilla.</summary>
public sealed class GeneradorInformeWord
{
    public void Generar(string rutaPlantilla, string rutaSalida, DatosInforme informe)
    {
        File.Copy(rutaPlantilla, rutaSalida, overwrite: true);

        using var documento = WordprocessingDocument.Open(rutaSalida, isEditable: true);
        var main = documento.MainDocumentPart ?? throw new InvalidOperationException("La plantilla no tiene contenido.");
        var body = main.Document.Body ?? throw new InvalidOperationException("La plantilla no tiene cuerpo.");

        LlenarIdentificacion(body, informe.Paciente);

        LlenarCampo(body, ["Diagnóstico"], ResolverDiagnostico.Texto(informe.Paciente, informe.Orden), negrita: true);
        LlenarCampo(body, ["CIE-10"], informe.Orden.Cie10Codigo ?? "", negrita: true);
        LlenarCampo(body, ["Tiempo de terapia solicitado"], FormatearTiempo(informe.Orden));
        LlenarCampo(body, ["Fecha elaboración de Informe", "Fecha de elaboración del informe"],
                    FormatoFecha.Larga(informe.FechaElaboracion));

        InsertarSeccion(body, "Condición Actual", informe.CondicionActual);
        InsertarSeccion(body, "Plan Actual", informe.PlanActual);

        main.Document.Save();
    }

    /// <summary>
    /// Inserta el texto debajo del título, un párrafo justificado por bloque. El párrafo vacío que sigue al título
    /// en la plantilla se conserva como separador y define el formato del texto (tamaño 7 pt, como los informes aprobados).
    /// </summary>
    private static void InsertarSeccion(Body body, string titulo, string texto)
    {
        string tituloNorm = Normalizar(titulo);
        var encabezado = body.Elements<Paragraph>().FirstOrDefault(p => Normalizar(p.InnerText) == tituloNorm)
            ?? throw new InvalidOperationException($"La plantilla no contiene el título \"{titulo}\".");

        var modelo = encabezado.NextSibling<Paragraph>() is { } siguiente && string.IsNullOrWhiteSpace(siguiente.InnerText)
            ? siguiente
            : encabezado;

        var propiedadesTexto = new RunProperties();
        if (modelo.ParagraphProperties?.ParagraphMarkRunProperties is { } marca)
        {
            foreach (var propiedad in marca.ChildElements) propiedadesTexto.Append(propiedad.CloneNode(true));
        }
        propiedadesTexto.Bold = null;
        propiedadesTexto.BoldComplexScript = null;

        OpenXmlElement anterior = encabezado;
        foreach (var bloque in texto.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var pPr = modelo.ParagraphProperties?.CloneNode(true) as ParagraphProperties ?? new ParagraphProperties();
            pPr.Justification = new Justification { Val = JustificationValues.Both };

            var parrafo = new Paragraph(pPr,
                new Run((RunProperties)propiedadesTexto.CloneNode(true),
                        new Text(bloque) { Space = SpaceProcessingModeValues.Preserve }));

            anterior = anterior.InsertAfterSelf(parrafo);
        }
    }
}