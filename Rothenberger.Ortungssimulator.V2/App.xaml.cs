using Microsoft.Extensions.DependencyInjection;

namespace Rothenberger.Ortungssimulator.V2
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
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