using NeuroDocs.Models;
using NeuroDocs.Services;

namespace NeuroDocs.Tests;

public class CombinadorExamenesTests
{
    private static ExamenLeido Examen(string ruta, int longitud, Dictionary<string, string> campos, params string[] diagnosticos) =>
        new(ruta, new DatosPaciente(campos, diagnosticos), longitud);

    [Fact]
    public void UnSoloExamen_SinAvisos()
    {
        var ecc = Examen("ECC.pdf", 100, new() { [CamposPaciente.Nombre] = "Ana" });

        var r = CombinadorExamenes.Combinar(ecc, null);

        Assert.Same(ecc, r.Principal);
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public void UsaElDeMasContenido_CompletaLoQueFalta_YAvisaDiferencias()
    {
        var ecc = Examen(@"C:\p\ECC.pdf", 100, new()
        {
            [CamposPaciente.Nombre] = "Ana Pérez",
            [CamposPaciente.Identificacion] = "C.C. 123",
            [CamposPaciente.Lateralidad] = "Diestra"
        }, "Deterioro cognitivo");
        var ad5 = Examen(@"C:\p\AD5.pdf", 500, new()
        {
            [CamposPaciente.Nombre] = "ana perez.",      // igual salvo tildes, mayúsculas y puntuación
            [CamposPaciente.Identificacion] = "C.C. 124"
        });

        var r = CombinadorExamenes.Combinar(ecc, ad5);

        Assert.Same(ad5, r.Principal);
        Assert.Equal("C.C. 124", r.Paciente.Campos[CamposPaciente.Identificacion]);
        Assert.Equal("Diestra", r.Paciente.Campos[CamposPaciente.Lateralidad]);
        Assert.Equal(["Deterioro cognitivo"], r.Paciente.Diagnosticos);
        Assert.Contains(r.Avisos, a => a.Contains("«AD5.pdf»") && a.StartsWith("Se usaron"));
        Assert.Contains(r.Avisos, a => a.Contains(CamposPaciente.Identificacion) && a.Contains("124"));
        Assert.DoesNotContain(r.Avisos, a => a.StartsWith("Diferencia en " + CamposPaciente.Nombre));
    }
}
