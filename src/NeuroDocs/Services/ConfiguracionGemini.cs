using System.IO;

namespace NeuroDocs.Services;

/// <summary>Clase y no record a propósito: el ToString() de un record imprimiría la clave.</summary>
public sealed class ConfiguracionGemini
{
    public const string ModeloPorDefecto = "gemini-3.8-flash";

    public static readonly IReadOnlyList<string> ModelosSugeridos =
        ["gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.5-flash"];

    private static readonly string CarpetaInstrucciones = Path.Combine(AppContext.BaseDirectory, "IA");

    public string ApiKey { get; }
    public string Modelo { get; }

    private ConfiguracionGemini(string apiKey, string modelo)
    {
        ApiKey = apiKey;
        Modelo = modelo;
    }

    /// <summary>Prioridad: ventana de Configuración y, como respaldo, variables de entorno.</summary>
    public static ConfiguracionGemini Cargar()
    {
        var ajustes = AjustesUsuario.Cargar();

        string apiKey = ajustes.ObtenerApiKey()
            ?? LeerVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException("No hay una clave de Gemini configurada. Ingrésala en Configuración.");

        return new ConfiguracionGemini(apiKey, NoVacia(ajustes.Modelo) ?? LeerVariable("GEMINI_MODEL") ?? ModeloPorDefecto);
    }

    /// <summary>Se lee en cada uso: las instrucciones se pueden ajustar sin reiniciar la app.</summary>
    public static string LeerInstrucciones(string archivo)
    {
        string ruta = Path.Combine(CarpetaInstrucciones, archivo);
        return File.Exists(ruta)
            ? File.ReadAllText(ruta)
            : throw new FileNotFoundException($"No se encontró el archivo de instrucciones: {ruta}");
    }

    private static string? LeerVariable(string nombre) =>
        NoVacia(Environment.GetEnvironmentVariable(nombre))
        ?? NoVacia(Environment.GetEnvironmentVariable(nombre, EnvironmentVariableTarget.User));

    private static string? NoVacia(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}