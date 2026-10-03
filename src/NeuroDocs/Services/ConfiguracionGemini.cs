using System.IO;

namespace NeuroDocs.Services;

/// <summary>
/// Clase y no record a propósito: el ToString() automático de un record imprimiría la clave
/// si alguna vez se registra en un log.
/// </summary>
public sealed class ConfiguracionGemini
{
    public const string ModeloPorDefecto = "gemini-3.8-flash";
    /// <summary>Opciones del selector. Se puede escribir cualquier otro nombre si Google cambia de modelos.</summary>
    public static readonly IReadOnlyList<string> ModelosSugeridos =
        ["gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.5-flash"];

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

    /// <summary>
    /// Prioridad: lo guardado en la ventana de Configuración y, como respaldo,
    /// las variables de entorno (útiles en desarrollo).
    /// </summary>
    public static ConfiguracionGemini Cargar()
    {
        var ajustes = AjustesUsuario.Cargar();

        string apiKey = ajustes.ObtenerApiKey()
            ?? LeerVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException(
                "No hay una clave de Gemini configurada. Ingrésala en Configuración.");

        if (!File.Exists(RutaInstrucciones))
            throw new FileNotFoundException($"No se encontró el archivo de instrucciones:\n{RutaInstrucciones}");

        return new ConfiguracionGemini(
            apiKey,
            NoVacia(ajustes.Modelo) ?? LeerVariable("GEMINI_MODEL") ?? ModeloPorDefecto,
            File.ReadAllText(RutaInstrucciones));
    }

    // También se lee del perfil de usuario: permite usar una variable creada con setx sin reiniciar Visual Studio.
    private static string? LeerVariable(string nombre) =>
        NoVacia(Environment.GetEnvironmentVariable(nombre))
        ?? NoVacia(Environment.GetEnvironmentVariable(nombre, EnvironmentVariableTarget.User));

    private static string? NoVacia(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}