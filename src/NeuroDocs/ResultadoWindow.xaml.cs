using System.Runtime.InteropServices;
using System.Windows;

namespace NeuroDocs;

public partial class ResultadoWindow : Window
{
    public ResultadoWindow()
    {
        InitializeComponent();
    }

    public void MostrarContenido(string texto)
    {
        ContenidoTextBox.Text = texto;
        ContenidoTextBox.ScrollToHome();
    }

    private void CopiarButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(ContenidoTextBox.Text);
        }
        catch (COMException)
        {
            // Otra aplicación tiene el portapapeles bloqueado; ocurre de vez en cuando en Windows.
            MessageBox.Show("No se pudo copiar. Intenta de nuevo.", "NeuroDocs",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CerrarButton_Click(object sender, RoutedEventArgs e) => Close();
}