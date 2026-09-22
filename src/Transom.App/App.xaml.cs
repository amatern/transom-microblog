using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Transom.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// The application's dependency injection container and hosted services, started in
    /// <see cref="OnLaunched"/>.
    /// </summary>
    public static IHost Host { get; private set; } = null!;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    /// <summary>Logs unhandled exceptions before the default crash behavior runs. Does not set
    /// <see cref="Microsoft.UI.Xaml.UnhandledExceptionEventArgs.Handled"/> — this only adds visibility, it never
    /// swallows the exception.</summary>
    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine(e.Exception.ToString());
#endif
        Host?.Services.GetService<ILogger<App>>()?.LogError(e.Exception, "Unhandled exception");
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureTransomServices()
            .Build();

        _window = new MainWindow();
        _window.Activate();
    }
}