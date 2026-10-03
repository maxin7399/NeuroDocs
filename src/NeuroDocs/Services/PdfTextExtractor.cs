using NeuroDocs.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace NeuroDocs.Services;

public sealed class PdfTextExtractor
{
    // Diferencia máxima (en puntos PDF) para considerar que dos palabras están en la misma línea.
    private const double LineTolerance = 2.0;

    public IReadOnlyList<PdfLine> ExtractLines(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);

        var lines = new List<PdfLine>();
        foreach (Page page in document.GetPages())
        {
            lines.AddRange(ExtractPageLines(page));
        }
        return lines;
    }

    private static IEnumerable<PdfLine> ExtractPageLines(Page page)
    {
        // En PDF el origen está abajo a la izquierda: Y mayor = más arriba en la página.
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left);

        var current = new List<Word>();
        double lineBottom = 0;

        foreach (var word in words)
        {
            if (current.Count > 0 && Math.Abs(word.BoundingBox.Bottom - lineBottom) > LineTolerance)
            {
                yield return BuildLine(page.Number, current);
                current = new List<Word>();
            }

            if (current.Count == 0)
            {
                lineBottom = word.BoundingBox.Bottom;
            }
            current.Add(word);
        }

        if (current.Count > 0)
        {
            yield return BuildLine(page.Number, current);
        }
    }

    private static PdfLine BuildLine(int pageNumber, List<Word> words)
    {
        var ordered = words.OrderBy(w => w.BoundingBox.Left).ToList();
        string text = string.Join(" ", ordered.Select(w => w.Text));

        return new PdfLine(pageNumber, text, IsBold(ordered[0]), ordered.All(IsBold),
                           ordered.Min(w => w.BoundingBox.Bottom));
    }

    private static bool IsBold(Word word) =>
        word.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true;
}