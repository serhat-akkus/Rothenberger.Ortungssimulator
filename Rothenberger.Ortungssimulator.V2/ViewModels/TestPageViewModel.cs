using System.Collections.ObjectModel;
using System.Windows.Input;
using Rothenberger.Ortungssimulator.V2.Models;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using Rothenberger.Ortungssimulator.V2.Views;

namespace Rothenberger.Ortungssimulator.V2.ViewModels
{
    public partial class TestPageViewModel : ObservableObject
    {
        private const int CanvasSizePx = 1000;
        private const double MeterPerPixel = 0.01;

        [ObservableProperty] private string timerText = "00:00";
        [ObservableProperty] private string distanceText = "";
        [ObservableProperty] private string startRunButtonText = "Starte Test-Run 1";
        [ObservableProperty] private bool isRunNotStarted = true;
        [ObservableProperty] private bool isProbeVisible = false;
        [ObservableProperty] private bool isClickPointVisible = false;
        [ObservableProperty] private bool isArrowBoxVisible = false;
        [ObservableProperty] private Color canvasBackground = Colors.LightGray;
        [ObservableProperty] private Point probePosition;
        [ObservableProperty] private Point clickPointPosition;
        [ObservableProperty] private double arrowRotation = 0;

        public ObservableCollection<TestRunResult> RunResults { get; } = new();

        private int currentRun = 1;
        private int totalRuns = 3; // Beispielwert, kann übergeben werden
        private System.Timers.Timer timer;
        private DateTime runStartTime;
        private Point probeLogicalPosition;
        private bool arrowEnabled = false;
        private string userName = "";
        private string toneType = "";

        public ICommand StartRunCommand { get; }
        public ICommand CanvasClickedCommand { get; }

        public TestPageViewModel()
        {
            StartRunCommand = new RelayCommand(StartRun);
            CanvasClickedCommand = new RelayCommand<Point>(OnCanvasClicked);
        }

        public void Initialize(string name, string toneType, int runCount, bool arrow)
        {
            userName = name;
            this.toneType = toneType;
            totalRuns = runCount; // Wert wird gesetzt
            arrowEnabled = arrow;
            startRunButtonText = $"Starte Test-Run {currentRun}";
        }

        private void StartRun()
        {
            IsRunNotStarted = false;
            IsArrowBoxVisible = arrowEnabled;
            IsProbeVisible = false;      // Sonde ausblenden
            IsClickPointVisible = false; // Klickpunkt ausblenden
            DistanceText = "";
            TimerText = "00:00";
            // Zufällige Sonde platzieren
            var rnd = new Random();
            probeLogicalPosition = new Point(rnd.NextDouble() * CanvasSizePx, rnd.NextDouble() * CanvasSizePx);
            ProbePosition = probeLogicalPosition;
            // Timer starten
            timer = new System.Timers.Timer(1000);
            runStartTime = DateTime.Now;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var elapsed = DateTime.Now - runStartTime;
            TimerText = elapsed.ToString(@"mm\:ss");
        }

        public void OnCanvasClicked(Point mousePosition)
        {
            if (IsRunNotStarted) return;
            timer?.Stop();
            IsClickPointVisible = true;
            ClickPointPosition = mousePosition;
            IsProbeVisible = true;
            // Distanz berechnen
            var dx = (mousePosition.X - probeLogicalPosition.X) * MeterPerPixel;
            var dy = (mousePosition.Y - probeLogicalPosition.Y) * MeterPerPixel;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            DistanceText = $"Distanz: {dist:F2} m";
            // Ergebnis speichern
            RunResults.Add(new TestRunResult
            {
                Name = userName,
                ToneType = toneType,
                Timestamp = DateTime.Now,
                Time = DateTime.Now - runStartTime,
                Distance = dist,
                ArrowEnabled = arrowEnabled
            });
            // Run abschließen
            if (currentRun >= totalRuns)
            {
                // Ergebnisse anzeigen und speichern
                ShowResultsAndSave();
            }
            else
            {
                currentRun++;
                StartRunButtonText = $"Starte Test-Run {currentRun}";
                IsRunNotStarted = true; // Button erscheint wieder
                CanvasBackground = Colors.LightGray;
                IsArrowBoxVisible = false;
            }
        }

        private async void ShowResultsAndSave()
        {
            // CSV speichern
            var date = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
            var arrowStr = arrowEnabled ? "ja" : "nein";
            var fileName = $"{date}_{userName}_{toneType}_Hilfspfeil_{arrowStr}.csv";
            var csvLines = RunResults.Select(r =>
                $"{r.Name};{r.ToneType};{r.Timestamp:yyyy-MM-dd HH:mm:ss};{r.Time:mm\\:ss};{r.Distance:F2};{(r.ArrowEnabled ? "ja" : "nein")}");
            var csv = string.Join(Environment.NewLine, csvLines);
            var path = Path.Combine(FileSystem.AppDataDirectory, fileName);
            File.WriteAllText(path, csv);
            // Navigation zur Ergebnis-Seite
            var resultsPageVm = new ResultsPageViewModel(RunResults, userName, toneType, arrowEnabled);
            var resultsPage = new ResultsPage();
            resultsPage.BindingContext = resultsPageVm;
            await Shell.Current.Navigation.PushAsync(resultsPage);
        }

        // Pfeil-Rotation berechnen
        public void UpdateArrow(Point mousePosition)
        {
            if (!arrowEnabled || isRunNotStarted) return;
            var dx = probeLogicalPosition.X - mousePosition.X;
            var dy = probeLogicalPosition.Y - mousePosition.Y;
            arrowRotation = Math.Atan2(dy, dx) * 180 / Math.PI;
        }
    }
}