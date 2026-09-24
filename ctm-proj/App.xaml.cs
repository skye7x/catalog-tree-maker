using System.Windows;
using System.Windows.Threading;

namespace ctm_proj;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += HandleUnhandledException;
        base.OnStartup(e);
    }

    private static void HandleUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Unexpected error:\n{e.Exception.Message}", "CTM Project",
            MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;
    }
}
