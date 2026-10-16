using System.Globalization;

namespace NeuroDocs.Services;

public static class FormatoFecha
{
    private static readonly CultureInfo Espanol = new("es-CO");
    /// <summary>2026-10-15 → "15 de Octubre de 2026"</summary>
    public static string Larga(DateOnly fecha) => $"{fecha.Day} de {NombreMes(fecha.Month)} de {fecha.Year}";

    /// <summary>1 → "Enero"</summary>
    public static string NombreMes(int mes)
    {
        string nombre = Espanol.DateTimeFormat.GetMonthName(mes);
        return char.ToUpper(nombre[0], Espanol) + nombre[1..];
    }

    /// <summary>2026-09-01 → "Septiembre 2026"</summary>
    public static string MesAnio(DateOnly fecha) => $"{NombreMes(fecha.Month)} {fecha.Year}";
}