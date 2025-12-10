using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Controls;

namespace Rothenberger.Ortungssimulator.V2.Views
{
    public partial class SaveResultsPopup : Popup
    {
        private readonly string _path;

        public SaveResultsPopup(string path)
        {
            InitializeComponent();
            _path = path;
            PathLabel.Text = $"Die Ergebnisse wurden gespeichert:\n{_path}";

            CopyButton.Clicked += async (_, __) =>
            {
                await Clipboard.Default.SetTextAsync(_path);
            };

            // "Weiter" soll zur Startseite navigieren
            ContinueButton.Clicked += async (_, __) =>
            {
                // Popup sauber schließen
                await CloseAsync();

                // Zur Startseite zurück (Root der Navigation)
                if (Shell.Current != null)
                {
                    await Shell.Current.Navigation.PopToRootAsync();
                }
            };

            AdjustSizeForPath();
        }

        private void AdjustSizeForPath()
        {
            var displayInfo = DeviceDisplay.MainDisplayInfo;
            var maxScreenWidth = displayInfo.Width / displayInfo.Density - 40;

            PathLabel.Measure(double.PositiveInfinity, double.PositiveInfinity);
            var desiredWidth = PathLabel.DesiredSize.Width + 32;

            if (desiredWidth < 400) desiredWidth = 400;
            if (desiredWidth > maxScreenWidth) desiredWidth = maxScreenWidth;
            if (desiredWidth > 800) desiredWidth = 800;

            WidthRequest = desiredWidth;
        }
    }
}