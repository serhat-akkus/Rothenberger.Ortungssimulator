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
            ShowGlobalError(e.ExceptionObject?.ToString());
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowGlobalError(e.Exception?.Message);
            e.SetObserved();
        }

        private void ShowGlobalError(string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Current.MainPage.DisplayAlertAsync("Fehler", message, "OK");
            });
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