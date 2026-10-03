namespace NeuroDocs.Models;

public enum FormatoImagen { Png, Jpeg }

/// <summary>Firma del profesional: imagen con su tamaño original en el PDF y las líneas de nombre/título/registro.</summary>
public sealed record Firma(
    byte[] Imagen,
    FormatoImagen Formato,
    double AnchoPt,
    double AltoPt,
    IReadOnlyList<string> Lineas);