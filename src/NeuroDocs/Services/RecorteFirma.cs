using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NeuroDocs.Models;

namespace NeuroDocs.Services;

/// <summary>
/// Quita los márgenes blancos o transparentes alrededor de la firma. La parte visible conserva
/// su tamaño en el documento: solo deja de ocupar el espacio vacío que la rodeaba.
/// </summary>
public static class RecorteFirma
{
    private const byte UmbralBlanco = 235; // canal más oscuro por encima de esto = fondo
    private const byte UmbralAlfa = 16;    // alfa por debajo de esto = transparente
    private const int MargenPx = 2;        // aire alrededor del trazo para no cortarlo al ras

    public static Firma Recortar(Firma firma)
    {
        try
        {
            return RecortarImagen(firma);
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException)
        {
            return firma; // formato que WPF no decodifica: mejor la firma completa que ninguna
        }
    }

    private static Firma RecortarImagen(Firma firma)
    {
        BitmapSource origen;
        using (var stream = new MemoryStream(firma.Imagen))
        {
            origen = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        }

        var bgra = new FormatConvertedBitmap(origen, PixelFormats.Bgra32, null, 0);
        int ancho = bgra.PixelWidth, alto = bgra.PixelHeight, stride = ancho * 4;
        var pixeles = new byte[stride * alto];
        bgra.CopyPixels(pixeles, stride, 0);

        int izq = ancho, der = -1, arr = alto, aba = -1;
        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                int i = y * stride + x * 4;
                byte b = pixeles[i], g = pixeles[i + 1], r = pixeles[i + 2], a = pixeles[i + 3];
                if (a < UmbralAlfa || Math.Min(r, Math.Min(g, b)) > UmbralBlanco) continue;

                izq = Math.Min(izq, x);
                der = Math.Max(der, x);
                arr = Math.Min(arr, y);
                aba = Math.Max(aba, y);
            }
        }

        if (der < 0) return firma; // imagen vacía: no hay nada que recortar

        var recorte = new Int32Rect(
            Math.Max(0, izq - MargenPx),
            Math.Max(0, arr - MargenPx),
            0, 0);
        recorte.Width = Math.Min(ancho, der + MargenPx + 1) - recorte.X;
        recorte.Height = Math.Min(alto, aba + MargenPx + 1) - recorte.Y;

        if (recorte.Width == ancho && recorte.Height == alto) return firma;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(new CroppedBitmap(bgra, recorte)));
        using var salida = new MemoryStream();
        encoder.Save(salida);

        // Mismos puntos por píxel que la imagen original: la firma visible no cambia de tamaño.
        return firma with
        {
            Imagen = salida.ToArray(),
            Formato = FormatoImagen.Png,
            AnchoPt = firma.AnchoPt * recorte.Width / ancho,
            AltoPt = firma.AltoPt * recorte.Height / alto
        };
    }
}
