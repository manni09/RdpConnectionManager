using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RdpManager.Services;

namespace RdpManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize logging
            LoggingService.Info("Application starting...");
            LoggingService.Info($"Version: {typeof(App).Assembly.GetName().Version}");
            LoggingService.Info($"OS: {Environment.OSVersion}");
            LoggingService.Info($".NET Version: {Environment.Version}");

            // Clean up old logs on startup
            LoggingService.CleanupOldLogs(30);

            // Setup global exception handlers
            SetupExceptionHandling();

            LoggingService.Info("Application startup complete");
        }

        private void SetupExceptionHandling()
        {
            // Handle UI thread exceptions
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Handle non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Handle task exceptions
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LoggingService.Critical("Unhandled UI exception", e.Exception);

            var result = MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nWould you like to continue? (Choosing 'No' will close the application)",
                "Application Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                e.Handled = true;
            }
            else
            {
                LoggingService.Critical("User chose to terminate application after error");
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception ?? new Exception("Unknown exception");
            LoggingService.Critical($"Unhandled domain exception (Terminating: {e.IsTerminating})", exception);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LoggingService.Error("Unobserved task exception", e.Exception);
            e.SetObserved(); // Prevent crash
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggingService.Info($"Application exiting with code: {e.ApplicationExitCode}");
            base.OnExit(e);
        }
    }
}
