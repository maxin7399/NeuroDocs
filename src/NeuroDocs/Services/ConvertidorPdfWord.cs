using System.Runtime.InteropServices;

namespace NeuroDocs.Services;

/// <summary>Convierte .docx a PDF usando el Microsoft Word instalado (misma fidelidad que "Guardar como PDF").</summary>
public sealed class ConvertidorPdfWord
{
    // Constantes de la API de Word (sin interop no tenemos los enums).
    private const int WdExportFormatPdf = 17;
    private const int WdExportOptimizeForPrint = 0;
    private const int WdDoNotSaveChanges = 0;
    private const int WdAlertsNone = 0;
    private const int WdStatisticPages = 2;

    /// <summary>
    /// Word (COM) debe usarse desde un hilo STA; se crea uno dedicado para no congelar la ventana.
    /// Devuelve el número de páginas del documento según la paginación de Word.
    /// </summary>
    public Task<int> ConvertirAsync(string rutaDocx, string rutaPdf) =>
        EjecutarEnSta(() => Convertir(rutaDocx, rutaPdf));

    private static int Convertir(string rutaDocx, string rutaPdf)
    {
        var tipoWord = Type.GetTypeFromProgID("Word.Application")
            ?? throw new InvalidOperationException(
                "No se encontró Microsoft Word en este equipo. Es necesario para generar el PDF.");

        dynamic? word = null;
        dynamic? documento = null;
        try
        {
            word = Activator.CreateInstance(tipoWord)!;
            word.Visible = false;
            word.DisplayAlerts = WdAlertsNone;

            documento = word.Documents.Open(
                FileName: rutaDocx, ReadOnly: true, AddToRecentFiles: false, Visible: false);

            // Misma paginación que usará el PDF (el plan debe caber en una página).
            int paginas = (int)documento.ComputeStatistics(WdStatisticPages);

            documento.ExportAsFixedFormat(
                OutputFileName: rutaPdf,
                ExportFormat: WdExportFormatPdf,
                OpenAfterExport: false,
                OptimizeFor: WdExportOptimizeForPrint);

            return paginas;
        }
        finally
        {
            // Cerrar y liberar siempre: si no, quedan procesos WINWORD.EXE ocultos consumiendo memoria.
            if (documento is not null)
            {
                try { documento.Close(SaveChanges: WdDoNotSaveChanges); } catch (COMException) { }
                Marshal.FinalReleaseComObject(documento);
            }
            if (word is not null)
            {
                try { word.Quit(SaveChanges: WdDoNotSaveChanges); } catch (COMException) { }
                Marshal.FinalReleaseComObject(word);
            }
        }
    }

    private static Task<T> EjecutarEnSta<T>(Func<T> accion)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hilo = new Thread(() =>
        {
            try
            {
                tcs.SetResult(accion());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.IsBackground = true;
        hilo.Start();
        return tcs.Task;
    }
}