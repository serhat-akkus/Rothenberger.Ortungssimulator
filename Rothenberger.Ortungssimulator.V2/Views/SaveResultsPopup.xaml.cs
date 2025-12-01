using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel.DataTransfer;

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
                // Popup bleibt geöffnet
            };
            ContinueButton.Clicked += (_, __) =>
            {
                this.CloseAsync(); // signalisiert "Weiter"
            };
        }
    }
}