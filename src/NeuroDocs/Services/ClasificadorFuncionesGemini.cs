using System.IO;
using NeuroDocs.Models;

namespace NeuroDocs.Services;

/// <summary>Conservados y alterados a partir de los PDFs de "Información para la IA".</summary>
public sealed class ClasificadorFuncionesGemini
{
    private const string ArchivoInstrucciones = "InstruccionesFunciones.txt";
    private const long LimiteBytes = 20L * 1024 * 1024;

    private static readonly object Esquema = new
    {
        type = "OBJECT",
        properties = new
        {
            conservados = new { type = "ARRAY", items = new { type = "STRING" } },
            alterados = new { type = "ARRAY", items = new { type = "STRING" } }
        },
        required = new[] { "conservados", "alterados" }
    };

    private readonly ClienteGemini _cliente;

    public ClasificadorFuncionesGemini(ConfiguracionGemini config) => _cliente = new ClienteGemini(config);

    public async Task<FuncionesEvaluadas> ClasificarAsync(IReadOnlyList<string> rutasPdf, CancellationToken ct = default)
    {
        if (rutasPdf.Count == 0)
            throw new ArgumentException("Se necesita al menos un documento.", nameof(rutasPdf));

        // Los PDFs van primero y la instrucción al final: es el orden que mejor funciona con Gemini 3.
        var partes = new List<object>();
        long totalBytes = 0;
        foreach (var ruta in rutasPdf)
        {
            byte[] bytes = await File.ReadAllBytesAsync(ruta, ct);
            totalBytes += bytes.Length;
            partes.Add(ClienteGemini.Pdf(bytes));
        }

        if (totalBytes > LimiteBytes)
            throw new InvalidOperationException("Los documentos superan el tamaño máximo permitido (20 MB en total).");

        partes.Add(ClienteGemini.Texto(rutasPdf.Count == 1
            ? "Clasifica las funciones cognitivas del informe adjunto."
            : "Clasifica las funciones cognitivas de los dos informes adjuntos en una sola clasificación."));

        var respuesta = await _cliente.GenerarAsync<RespuestaFunciones>(
            ConfiguracionGemini.LeerInstrucciones(ArchivoInstrucciones), partes, Esquema, ct);

        return new FuncionesEvaluadas(Limpiar(respuesta.Conservados), Limpiar(respuesta.Alterados));
    }

    private static List<string> Limpiar(List<string>? items) =>
        (items ?? []).Select(i => i.Trim()).Where(i => i.Length > 0).ToList();

    private sealed record RespuestaFunciones(List<string>? Conservados, List<string>? Alterados);
}