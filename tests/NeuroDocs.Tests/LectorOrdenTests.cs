using NeuroDocs.Models;
using NeuroDocs.Services;

namespace NeuroDocs.Tests;

public class LectorOrdenTests
{
    private readonly LectorOrden _lector = new();

    [FactConMuestra("LauraPerezRiosCUPS.pdf")]
    public void Laura_LeeLaSolicitudCups()
    {
        var lineas = new PdfTextExtractor().ExtractLines(Muestras.Ruta("LauraPerezRiosCUPS.pdf"));

        Assert.True(_lector.TryParse(lineas, out var orden));
        Assert.Equal(TipoOrden.SolicitudServicio, orden.Tipo);
        Assert.Equal("1234567890", orden.NumeroDocumento);
        Assert.Equal(10, orden.SesionesMensuales);
        Assert.Equal(3, orden.Meses);
        Assert.Equal("F067", orden.Cie10Codigo);
        Assert.Equal("TRASTORNO COGNOSCITIVO LEVE", orden.Cie10Descripcion);
        Assert.False(orden.TiempoPorDefecto);
    }

    [FactConMuestra("LauraPerezRiosECC.pdf")]
    public void EccEnElEspacioDeLaOrden_NoSeReconoce()
    {
        var lineas = new PdfTextExtractor().ExtractLines(Muestras.Ruta("LauraPerezRiosECC.pdf"));

        Assert.False(_lector.TryParse(lineas, out var orden));
        Assert.Null(orden);
    }

    [Fact]
    public void Cups_DescripcionCie10EnDosLineas_SeUne()
    {
        var lineas = new Lineas()
            .Titulo("SOLICITUD DE ORDEN DE SERVICIO")
            .Texto("Nombres: MARIA Apellidos: EJEMPLO No.Documento: 1111111")
            .Titulo("SE SOLICITA 10 SESIONES")
            .Titulo("MENSUALES POR 3 MESES")
            .Titulo("Diagnóstico CIE-10: CIE-11:")
            .Texto("R418 - OTROS SINTOMAS Y SIGNOS QUE INVOLUCRAN LA FUNCION COGNOSCITIVA Y LA")
            .Texto("CONCIENCIA Y LOS NO ESPECIFICADOS")
            .Espacio(80)
            .Texto("PROFESIONAL EJEMPLO")
            .Lista;

        Assert.True(_lector.TryParse(lineas, out var orden));
        Assert.Equal("R418", orden.Cie10Codigo);
        Assert.Equal(
            "OTROS SINTOMAS Y SIGNOS QUE INVOLUCRAN LA FUNCION COGNOSCITIVA Y LA CONCIENCIA Y LOS NO ESPECIFICADOS",
            orden.Cie10Descripcion);
        Assert.Equal("1111111", orden.NumeroDocumento);
    }

    [Fact]
    public void OrdenDeCobro_ConTiempoEnObservaciones()
    {
        var lineas = OrdenCobro(observaciones: "944301: 10 SESIONES X MES X 3 MESES - RENOVAR EN JULIO 2026");

        Assert.True(_lector.TryParse(lineas, out var orden));
        Assert.Equal(TipoOrden.OrdenCobro, orden.Tipo);
        Assert.Equal("8601375", orden.NumeroDocumento);
        Assert.Equal("F067", orden.Cie10Codigo);
        Assert.Null(orden.Cie10Descripcion);
        Assert.Equal(9, orden.CantidadAutorizada);
        Assert.Equal(10, orden.SesionesMensuales);
        Assert.Equal(3, orden.Meses);
        Assert.False(orden.TiempoPorDefecto);
    }

    [Fact]
    public void OrdenDeCobro_SinTiempo_UsaValorPorDefectoYLoMarca()
    {
        var lineas = OrdenCobro(observaciones: "SIN OBSERVACIONES");

        Assert.True(_lector.TryParse(lineas, out var orden));
        Assert.Equal(LectorOrden.SesionesPorDefecto, orden.SesionesMensuales);
        Assert.Equal(LectorOrden.MesesPorDefecto, orden.Meses);
        Assert.True(orden.TiempoPorDefecto);
        // La cantidad autorizada no se usa como sesiones.
        Assert.Equal(9, orden.CantidadAutorizada);
    }

    private static IReadOnlyList<PdfLine> OrdenCobro(string observaciones) => new Lineas()
        .Titulo("ORDEN DE COBRO")
        .Texto("CC 8601375 PACIENTE DE PRUEBA")
        .Texto("944301 944301 944301 TERAPIA DE REHABILITACION COGNITIVA (SESION) F067 9")
        .Titulo("Observaciones")
        .Texto(observaciones)
        .Lista;
}
