using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Rothenberger.Ortungssimulator.V2
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Fehlerbehandlung, z.B. Logging oder Anzeige einer Meldung
            // Beispiel: Fehler in Datei schreiben oder Dialog anzeigen
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Fehlerbehandlung, z.B. Logging oder Anzeige einer Meldung
            e.SetObserved();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell())
            {
                Width = 1400,
                Height = 1200
            };
            return window;
        }
    }
}