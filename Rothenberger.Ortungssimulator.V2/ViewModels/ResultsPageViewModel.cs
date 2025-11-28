using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rothenberger.Ortungssimulator.V2.Models;
using Microsoft.Maui.ApplicationModel.DataTransfer;

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
            var date = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
            var arrowStr = ArrowEnabled ? "ja" : "nein";
            var fileName = $"{date}_{UserName}_{ToneType}_Hilfspfeil_{arrowStr}.csv";
            var csvLines = RunResults.Select(r =>
                $"{r.Name};{r.ToneType};{r.Timestamp:yyyy-MM-dd HH:mm:ss};{r.Time:mm\\:ss};{r.Distance:F2};{(r.ArrowEnabled ? "ja" : "nein")}");
            var csv = string.Join(Environment.NewLine, csvLines);
            var path = Path.Combine(FileSystem.AppDataDirectory, fileName);
            File.WriteAllText(path, csv);

            var page = Application.Current?.MainPage;
            if (page != null)
            {
                bool copy = await page.DisplayAlertAsync(
                    "Datei gespeichert",
                    $"Die Ergebnisse wurden gespeichert:\n{path}\n\nPfad in Zwischenablage kopieren?",
                    "Kopieren", "Weiter");

                if (copy)
                    await Clipboard.Default.SetTextAsync(path);

                await Shell.Current.Navigation.PopToRootAsync();
            }
        }
    }
}
