using CommunityToolkit.Maui.Views;

namespace Rothenberger.Ortungssimulator.V2.Views
{
    public partial class RunsCompletedPopup : Popup
    {
        public RunsCompletedPopup()
        {
            InitializeComponent();
            ContinueButton.Clicked += (_, __) => CloseAsync();
        }
    }
}