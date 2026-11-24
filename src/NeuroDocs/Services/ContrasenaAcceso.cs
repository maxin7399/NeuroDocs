using System.Security.Cryptography;

namespace NeuroDocs.Services;

/// <summary>
/// Contraseña de acceso a la app. Nunca se guarda la contraseña: solo una huella PBKDF2 (SHA-256)
/// con sal aleatoria, que no permite recuperarla.
/// </summary>
public static class ContrasenaAcceso
{
    public const int LongitudMinima = 4;

    private const int Iteraciones = 210_000;
    private const int BytesHuella = 32;
    private const int BytesSal = 16;

    public static (string Huella, string Sal) Crear(string contrasena)
    {
        byte[] sal = RandomNumberGenerator.GetBytes(BytesSal);
        return (Convert.ToBase64String(Derivar(contrasena, sal)), Convert.ToBase64String(sal));
    }

    public static bool Verificar(string contrasena, string huella, string sal)
    {
        try
        {
            byte[] esperada = Convert.FromBase64String(huella);
            byte[] calculada = Derivar(contrasena, Convert.FromBase64String(sal));
            return CryptographicOperations.FixedTimeEquals(esperada, calculada);
        }
        catch (FormatException)
        {
            return false; // configuración alterada: nunca se acepta
        }
    }

    private static byte[] Derivar(string contrasena, byte[] sal) =>
        Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHuella);
}
