using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NeuroDocs.Models;
using NeuroDocs.Services;

namespace NeuroDocs.Tests;

public class RecorteFirmaTests
{
    [Fact]
    public void QuitaElMargenBlanco_SinCambiarLaEscalaDelTrazo()
    {
        // 100x50 px blanco con un trazo negro de 20x10 px en (20, 10); en el PDF ocupa 200x100 pt.
        var firma = new Firma(Png(100, 50, new Int32Rect(20, 10, 20, 10)), FormatoImagen.Png, 200, 100, []);

        var recortada = RecorteFirma.Recortar(firma);

        // Trazo + 2 px de margen por lado = 24x14 px, a 2 pt por píxel.
        Assert.Equal(48, recortada.AnchoPt, precision: 6);
        Assert.Equal(28, recortada.AltoPt, precision: 6);
        var imagen = Decodificar(recortada.Imagen);
        Assert.Equal(24, imagen.PixelWidth);
        Assert.Equal(14, imagen.PixelHeight);
    }

    [Fact]
    public void SinMargen_DevuelveLaMismaFirma()
    {
        var firma = new Firma(Png(10, 10, new Int32Rect(0, 0, 10, 10)), FormatoImagen.Png, 50, 50, []);

        Assert.Same(firma, RecorteFirma.Recortar(firma));
    }

    [Fact]
    public void BytesQueNoSonImagen_DevuelveLaFirmaSinTocar()
    {
        var firma = new Firma([1, 2, 3, 4], FormatoImagen.Jpeg, 50, 50, []);

        Assert.Same(firma, RecorteFirma.Recortar(firma));
    }

    [FactConMuestra("LauraPerezRiosECC.pdf")]
    public void Laura_LaFirmaRecortadaNoCreceNiPierdeElTrazo()
    {
        string ruta = Muestras.Ruta("LauraPerezRiosECC.pdf");
        var lineas = new PdfTextExtractor().ExtractLines(ruta);

        var firma = new FirmaExtractor().Extraer(ruta, lineas);

        Assert.NotNull(firma);
        Assert.InRange(firma.AnchoPt, 20, 170.5);
        Assert.InRange(firma.AltoPt, 5, 43.5);
    }

    private static byte[] Png(int ancho, int alto, Int32Rect trazo)
    {
        var bitmap = new WriteableBitmap(ancho, alto, 96, 96, PixelFormats.Bgra32, null);
        var blanco = Enumerable.Repeat((byte)255, ancho * alto * 4).ToArray();
        bitmap.WritePixels(new Int32Rect(0, 0, ancho, alto), blanco, ancho * 4, 0);

        var negro = new byte[trazo.Width * trazo.Height * 4];
        for (int i = 3; i < negro.Length; i += 4) negro[i] = 255; // B=G=R=0, alfa opaco
        bitmap.WritePixels(trazo, negro, trazo.Width * 4, 0);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static BitmapSource Decodificar(byte[] png)
    {
        using var stream = new MemoryStream(png);
        return BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
    }
}
