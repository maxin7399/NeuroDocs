using NeuroDocs.Services;

namespace NeuroDocs.Tests;

public class ContrasenaAccesoTests
{
    [Fact]
    public void Verificar_AceptaSoloLaMismaContrasena()
    {
        var (huella, sal) = ContrasenaAcceso.Crear("consultorio2026");

        Assert.True(ContrasenaAcceso.Verificar("consultorio2026", huella, sal));
        Assert.False(ContrasenaAcceso.Verificar("Consultorio2026", huella, sal));
        Assert.False(ContrasenaAcceso.Verificar("", huella, sal));
    }

    [Fact]
    public void Crear_UsaSalDistintaCadaVez_YNoGuardaLaContrasena()
    {
        var a = ContrasenaAcceso.Crear("1234");
        var b = ContrasenaAcceso.Crear("1234");

        Assert.NotEqual(a.Sal, b.Sal);
        Assert.NotEqual(a.Huella, b.Huella);
        Assert.DoesNotContain("1234", a.Huella);
    }

    [Fact]
    public void Verificar_RechazaDatosAlterados()
    {
        Assert.False(ContrasenaAcceso.Verificar("1234", "no-es-base64", "tampoco"));
    }

    [Fact]
    public void Ajustes_SinContrasena_NoVerificaNada()
    {
        var ajustes = new AjustesUsuario();

        Assert.False(ajustes.TieneContrasena);
        Assert.False(ajustes.VerificarContrasena(""));
        Assert.Equal(AjustesUsuario.MinutosBloqueoPorDefecto, ajustes.MinutosBloqueoEfectivos);
    }
}
