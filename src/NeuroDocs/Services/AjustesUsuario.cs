using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeuroDocs.Services;

/// <summary>
/// Configuración del usuario en %AppData%\NeuroDocs\configuracion.json.
/// La clave de API se guarda cifrada con DPAPI: solo esta cuenta de Windows puede descifrarla.
/// </summary>
public sealed class AjustesUsuario
{
    private static readonly string Carpeta = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NeuroDocs");
    private static readonly string Ruta = Path.Combine(Carpeta, "configuracion.json");

    // Capa adicional: separa estos datos de los de otras aplicaciones que también usen DPAPI.
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("NeuroDocs.Gemini.v1");

    public string? Modelo { get; set; }
    public string? ApiKeyCifrada { get; set; }

    [JsonIgnore]
    public bool TieneApiKey => !string.IsNullOrEmpty(ApiKeyCifrada);

    public static AjustesUsuario Cargar()
    {
        if (!File.Exists(Ruta)) return new AjustesUsuario();

        try
        {
            return JsonSerializer.Deserialize<AjustesUsuario>(File.ReadAllText(Ruta)) ?? new AjustesUsuario();
        }
        catch (JsonException)
        {
            return new AjustesUsuario(); // archivo dañado: se empieza de cero
        }
    }

    public void Guardar()
    {
        Directory.CreateDirectory(Carpeta);
        File.WriteAllText(Ruta, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public string? ObtenerApiKey()
    {
        if (ApiKeyCifrada is null) return null;

        try
        {
            byte[] datos = ProtectedData.Unprotect(
                Convert.FromBase64String(ApiKeyCifrada), Entropia, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(datos);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null; // cifrada por otro usuario o equipo: hay que volver a ingresarla
        }
    }

    public void EstablecerApiKey(string apiKey)
    {
        byte[] cifrado = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey), Entropia, DataProtectionScope.CurrentUser);
        ApiKeyCifrada = Convert.ToBase64String(cifrado);
    }
}