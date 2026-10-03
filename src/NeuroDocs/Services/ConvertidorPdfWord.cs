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

    /// <summary>Word (COM) debe usarse desde un hilo STA; se crea uno dedicado para no congelar la ventana.</summary>
    public Task ConvertirAsync(string rutaDocx, string rutaPdf) =>
        EjecutarEnSta(() => Convertir(rutaDocx, rutaPdf));

    private static void Convertir(string rutaDocx, string rutaPdf)
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

            documento.ExportAsFixedFormat(
                OutputFileName: rutaPdf,
                ExportFormat: WdExportFormatPdf,
                OpenAfterExport: false,
                OptimizeFor: WdExportOptimizeForPrint);
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

    private static Task EjecutarEnSta(Action accion)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hilo = new Thread(() =>
        {
            try
            {
                accion();
                tcs.SetResult();
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