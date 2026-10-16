using NeuroDocs.Services;

namespace NeuroDocs.Tests;

public class FirmaExtractorTests
{
    [FactConMuestra("LauraPerezRiosECC.pdf")]
    public void Laura_ExtraeLaImagenYElBloqueDeTextoDebajo()
    {
        string ruta = Muestras.Ruta("LauraPerezRiosECC.pdf");
        var lineas = new PdfTextExtractor().ExtractLines(ruta);

        var firma = new FirmaExtractor().Extraer(ruta, lineas);

        Assert.NotNull(firma);
        Assert.NotEmpty(firma.Imagen);
        Assert.Equal(["M.Sc. Daniela Rojas Martínez", "Magíster en Neuropsicología", "T.P. 000000"], firma.Lineas);
        // No debe confundirse con el logo del encabezado, que se repite en cada página.
        Assert.InRange(firma.AnchoPt, 100, 300);
    }
}
