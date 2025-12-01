using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rothenberger.Ortungssimulator.V2.Models;
using Rothenberger.Ortungssimulator.V2.Views;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Core;

namespace Rothenberger.Ortungssimulator.V2.ViewModels
{
    public partial class ResultsPageViewModel : ObservableObject
    {
        public ObservableCollection<TestRunResult> RunResults { get; }
        public string UserName { get; }
        public string ToneType { get; }
        public bool ArrowEnabled { get; }

        public IRelayCommand SaveAndContinueCommand { get; }

        public ResultsPageViewModel(
            ObservableCollection<TestRunResult> runResults,
            string userName,
            string toneType,
            bool arrowEnabled)
        {
            RunResults = runResults;
            UserName = userName;
            ToneType = toneType;
            ArrowEnabled = arrowEnabled;
            SaveAndContinueCommand = new RelayCommand(SaveAndContinue);
        }

        private async void SaveAndContinue()
        {
            var now = DateTime.Now;
            var date = now.ToString("yyMMdd", CultureInfo.InvariantCulture);
            var time = now.ToString("HHmmss", CultureInfo.InvariantCulture);
            var arrowStr = ArrowEnabled ? "ja" : "nein";
            var fileName = $"{date}_{time}_{UserName}_{ToneType}_Hilfspfeil_{arrowStr}.csv";
            var csvLines = RunResults.Select(r =>
                $"{r.Name};{r.ToneType};{r.Timestamp:yyyy-MM-dd HH:mm:ss};{r.Time:mm\\:ss};{r.Distance:F2};{(r.ArrowEnabled ? "ja" : "nein")}");
            var csv = string.Join(Environment.NewLine, csvLines);
            var path = Path.Combine(FileSystem.AppDataDirectory, fileName);
            File.WriteAllText(path, csv);

            var page = Application.Current?.MainPage;
            if (page != null)
            {
                var popup = new SaveResultsPopup(path);
                var result = await page.ShowPopupAsync(popup);
                if (result is IPopupResult popupResult && !popupResult.WasDismissedByTappingOutsideOfPopup)
                {
                    await Shell.Current.Navigation.PopToRootAsync();
                }
            }
        }
    }
}
