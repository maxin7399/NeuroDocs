namespace NeuroDocs.Models;

/// <summary>Una línea visual del PDF. Y = base de la línea en puntos PDF (origen abajo: mayor Y = más arriba).</summary>
public sealed record PdfLine(int Page, string Text, bool StartsBold, bool IsAllBold, double Y);