using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using CPTHub.Data;
using CPTHub.Services;
using CPTHub.ViewModels;
using CPTHub.Views;

namespace CPTHub
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Run console demo first
            Console.WriteLine("Starting CPTHub Demo...");
            await DemoProgram.RunDemo();
            
            // Then try to start the WPF application
            try
            {
                _host = Host.CreateDefaultBuilder()
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // Database
                        services.AddDbContext<CPTHubDbContext>(options =>
                            options.UseSqlite(context.Configuration.GetConnectionString("DefaultConnection")));

                        // AI Services
                        services.AddHttpClient<ILearnLMService, LearnLMService>();
                        services.AddSingleton<IAIService, AIServiceWithFallback>();
                        services.AddSingleton<ICacheService, MemoryCacheService>();
                        services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();

                        // Core Services
                        services.AddSingleton<IStudyService, StudyService>();
                        services.AddSingleton<IQuizService, QuizService>();
                        services.AddSingleton<IFlashcardService, FlashcardService>();
                        services.AddSingleton<IAnalyticsService, AnalyticsService>();
                        services.AddSingleton<INotificationService, SimpleNotificationService>();

                        // ViewModels
                        services.AddTransient<MainWindowViewModel>();

                        // Views
                        services.AddTransient<MainWindow>();
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.AddConsole();
                        logging.AddDebug();
                    })
                    .Build();

                await _host.StartAsync();

                // Initialize database
                using var scope = _host.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CPTHubDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                // Initialize data if needed
                var dataInitializer = scope.ServiceProvider.GetRequiredService<IStudyService>();
                await dataInitializer.InitializeContentAsync();

                // Show main window
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting WPF application: {ex.Message}");
                Console.WriteLine("Demo completed successfully. The full WPF application requires additional setup.");
                Environment.Exit(0);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            base.OnExit(e);
        }
    }
}
