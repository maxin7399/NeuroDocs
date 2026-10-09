using System.Reflection;

namespace NeuroDocs.Services;

/// <summary>Versión de la app, leída de lo que se definió en el .csproj al compilar.</summary>
public static class InfoAplicacion
{
    // .NET guarda aquí "1.0.0+<hash del commit>" cuando se compila dentro de un repositorio Git.
    private static readonly string VersionInformativa =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

    /// <summary>"1.0.0"</summary>
    public static string Version => VersionInformativa.Split('+')[0];

    /// <summary>"1.0.0 (a1b2c3d)": incluye el commit para saber exactamente qué código está instalado.</summary>
    public static string VersionCompleta
    {
        get
        {
            var partes = VersionInformativa.Split('+', 2);
            return partes.Length == 2
                ? $"{partes[0]} ({partes[1][..Math.Min(7, partes[1].Length)]})"
                : partes[0];
        }
    }
}