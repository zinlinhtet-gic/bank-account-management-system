using System.Windows;
using bams.desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace bams.desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // Create and show main window
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register AuthContext as singleton
        services.AddSingleton<AuthContext>();

        // Register HTTP client
        services.AddHttpClient<Services.IAuthenticationService, Services.AuthenticationService>();

        // Register ViewModels
        services.AddTransient<ViewModels.LoginViewModel>();

        // Register Views
        services.AddTransient<Views.LoginView>();

        // Register main window
        services.AddTransient<MainWindow>();
    }
}

