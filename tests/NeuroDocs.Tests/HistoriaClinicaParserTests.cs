using NeuroDocs.Models;
using NeuroDocs.Services;

namespace NeuroDocs.Tests;

public class HistoriaClinicaParserTests
{
    private readonly HistoriaClinicaParser _parser = new();

    [FactConMuestra("LauraPerezRiosECC.pdf")]
    public void Laura_LeeTodosLosCamposDeIdentificacion()
    {
        var lineas = new PdfTextExtractor().ExtractLines(Muestras.Ruta("LauraPerezRiosECC.pdf"));
        var datos = _parser.Parse(lineas);

        Assert.Equal("Laura Marcela Pérez Ríos", datos.Campos[CamposPaciente.Nombre]);
        Assert.Equal("C.C. 1234567890, Barranquilla – Atlántico.", datos.Campos[CamposPaciente.Identificacion]);
        Assert.Equal("55 años", datos.Campos[CamposPaciente.Edad]);
        Assert.Equal("Diestra.", datos.Campos[CamposPaciente.Lateralidad]);
        Assert.Equal("Calle 10 # 20-30. Barrio Ejemplo, Barranquilla – Atlántico.", datos.Campos[CamposPaciente.Direccion]);
        Assert.Equal("Contributivo. Cotizante.", datos.Campos[CamposPaciente.TipoVinculacion]);
        Assert.Equal("laura.perez@ejemplo.com", datos.Campos[CamposPaciente.Email]);

        // Todos menos Problemas en el colegio, que este ECC no trae.
        Assert.Equal(CamposPaciente.Todos.Count - 1, datos.Campos.Count);
        Assert.False(datos.Campos.ContainsKey(CamposPaciente.ProblemasColegio));
    }

    [FactConMuestra("LauraPerezRiosECC.pdf")]
    public void Laura_LeeImpresionDiagnostica()
    {
        var lineas = new PdfTextExtractor().ExtractLines(Muestras.Ruta("LauraPerezRiosECC.pdf"));

        var datos = _parser.Parse(lineas);

        Assert.Equal(["Deterioro Cognitivo Leve. (DSM-5, Trastorno Neurocognitivo Menor)."], datos.Diagnosticos);
    }

    [Fact]
    public void ValorLargo_SeUneConLaLineaSiguiente()
    {
        var lineas = new Lineas()
            .Campo("Dirección Completa: Calle 10 # 20-30. Barrio Ejemplo,")
            .Texto("Barranquilla – Atlántico.")
            .Campo("Edad: 55 años")
            .Lista;

        var datos = _parser.Parse(lineas);

        Assert.Equal("Calle 10 # 20-30. Barrio Ejemplo, Barranquilla – Atlántico.", datos.Campos[CamposPaciente.Direccion]);
        Assert.Equal("55 años", datos.Campos[CamposPaciente.Edad]);
    }

    [Fact]
    public void EtiquetaSinTildes_SeReconoce()
    {
        var lineas = new Lineas().Campo("Genero: Masculino").Campo("Direccion: Cra 1").Lista;

        var datos = _parser.Parse(lineas);

        Assert.Equal("Masculino", datos.Campos[CamposPaciente.Genero]);
        Assert.Equal("Cra 1", datos.Campos[CamposPaciente.Direccion]);
    }

    [Fact]
    public void CampoRepetido_ConservaLaPrimeraAparicion()
    {
        var lineas = new Lineas().Campo("Nombre: Paciente Uno").Campo("Nombre: Otro texto más abajo").Lista;

        var datos = _parser.Parse(lineas);

        Assert.Equal("Paciente Uno", datos.Campos[CamposPaciente.Nombre]);
    }

    [Fact]
    public void TituloNumeradoEnMayusculas_EncuentraElDiagnostico()
    {
        var lineas = new Lineas()
            .Titulo("9. IMPRESIÓN DIAGNÓSTICA")
            .Texto("• Trastorno por déficit de atención.")
            .Texto("• Dificultades específicas del aprendizaje con")
            .Texto("alteración en la lectura.")
            .Titulo("10. RECOMENDACIONES")
            .Texto("• Rehabilitación neuropsicológica.")
            .Lista;

        var datos = _parser.Parse(lineas);

        Assert.Equal(
            ["Trastorno por déficit de atención.", "Dificultades específicas del aprendizaje con alteración en la lectura."],
            datos.Diagnosticos);
    }

    [Fact]
    public void SinImpresionDiagnostica_DevuelveListaVacia()
    {
        var lineas = new Lineas().Campo("Nombre: Paciente Uno").Titulo("Recomendaciones").Texto("Algo").Lista;

        Assert.Empty(_parser.Parse(lineas).Diagnosticos);
    }
}
