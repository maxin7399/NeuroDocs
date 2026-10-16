using System.IO;
using System.Windows;
using System.Windows.Documents;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace NeuroDocs.Services;

/// <summary>Extrae el texto de la evaluación del profesional (RTF, DOCX, PDF o TXT).</summary>
public static class LectorEvaluacion
{
    public const string Extensiones = "rtf,docx,doc,pdf,txt";
    public const string Filtro = "Evaluación (*.rtf;*.docx;*.doc;*.pdf;*.txt)|*.rtf;*.docx;*.doc;*.pdf;*.txt";

    private enum Formato { Texto, Rtf, Docx, Pdf, DocAntiguo }

    /// <remarks>Llamar desde el hilo de la interfaz: el RTF se lee con un FlowDocument de WPF.</remarks>
    public static string LeerTexto(string ruta)
    {
        string texto = DetectarFormato(ruta) switch
        {
            Formato.Rtf => LeerRtf(ruta),
            Formato.Docx => LeerDocx(ruta),
            Formato.Pdf => LeerPdf(ruta),
            Formato.DocAntiguo => throw new InvalidOperationException(
                "La evaluación está en formato Word 97-2003 (.doc). Ábrela en Word y guárdala como .docx o .rtf."),
            _ => File.ReadAllText(ruta)
        };

        string limpio = string.Join("\n", texto
            .Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        return limpio.Length > 0 ? limpio : throw new InvalidOperationException("La evaluación no contiene texto.");
    }

    private static Formato DetectarFormato(string ruta)
    {
        var inicio = new byte[5];
        int leidos;
        using (var stream = File.OpenRead(ruta)) leidos = stream.Read(inicio, 0, inicio.Length);

        bool Empieza(params byte[] firma) => leidos >= firma.Length && inicio.AsSpan(0, firma.Length).SequenceEqual(firma);

        if (Empieza("{\\rtf"u8.ToArray())) return Formato.Rtf;
        if (Empieza("%PDF"u8.ToArray())) return Formato.Pdf;
        if (Empieza(0x50, 0x4B)) return Formato.Docx;               // "PK": .docx es un ZIP
        if (Empieza(0xD0, 0xCF, 0x11, 0xE0)) return Formato.DocAntiguo;
        return Formato.Texto;
    }

    private static string LeerRtf(string ruta)
    {
        var documento = new FlowDocument();
        var rango = new TextRange(documento.ContentStart, documento.ContentEnd);
        using var stream = File.OpenRead(ruta);
        rango.Load(stream, DataFormats.Rtf);
        return rango.Text;
    }

    private static string LeerDocx(string ruta)
    {
        using var documento = WordprocessingDocument.Open(ruta, isEditable: false);
        var body = documento.MainDocumentPart?.Document.Body;
        return body is null ? "" : string.Join("\n", body.Descendants<W.Paragraph>().Select(p => p.InnerText));
    }

    // Sin filtrar líneas repetidas: en las evoluciones frases como "Paciente asiste a consulta…" se repiten a propósito.
    private static string LeerPdf(string ruta) =>
        string.Join("\n", new PdfTextExtractor().ExtractLines(ruta).Select(l => l.Text));
}