using NeuroDocs.Models;

namespace NeuroDocs.Tests;

/// <summary>PDFs ficticios de muestras/ (fuera de git): los tests que los usan se omiten si no están.</summary>
public static class Muestras
{
    public static readonly string Carpeta = BuscarCarpeta();

    public static string Ruta(string archivo) => Path.Combine(Carpeta, archivo);

    private static string BuscarCarpeta()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidata = Path.Combine(dir.FullName, "muestras");
            if (Directory.Exists(candidata)) return candidata;
        }
        return "";
    }
}

/// <summary>Como [Fact], pero se omite si falta el PDF de muestras/.</summary>
public sealed class FactConMuestraAttribute : FactAttribute
{
    public FactConMuestraAttribute(string archivo)
    {
        if (!File.Exists(Muestras.Ruta(archivo)))
            Skip = $"Falta muestras/{archivo}";
    }
}

/// <summary>Arma líneas de PDF a mano para probar formatos que no tenemos en muestras.</summary>
public sealed class Lineas
{
    private readonly List<PdfLine> _lineas = [];
    private double _y = 750;
    private int _pagina = 1;

    /// <summary>Etiqueta en negrita seguida de valor normal ("Nombre: ...").</summary>
    public Lineas Campo(string texto) => Agregar(texto, startsBold: true, allBold: false);

    public Lineas Titulo(string texto) => Agregar(texto, startsBold: true, allBold: true);

    public Lineas Texto(string texto) => Agregar(texto, startsBold: false, allBold: false);

    /// <summary>Deja un salto vertical grande antes de la siguiente línea.</summary>
    public Lineas Espacio(double pt = 40)
    {
        _y -= pt;
        return this;
    }

    public Lineas NuevaPagina()
    {
        _pagina++;
        _y = 750;
        return this;
    }

    public IReadOnlyList<PdfLine> Lista => _lineas;

    private Lineas Agregar(string texto, bool startsBold, bool allBold)
    {
        _lineas.Add(new PdfLine(_pagina, texto, startsBold, allBold, _y));
        _y -= 12;
        return this;
    }
}
