using System.IO;

namespace NeuroDocs.Services;

/// <summary>
/// Clase y no record a propósito: el ToString() automático de un record imprimiría la clave
/// si alguna vez se registra en un log.
/// </summary>
public sealed class ConfiguracionGemini
{
    private const string ModeloPorDefecto = "gemini-3.8-flash";

    private static readonly string RutaInstrucciones =
        Path.Combine(AppContext.BaseDirectory, "IA", "InstruccionesFunciones.txt");

    public string ApiKey { get; }
    public string Modelo { get; }
    public string Instrucciones { get; }

    private ConfiguracionGemini(string apiKey, string modelo, string instrucciones)
    {
        ApiKey = apiKey;
        Modelo = modelo;
        Instrucciones = instrucciones;
    }

    /// <summary>Se carga en cada uso: así se pueden editar las instrucciones sin reiniciar la app.</summary>
    public static ConfiguracionGemini Cargar()
    {
        string apiKey = LeerVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException(
                "No se encontró la clave de Gemini. Configura la variable de entorno GEMINI_API_KEY.");

        if (!File.Exists(RutaInstrucciones))
            throw new FileNotFoundException($"No se encontró el archivo de instrucciones:\n{RutaInstrucciones}");

        return new ConfiguracionGemini(
            apiKey,
            LeerVariable("GEMINI_MODEL") ?? ModeloPorDefecto,
            File.ReadAllText(RutaInstrucciones));
    }

    // También se lee del perfil de usuario: permite usar una variable creada con setx sin reiniciar Visual Studio.
    private static string? LeerVariable(string nombre) =>
        NoVacia(Environment.GetEnvironmentVariable(nombre))
        ?? NoVacia(Environment.GetEnvironmentVariable(nombre, EnvironmentVariableTarget.User));

    private static string? NoVacia(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}