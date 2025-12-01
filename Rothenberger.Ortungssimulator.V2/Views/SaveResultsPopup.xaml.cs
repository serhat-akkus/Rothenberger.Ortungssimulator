using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;

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

            ContinueButton.Clicked += (_, __) =>
            {
                CloseAsync();
            };

            AdjustSizeForPath();
        }

        private void AdjustSizeForPath()
        {
            // Verfügbare Bildschirmbreite in Geräteeinheiten
            var displayInfo = DeviceDisplay.MainDisplayInfo;
            var maxScreenWidth = displayInfo.Width / displayInfo.Density - 40; // etwas Rand

            // Label messen (unbegrenzt)
            PathLabel.Measure(double.PositiveInfinity, double.PositiveInfinity);
            var desiredWidth = PathLabel.DesiredSize.Width + 32; // + Padding

            // Grenzen anwenden
            if (desiredWidth < 400) desiredWidth = 400;
            if (desiredWidth > maxScreenWidth) desiredWidth = maxScreenWidth;
            if (desiredWidth > 800) desiredWidth = 800; // MaxWidthRequest greifen lassen

            WidthRequest = desiredWidth;
            // Höhe wird durch ScrollView begrenzt, keine explizite HeightRequest nötig
        }
    }
}