using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rothenberger.Ortungssimulator.V2.Models;
using Rothenberger.Ortungssimulator.V2.Views;
using CommunityToolkit.Maui.Views;
using System.IO;
using CommunityToolkit.Maui.Extensions;

namespace Rothenberger.Ortungssimulator.V2.ViewModels
{
    public partial class ResultsPageViewModel : ObservableObject
    {
        public ObservableCollection<TestRunResult> RunResults { get; }
        public string UserName { get; }
        public string ToneType { get; }
        public bool ArrowEnabled { get; }

        [ObservableProperty]
        private string averageTimeDisplay;

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

            UpdateAverageTimeDisplay();
        }

        private void UpdateAverageTimeDisplay()
        {
            if (RunResults.Count == 0)
            {
                AverageTimeDisplay = "Ø Zeit: -";
                return;
            }
            var avg = TimeSpan.FromMilliseconds(RunResults.Average(r => r.Time.TotalMilliseconds));
            AverageTimeDisplay = $"Ø Zeit: {avg:mm\\:ss\\.ff}";
        }

        private static string CleanCsvField(string value)
        {
            return value?.Replace(",", " ").Replace(";", " ") ?? string.Empty;
        }

        private async void SaveAndContinue()
        {
            var now = DateTime.Now;
            var date = now.ToString("yyMMdd", CultureInfo.InvariantCulture);
            var time = now.ToString("HHmmss", CultureInfo.InvariantCulture);
            var arrowStr = ArrowEnabled ? "ja" : "nein";
            var fileName = $"{date}_{time}_{CleanCsvField(UserName)}_" +
                $"{CleanCsvField(ToneType)}_Hilfspfeil_{arrowStr}.csv";

            // Header ergänzen
            const string header = "Name;Ton-Typ;Zeitstempel;Dauer (s);Distanz (m);Sondentiefe (m);Pfeil";

            var csvLines = RunResults.Select(r =>
                $"{CleanCsvField(r.Name)};{CleanCsvField(r.ToneType)};" +
                $"{r.Timestamp:yyyy-MM-dd HH:mm:ss};{r.Time:mm\\:ss\\.ff};" +
                $"{r.Distance:F2};{r.ProbeDepthMeters:F2};{(r.ArrowEnabled ? "ja" : "nein")}");

            // Durchschnitt berechnen
            string averageLine = "";
            if (RunResults.Count > 0)
            {
                var avg = TimeSpan.FromMilliseconds(RunResults.Average(r => r.Time.TotalMilliseconds));
                averageLine = Environment.NewLine + $"Durchschnittszeit:;{avg:mm\\:ss\\.ff}";
            }

            // CSV zusammenbauen: Header, Daten, Leerzeile, Durchschnitt
            var csv = header + Environment.NewLine
                      + string.Join(Environment.NewLine, csvLines)
                      + averageLine;

            string finalPath;
            try
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var projectDir = Path.Combine(documents, "Rothenberger.Ortungssimulator");
                Directory.CreateDirectory(projectDir);
                finalPath = Path.Combine(projectDir, fileName);
                File.WriteAllText(finalPath, csv);
            }
            catch
            {
                finalPath = Path.Combine(FileSystem.AppDataDirectory, fileName);
                File.WriteAllText(finalPath, csv);
            }

            var page = Application.Current?.MainPage;
            if (page != null)
            {
                var popup = new SaveResultsPopup(finalPath);
                await page.ShowPopupAsync(popup);
            }
        }
    }
}
