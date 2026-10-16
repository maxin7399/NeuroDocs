using NeuroDocs.Models;
using NeuroDocs.Services;

namespace NeuroDocs.Tests;

/// <summary>Reglas que combinan ECC y orden: diagnóstico, advertencias y listas digitadas.</summary>
public class ReglasPlanTests
{
    private static DatosPaciente Paciente(string? identificacion = null, params string[] diagnosticos)
    {
        var campos = new Dictionary<string, string>();
        if (identificacion is not null) campos[CamposPaciente.Identificacion] = identificacion;
        return new DatosPaciente(campos, diagnosticos);
    }

    private static DatosOrden Orden(string? documento = "8601375", string? descripcion = "TRASTORNO DE LA CONDUCTA SOCIABLE",
                                    TipoOrden tipo = TipoOrden.SolicitudServicio, bool tiempoPorDefecto = false) =>
        new(tipo, documento, 10, 3, "F912", descripcion, TiempoPorDefecto: tiempoPorDefecto);

    [Fact]
    public void Diagnostico_PrioridadEcc_SinPuntoFinal()
    {
        var paciente = Paciente(null, "Deterioro Cognitivo Leve. (DSM-5, Trastorno Neurocognitivo Menor).");

        Assert.Equal("Deterioro Cognitivo Leve. (DSM-5, Trastorno Neurocognitivo Menor)", ResolverDiagnostico.Texto(paciente, Orden()));
        Assert.Equal("historia clínica", ResolverDiagnostico.Origen(paciente, Orden()));
    }

    [Fact]
    public void Diagnostico_SinEcc_UsaCie10DeLaOrdenEnFormatoNormal()
    {
        Assert.Equal("Trastorno de la conducta sociable", ResolverDiagnostico.Texto(Paciente(), Orden()));
    }

    [Fact]
    public void Advertencia_DocumentoNoCoincide()
    {
        var advertencias = ValidadorDocumentos.Validar(Paciente("C.C. 8.601.373, Repelón – Atlántico."), Orden());

        Assert.Contains(advertencias, a => a.Contains("8601373") && a.Contains("8601375"));
    }

    [Fact]
    public void SinAdvertencias_CuandoTodoCoincide()
    {
        var advertencias = ValidadorDocumentos.Validar(Paciente("C.C. 8601375, Repelón."), Orden());

        Assert.Empty(advertencias);
    }

    [Fact]
    public void Advertencias_SinDiagnosticoYTiempoPorDefecto()
    {
        var orden = Orden(descripcion: null, tipo: TipoOrden.OrdenCobro, tiempoPorDefecto: true);

        var advertencias = ValidadorDocumentos.Validar(Paciente(), orden);

        Assert.Equal(2, advertencias.Count);
        Assert.Contains(advertencias, a => a.Contains("orden de cobro"));
        Assert.Contains(advertencias, a => a.Contains("valor por defecto"));
    }

    [Fact]
    public void ListaPegadaDesdeWord_QuitaVinetasYLineasVacias()
    {
        string pegado = "•\tAtención sostenida.\r\n\r\n Memoria lógica.\n1. Praxias.\n- Gnosias.\n   \n";

        Assert.Equal(["Atención sostenida.", "Memoria lógica.", "Praxias.", "Gnosias."], ListaTexto.ParsearItems(pegado));
    }
}
