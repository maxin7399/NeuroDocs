using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeuroDocs.Models;
using System.IO;

namespace NeuroDocs.Services;

/// <summary>Pide a Gemini que clasifique las funciones de la Descripción en conservados y alterados.</summary>
public sealed class ClasificadorFuncionesGemini
{
    private const string UrlBase = "https://generativelanguage.googleapis.com/v1beta/models/";

    // Una sola instancia para toda la app: crear HttpClient por llamada agota los sockets.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private const int MaxIntentos = 3;

    // Errores del lado de Google que suelen resolverse solos en segundos.
    private static readonly HashSet<HttpStatusCode> EstadosTemporales =
    [
        HttpStatusCode.InternalServerError,  // 500
    HttpStatusCode.BadGateway,           // 502
    HttpStatusCode.ServiceUnavailable,   // 503
    HttpStatusCode.GatewayTimeout        // 504
    ];

    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };
    private const long LimiteBytes = 20L * 1024 * 1024;

    private readonly ConfiguracionGemini _config;

    public ClasificadorFuncionesGemini(ConfiguracionGemini config) => _config = config;

    /// <summary>Comprueba que la clave sea válida y que el modelo exista, sin gastar una clasificación.</summary>
    public static async Task VerificarAsync(string apiKey, string modelo, CancellationToken ct = default)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, $"{UrlBase}{Uri.EscapeDataString(modelo)}");
        mensaje.Headers.Add("x-goog-api-key", apiKey);

        using var respuesta = await Http.SendAsync(mensaje, ct);
        if (!respuesta.IsSuccessStatusCode)
        {
            string cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(DescribirError(respuesta.StatusCode, cuerpo));
        }
    }

    public async Task<FuncionesEvaluadas> ClasificarAsync(IReadOnlyList<string> rutasPdf, CancellationToken ct = default)
    {
        if (rutasPdf.Count == 0)
            throw new ArgumentException("Se necesita al menos un informe.", nameof(rutasPdf));

        // Los PDFs van primero y la instrucción al final: es el orden que mejor funciona con Gemini 3.
        var partes = new List<object>();
        long totalBytes = 0;

        foreach (var ruta in rutasPdf)
        {
            byte[] bytes = await File.ReadAllBytesAsync(ruta, ct);
            totalBytes += bytes.Length;
            partes.Add(new { inlineData = new { mimeType = "application/pdf", data = Convert.ToBase64String(bytes) } });
        }

        if (totalBytes > LimiteBytes)
            throw new InvalidOperationException("Los informes superan el tamaño máximo permitido (20 MB en total).");

        partes.Add(new
        {
            text = rutasPdf.Count == 1
                ? "Clasifica las funciones cognitivas del informe adjunto."
                : "Clasifica las funciones cognitivas de los dos informes adjuntos en una sola clasificación."
        });

        var solicitud = new
        {
            systemInstruction = new { parts = new[] { new { text = _config.Instrucciones } } },
            contents = new[] { new { role = "user", parts = partes } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        conservados = new { type = "ARRAY", items = new { type = "STRING" } },
                        alterados = new { type = "ARRAY", items = new { type = "STRING" } }
                    },
                    required = new[] { "conservados", "alterados" }
                }
            }
        };

        string cuerpo = await EnviarConReintentosAsync(solicitud, ct);
        return Interpretar(cuerpo);
    }
    /// <summary>
    /// Envía la solicitud y reintenta los errores temporales con espera creciente (≈2 s, ≈4 s).
    /// Respeta el encabezado Retry-After si Google indica cuánto esperar.
    /// </summary>
    private async Task<string> EnviarConReintentosAsync(object solicitud, CancellationToken ct)
    {
        for (int intento = 1; ; intento++)
        {
            // Un HttpRequestMessage no se puede reenviar: se crea uno nuevo en cada intento.
            using var mensaje = CrearMensaje(solicitud);
            using var respuesta = await Http.SendAsync(mensaje, ct);
            string cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

            if (respuesta.IsSuccessStatusCode) return cuerpo;

            if (!EstadosTemporales.Contains(respuesta.StatusCode) || intento == MaxIntentos)
                throw new InvalidOperationException(DescribirError(respuesta.StatusCode, cuerpo));

            // El componente aleatorio evita que muchos clientes reintenten exactamente al mismo tiempo.
            var espera = respuesta.Headers.RetryAfter?.Delta
                ?? TimeSpan.FromSeconds(Math.Pow(2, intento)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));

            await Task.Delay(espera, ct);
        }
    }

    private HttpRequestMessage CrearMensaje(object solicitud)
    {
        var mensaje = new HttpRequestMessage(
            HttpMethod.Post, $"{UrlBase}{Uri.EscapeDataString(_config.Modelo)}:generateContent")
        {
            // Se pasa el tipo real: la solicitud es un tipo anónimo guardado en una variable object.
            Content = JsonContent.Create(solicitud, solicitud.GetType())
        };
        mensaje.Headers.Add("x-goog-api-key", _config.ApiKey);
        return mensaje;
    }
    private static FuncionesEvaluadas Interpretar(string cuerpo)
    {
        var raiz = JsonNode.Parse(cuerpo);
        var partes = raiz?["candidates"]?[0]?["content"]?["parts"]?.AsArray();

        if (partes is null)
        {
            string? bloqueo = raiz?["promptFeedback"]?["blockReason"]?.GetValue<string>();
            throw new InvalidOperationException(bloqueo is null
                ? "Gemini no devolvió ninguna respuesta."
                : $"Gemini bloqueó la solicitud ({bloqueo}).");
        }

        // Los modelos con "razonamiento" pueden incluir partes de pensamiento; solo interesa la respuesta.
        string json = string.Concat(partes
            .Where(p => p?["thought"]?.GetValue<bool>() != true)
            .Select(p => p?["text"]?.GetValue<string>() ?? ""));

        try
        {
            var datos = JsonSerializer.Deserialize<RespuestaFunciones>(json, OpcionesJson)
                ?? throw new InvalidOperationException("Gemini devolvió una respuesta vacía.");

            return new FuncionesEvaluadas(Limpiar(datos.Conservados), Limpiar(datos.Alterados));
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("La respuesta de Gemini llegó incompleta o con formato inválido. Intenta de nuevo.");
        }
    }

    private static List<string> Limpiar(List<string>? items) =>
        (items ?? []).Select(i => i.Trim()).Where(i => i.Length > 0).ToList();

    private static string DescribirError(HttpStatusCode estado, string cuerpo)
    {
        string? detalle = null;
        try { detalle = JsonNode.Parse(cuerpo)?["error"]?["message"]?.GetValue<string>(); }
        catch (JsonException) { }

        return estado switch
        {
            HttpStatusCode.TooManyRequests when detalle?.Contains("prepayment", StringComparison.OrdinalIgnoreCase) == true =>
            "El saldo prepago de la API de Gemini se agotó. Recarga créditos en Google AI Studio (Facturación) e intenta de nuevo.",
            HttpStatusCode.TooManyRequests =>
                $"Se alcanzó el límite de solicitudes de Gemini. Espera un momento e intenta de nuevo.\n\n{detalle}",
            HttpStatusCode.NotFound =>
                $"El modelo configurado no existe o no está disponible. Revisa GEMINI_MODEL.\n\n{detalle}",
            HttpStatusCode.ServiceUnavailable =>
                "Gemini está saturado en este momento (se intentó varias veces). Espera unos minutos e intenta de nuevo.",
            _ => $"Gemini respondió con error {(int)estado}:\n{detalle ?? cuerpo}",
        };
    }

    private sealed record RespuestaFunciones(List<string>? Conservados, List<string>? Alterados);
}