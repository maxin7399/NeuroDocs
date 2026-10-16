using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NeuroDocs.Services;

/// <summary>Llamada a Gemini con respuesta JSON obligatoria, reintentos y mensajes de error claros.</summary>
public sealed class ClienteGemini
{
    private const string UrlBase = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const int MaxIntentos = 3;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    private static readonly HashSet<HttpStatusCode> EstadosTemporales =
    [
        HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout
    ];

    private readonly ConfiguracionGemini _config;

    public ClienteGemini(ConfiguracionGemini config) => _config = config;

    public static object Texto(string texto) => new { text = texto };

    public static object Pdf(byte[] bytes) =>
        new { inlineData = new { mimeType = "application/pdf", data = Convert.ToBase64String(bytes) } };

    /// <summary>Envía las partes y deserializa la respuesta, que Gemini debe ajustar al esquema.</summary>
    public async Task<T> GenerarAsync<T>(string instrucciones, IReadOnlyList<object> partes, object esquema,
                                         CancellationToken ct = default)
    {
        var solicitud = new
        {
            systemInstruction = new { parts = new[] { new { text = instrucciones } } },
            contents = new[] { new { role = "user", parts = partes } },
            generationConfig = new { responseMimeType = "application/json", responseSchema = esquema }
        };

        string json = ExtraerTexto(await EnviarConReintentosAsync(solicitud, ct));
        try
        {
            return JsonSerializer.Deserialize<T>(json, OpcionesJson)
                ?? throw new InvalidOperationException("Gemini devolvió una respuesta vacía.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("La respuesta de Gemini llegó incompleta o con formato inválido. Intenta de nuevo.");
        }
    }

    /// <summary>Comprueba la clave y el modelo sin gastar una generación.</summary>
    public static async Task VerificarAsync(string apiKey, string modelo, CancellationToken ct = default)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, $"{UrlBase}{Uri.EscapeDataString(modelo)}");
        mensaje.Headers.Add("x-goog-api-key", apiKey);

        using var respuesta = await Http.SendAsync(mensaje, ct);
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException(DescribirError(respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync(ct)));
    }

    private async Task<string> EnviarConReintentosAsync(object solicitud, CancellationToken ct)
    {
        for (int intento = 1; ; intento++)
        {
            using var mensaje = CrearMensaje(solicitud); // un HttpRequestMessage no se puede reenviar
            using var respuesta = await Http.SendAsync(mensaje, ct);
            string cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

            if (respuesta.IsSuccessStatusCode) return cuerpo;

            if (!EstadosTemporales.Contains(respuesta.StatusCode) || intento == MaxIntentos)
                throw new InvalidOperationException(DescribirError(respuesta.StatusCode, cuerpo));

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
            Content = JsonContent.Create(solicitud, solicitud.GetType())
        };
        mensaje.Headers.Add("x-goog-api-key", _config.ApiKey);
        return mensaje;
    }

    private static string ExtraerTexto(string cuerpo)
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

        // Los modelos con razonamiento pueden incluir partes de "pensamiento": solo interesa la respuesta.
        return string.Concat(partes
            .Where(p => p?["thought"]?.GetValue<bool>() != true)
            .Select(p => p?["text"]?.GetValue<string>() ?? ""));
    }

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
            HttpStatusCode.ServiceUnavailable =>
                "Gemini está saturado en este momento (se intentó varias veces). Espera unos minutos e intenta de nuevo.",
            HttpStatusCode.NotFound =>
                $"El modelo configurado no existe o no está disponible. Revísalo en Configuración.\n\n{detalle}",
            _ => $"Gemini respondió con error {(int)estado}:\n{detalle ?? cuerpo}"
        };
    }
}