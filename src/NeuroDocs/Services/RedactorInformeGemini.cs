using NeuroDocs.Models;

namespace NeuroDocs.Services;

/// <summary>Redacta la Condición Actual y el Plan Actual a partir de las evoluciones del profesional.</summary>
public sealed class RedactorInformeGemini
{
    private const string ArchivoInstrucciones = "InstruccionesInforme.txt";

    private static readonly object Esquema = new
    {
        type = "OBJECT",
        properties = new
        {
            condicionActual = new { type = "STRING" },
            planActual = new { type = "STRING" }
        },
        required = new[] { "condicionActual", "planActual" }
    };

    private readonly ClienteGemini _cliente;

    public RedactorInformeGemini(ConfiguracionGemini config) => _cliente = new ClienteGemini(config);

    public async Task<TextoInforme> RedactarAsync(string evoluciones, TipoInforme tipo, string? genero, string? diagnostico,
                                                  CancellationToken ct = default)
    {
        string tipoTexto = tipo == TipoInforme.Final
            ? "FINAL (se cierra el proceso y se da de alta)"
            : "MENSUAL (el paciente continúa el proceso)";

        string mensaje = $"""
            TIPO DE INFORME: {tipoTexto}
            GÉNERO: {genero ?? "no indicado"}
            DIAGNÓSTICO: {diagnostico ?? "no indicado"}

            EVOLUCIONES:
            {evoluciones}
            """;

        var respuesta = await _cliente.GenerarAsync<RespuestaInforme>(
            ConfiguracionGemini.LeerInstrucciones(ArchivoInstrucciones), [ClienteGemini.Texto(mensaje)], Esquema, ct);

        string condicion = respuesta.CondicionActual?.Trim() ?? "";
        string plan = respuesta.PlanActual?.Trim() ?? "";

        return condicion.Length > 0 && plan.Length > 0
            ? new TextoInforme(condicion, plan)
            : throw new InvalidOperationException("Gemini devolvió un informe incompleto. Intenta de nuevo.");
    }

    private sealed record RespuestaInforme(string? CondicionActual, string? PlanActual);
}