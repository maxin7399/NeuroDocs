using System.Text.RegularExpressions;
using NeuroDocs.Models;

namespace NeuroDocs.Services;

public static class ValidadorDocumentos
{
    public static IReadOnlyList<string> Validar(DatosPaciente paciente, DatosOrden orden)
    {
        var advertencias = new List<string>();

        if (paciente.Campos.TryGetValue(CamposPaciente.Identificacion, out var identificacion)
            && orden.NumeroDocumento is { } documentoOrden)
        {
            // "C.C. 8601373, Repelón – Atlántico." → "8601373" (tolera puntos: 8.601.373)
            string documentoHistoria = Regex.Replace(
                Regex.Match(identificacion, @"\d[\d.]{4,}").Value, @"\D", "");

            if (documentoHistoria.Length > 0 && documentoHistoria != documentoOrden)
            {
                advertencias.Add(
                    $"El número de documento no coincide: historia clínica {documentoHistoria}, orden {documentoOrden}.");
            }
        }

        if (paciente.Diagnosticos.Count == 0 && orden.Cie10Descripcion is null)
        {
            advertencias.Add(orden.Tipo == TipoOrden.OrdenCobro
                ? "La historia clínica no trae Impresión Diagnóstica y la orden de cobro solo incluye el código CIE-10. El campo Diagnóstico quedará vacío."
                : "No se encontró diagnóstico ni en la historia clínica ni en la orden de servicio.");
        }

        if (orden.TiempoPorDefecto)
        {
            advertencias.Add(
                $"La orden no indica el tiempo de terapia. Se usó el valor por defecto: " +
                $"{LectorOrden.SesionesPorDefecto} sesiones mensuales por {LectorOrden.MesesPorDefecto} meses. Verifícalo antes de generar.");
        }
        return advertencias;
    }
}