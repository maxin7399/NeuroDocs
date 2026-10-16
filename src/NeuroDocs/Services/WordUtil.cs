using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using NeuroDocs.Models;
using static NeuroDocs.Services.TextoUtil;

namespace NeuroDocs.Services;

/// <summary>Llenado de etiquetas "Campo: valor" en plantillas Word (plan e informe).</summary>
internal static class WordUtil
{
    public static void LlenarIdentificacion(Body body, DatosPaciente paciente)
    {
        foreach (var campo in CamposPaciente.Todos)
        {
            var parrafo = BuscarParrafo(body, CamposPaciente.Etiquetas[campo]);

            if (paciente.Campos.TryGetValue(campo, out var valor))
                EscribirValor(parrafo, valor, negrita: false);
            else if (CamposPaciente.Opcionales.Contains(campo))
                parrafo.Remove(); // si el dato no existe, la línea no aparece
            else
                EscribirValor(parrafo, "", negrita: false);
        }
    }

    public static void LlenarCampo(Body body, IReadOnlyList<string> etiquetas, string valor, bool negrita = false) =>
        EscribirValor(BuscarParrafo(body, etiquetas), valor, negrita);

    public static Paragraph BuscarParrafo(Body body, IReadOnlyList<string> etiquetas) =>
        body.Descendants<Paragraph>().FirstOrDefault(p => etiquetas.Any(e => EmpiezaConEtiqueta(p.InnerText, e)))
        ?? throw new InvalidOperationException($"La plantilla no contiene la etiqueta \"{etiquetas[0]}:\".");

    /// <summary>Conserva la etiqueta con su formato, borra lo que haya después de los dos puntos y agrega el valor.</summary>
    public static void EscribirValor(Paragraph parrafo, string valor, bool negrita)
    {
        var runEtiqueta = parrafo.Elements<Run>().FirstOrDefault(r => r.InnerText.Contains(':'))
            ?? throw new InvalidOperationException($"Formato inesperado en la línea \"{parrafo.InnerText}\".");

        foreach (var siguiente in runEtiqueta.ElementsAfter().ToList())
            siguiente.Remove();

        string textoEtiqueta = runEtiqueta.InnerText;
        textoEtiqueta = textoEtiqueta[..(textoEtiqueta.IndexOf(':') + 1)] + " ";
        runEtiqueta.RemoveAllChildren<Text>();
        runEtiqueta.AppendChild(new Text(textoEtiqueta) { Space = SpaceProcessingModeValues.Preserve });

        if (string.IsNullOrEmpty(valor)) return;

        var propiedades = runEtiqueta.RunProperties?.CloneNode(true) as RunProperties ?? new RunProperties();
        propiedades.Bold = negrita ? new Bold() : null;
        propiedades.BoldComplexScript = negrita ? new BoldComplexScript() : null;

        runEtiqueta.InsertAfterSelf(new Run(propiedades, new Text(valor) { Space = SpaceProcessingModeValues.Preserve }));
    }

    public static string FormatearTiempo(DatosOrden orden) =>
        orden is { SesionesMensuales: { } sesiones, Meses: { } meses }
            ? $"{sesiones} SESIONES MENSUALES POR {meses} MESES"
            : "";
}