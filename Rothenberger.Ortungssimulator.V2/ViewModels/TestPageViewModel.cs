using System.Collections.ObjectModel;
using System.Windows.Input;
using Rothenberger.Ortungssimulator.V2.Models;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using Rothenberger.Ortungssimulator.V2.Views;
using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Audio;

namespace Rothenberger.Ortungssimulator.V2.ViewModels
{
    public partial class TestPageViewModel : ObservableObject
    {
        // Canvas: 1000 px Seitenlänge
        private const int CanvasSizePx = 1000;

        // 1000 Pixel entsprechen 10 m => 1 Pixel = 0,01 m
        private const double MeterPerPixel = 0.01;

        private const double ProbeRadiusMeters = 5.0;
        // 5 m => 5 / 0.01 = 500 px
        private const double ProbeRadiusPixels = ProbeRadiusMeters / MeterPerPixel; // 500 px

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
        [ObservableProperty] private Point currentMousePosition;

        public ObservableCollection<TestRunResult> RunResults { get; } = new();

        private int currentRun = 1;
        private int totalRuns = 3;
        private System.Timers.Timer timer;
        private DateTime runStartTime;
        private Point probeLogicalPosition;
        private bool arrowEnabled = false;
        private string userName = "";
        private ToneType selectedToneType;
        private IDispatcherTimer arrowUpdateTimer;

        // Audio: NAudio
        private IWavePlayer? waveOut;
        private SineWaveProvider? sineProvider;
        private ShepardToneProvider? shepardProvider;
        private bool isAudioActive;
        private double? lastFrequency; // letzte verwendete Frequenz merken
        private double? _previousDistanceMeters;
        private const double MovementEpsilonMeters = 0.01; // Schwelle für "still"

        public ICommand StartRunCommand { get; }
        public ICommand CanvasClickedCommand { get; }

        public ToneType SelectedToneType { get; private set; }

        public TestPageViewModel()
        {
            StartRunCommand = new RelayCommand(StartRun);
            CanvasClickedCommand = new RelayCommand<Point>(OnCanvasClicked);
        }

        public void Initialize(string name, ToneType selectedToneType, int runCount, bool arrow)
        {
            userName = name;
            SelectedToneType = selectedToneType;
            this.selectedToneType = selectedToneType;
            totalRuns = runCount;
            arrowEnabled = arrow;
            startRunButtonText = $"Starte Test-Run {currentRun}";
        }

        private void StartRun()
        {
            IsRunNotStarted = false;
            IsArrowBoxVisible = arrowEnabled;
            IsProbeVisible = false;
            IsClickPointVisible = false;
            DistanceText = "";
            TimerText = "00:00";

            var rnd = new Random();
            // Sonde innerhalb des 1000px-Canvas zufällig platzieren
            probeLogicalPosition = new Point(rnd.NextDouble() * CanvasSizePx, rnd.NextDouble() * CanvasSizePx);
            ProbePosition = probeLogicalPosition;

            timer = new System.Timers.Timer(1000);
            runStartTime = DateTime.Now;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();

            if (arrowEnabled)
            {
                arrowUpdateTimer = Dispatcher.GetForCurrentThread().CreateTimer();
                arrowUpdateTimer.Interval = TimeSpan.FromMilliseconds(50);
                arrowUpdateTimer.Tick += ArrowUpdateTimer_Tick;
                arrowUpdateTimer.Start();
            }

            InitializeAudio();
            isAudioActive = true;

            // Initiale Frequenz setzen: MinFrequency (falls vorhanden), sonst letzte Frequenz, sonst 0 Hz
            var minFreqInit = selectedToneType?.MinFrequency;
            lastFrequency = minFreqInit ?? lastFrequency ?? 0.0;
            sineProvider?.SetFrequency(lastFrequency.Value);

            UpdateAudioForMousePosition();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var elapsed = DateTime.Now - runStartTime;
            TimerText = elapsed.ToString(@"mm\:ss");
        }

        private void ArrowUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateArrowToProbe();
            UpdateAudioForMousePosition();
        }

        private void StopArrowTimer()
        {
            if (arrowUpdateTimer != null)
            {
                arrowUpdateTimer.Stop();
                arrowUpdateTimer.Tick -= ArrowUpdateTimer_Tick;
                arrowUpdateTimer = null;
            }
        }

        public void OnCanvasClicked(Point mousePosition)
        {
            if (IsRunNotStarted) return;
            timer?.Stop();
            StopArrowTimer();

            StopAudio();

            IsClickPointVisible = true;
            var clamped = ClampToCanvas(mousePosition);
            ClickPointPosition = clamped;
            IsProbeVisible = true;

            var dx = (clamped.X - probeLogicalPosition.X) * MeterPerPixel;
            var dy = (clamped.Y - probeLogicalPosition.Y) * MeterPerPixel;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            DistanceText = $"Distanz: {dist:F2} m";

            RunResults.Add(new TestRunResult
            {
                Name = userName,
                ToneType = selectedToneType.Name,
                Timestamp = DateTime.Now,
                Time = DateTime.Now - runStartTime,
                Distance = dist,
                ArrowEnabled = arrowEnabled
            });

            if (currentRun >= totalRuns)
            {
                ShowResultsAndSave();
            }
            else
            {
                currentRun++;
                StartRunButtonText = $"Starte Test-Run {currentRun}";
                IsRunNotStarted = true;
                CanvasBackground = Colors.LightGray;
                IsArrowBoxVisible = false;
            }
        }

        private async void ShowResultsAndSave()
        {
            var date = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
            var arrowStr = arrowEnabled ? "ja" : "nein";
            var fileName = $"{date}_{userName}_{selectedToneType?.Id}_Hilfspfeil_{arrowStr}.csv";
            var csvLines = RunResults.Select(r =>
                $"{r.Name};{selectedToneType?.Id};{r.Timestamp:yyyy-MM-dd HH:mm:ss};{r.Time:mm\\:ss};{r.Distance:F2};{(r.ArrowEnabled ? "ja" : "nein")}");
            var csv = string.Join(Environment.NewLine, csvLines);
            var path = Path.Combine(FileSystem.AppDataDirectory, fileName);
            File.WriteAllText(path, csv);

            var resultsPageVm = new ResultsPageViewModel(RunResults, userName, selectedToneType?.Id, arrowEnabled);
            var resultsPage = new ResultsPage();
            resultsPage.BindingContext = resultsPageVm;
            await Shell.Current.Navigation.PushAsync(resultsPage);
        }

        public void UpdateArrowToProbe()
        {
            double dx = ProbePosition.X - CurrentMousePosition.X;
            double dy = ProbePosition.Y - CurrentMousePosition.Y;
            ArrowRotation = Math.Atan2(dy, dx) * 180 / Math.PI;
        }

        // ---- Audio-Logik (NAudio) ----

        public void UpdateCurrentMousePosition(Point mousePosition)
        {
            CurrentMousePosition = ClampToCanvas(mousePosition);

            if (!IsRunNotStarted)
            {
                UpdateAudioForMousePosition();
            }
        }

        private static Point ClampToCanvas(Point p)
        {
            var x = Math.Min(Math.Max(p.X, 0), CanvasSizePx);
            var y = Math.Min(Math.Max(p.Y, 0), CanvasSizePx);
            return new Point(x, y);
        }

        private void InitializeAudio()
        {
            StopAudio();

            waveOut = new WaveOutEvent
            {
                DesiredLatency = 100
            };

            var toneId = selectedToneType?.Id?.ToLowerInvariant();
            if (toneId == "shepard")
            {
                // Shepard: Start ohne Richtung (none)
                shepardProvider = new ShepardToneProvider(
                    baseFreq: 110f,
                    direction: "none",
                    duration: 6f,
                    sampleRate: 44100,
                    numComponents: 8);
                shepardProvider.SetGain(0f);
                waveOut.Init(shepardProvider);
            }
            else
            {
                shepardProvider = null;
                sineProvider = new SineWaveProvider(sampleRate: 44100, frequency: 440.0, initialVolume: 0.0f);
                waveOut.Init(sineProvider);
            }

            waveOut.Play();
            _previousDistanceMeters = null;
        }

        private void StopAudio()
        {
            isAudioActive = false;

            if (waveOut != null)
            {
                try { waveOut.Stop(); } catch { }
                waveOut.Dispose();
                waveOut = null;
            }

            sineProvider?.Dispose();
            sineProvider = null;
            shepardProvider = null;
        }

        private void UpdateAudioForMousePosition()
        {
            if (!isAudioActive)
                return;

            double dxPx = CurrentMousePosition.X - ProbePosition.X;
            double dyPx = CurrentMousePosition.Y - ProbePosition.Y;
            double distancePx = Math.Sqrt(dxPx * dxPx + dyPx * dyPx);
            double distanceMeters = distancePx * MeterPerPixel;

            var toneId = selectedToneType?.Id?.ToLowerInvariant();

            if (toneId == "shepard")
            {
                if (shepardProvider == null) return;

                if (distanceMeters <= ProbeRadiusMeters)
                {
                    // Normalisierung für Lautstärke (näher => lauter)
                    double normalized = 1.0 - (distanceMeters / ProbeRadiusMeters);
                    normalized = Math.Clamp(normalized, 0.0, 1.0);

                    shepardProvider.SetGain((float)Math.Pow(normalized, 0.8));

                    // Bewegungsrichtung bestimmen
                    if (_previousDistanceMeters.HasValue)
                    {
                        double delta = _previousDistanceMeters.Value - distanceMeters;
                        string direction;
                        if (Math.Abs(delta) < MovementEpsilonMeters)
                            direction = "none";
                        else if (delta > 0)
                            direction = "up";   // Annäherung -> steigende Illusion
                        else
                            direction = "down"; // Entfernen -> fallende Illusion

                        shepardProvider.SetDirection(direction);
                    }

                    _previousDistanceMeters = distanceMeters;
                }
                else
                {
                    // außerhalb: Lautstärke aus
                    shepardProvider.SetGain(0f);
                    shepardProvider.SetDirection("none");
                    _previousDistanceMeters = null;
                }

                return; // Shepard komplett behandelt, anderen Code überspringen
            }

            // --- Bisherige Sinus-Logik ---
            if (sineProvider == null) return;

            if (distanceMeters <= ProbeRadiusMeters)
            {
                double normalized = 1.0 - (distanceMeters / ProbeRadiusMeters);
                normalized = Math.Clamp(normalized, 0.0, 1.0);
                double perceptual = Math.Pow(normalized, 0.8);
                sineProvider.SetVolume((float)perceptual);

                var minFreqOpt = selectedToneType?.MinFrequency;
                var maxFreqOpt = selectedToneType?.MaxFrequency;

                switch (toneId)
                {
                    case "sinus_linear":
                        if (minFreqOpt.HasValue && maxFreqOpt.HasValue && maxFreqOpt.Value > minFreqOpt.Value)
                        {
                            double freq = minFreqOpt.Value + (maxFreqOpt.Value - minFreqOpt.Value) * normalized;
                            lastFrequency = freq;
                            sineProvider.SetFrequency(freq);
                        }
                        else
                        {
                            if (!lastFrequency.HasValue && minFreqOpt.HasValue)
                                lastFrequency = minFreqOpt.Value;
                            sineProvider.SetFrequency(lastFrequency ?? 0.0);
                        }
                        break;

                    case "sinus_log":
                        if (minFreqOpt.HasValue && maxFreqOpt.HasValue && maxFreqOpt.Value > minFreqOpt.Value && minFreqOpt.Value > 0)
                        {
                            double logBase;
                            var logBaseStr = selectedToneType?.LogBase;
                            if (string.Equals(logBaseStr, "e", StringComparison.OrdinalIgnoreCase))
                                logBase = Math.E;
                            else if (!double.TryParse(logBaseStr, System.Globalization.NumberStyles.Float,
                                     System.Globalization.CultureInfo.InvariantCulture, out logBase) ||
                                     logBase <= 0.0 || Math.Abs(logBase - 1.0) < double.Epsilon)
                                logBase = Math.E;

                            double k = Math.Log(maxFreqOpt.Value / minFreqOpt.Value) / Math.Log(logBase);
                            double freq = minFreqOpt.Value * Math.Pow(logBase, normalized * k);
                            lastFrequency = freq;
                            sineProvider.SetFrequency(freq);
                        }
                        else
                        {
                            if (!lastFrequency.HasValue && minFreqOpt.HasValue)
                                lastFrequency = minFreqOpt.Value;
                            sineProvider.SetFrequency(lastFrequency ?? 0.0);
                        }
                        break;

                    case "tonleiter_diskret":
                        {
                            var list = selectedToneType?.Frequencies;
                            if (list != null && list.Count > 0)
                            {
                                double stepMeters = 0.2;
                                int index = (int)Math.Floor(distanceMeters / stepMeters);
                                if (index < 0) index = 0;
                                if (index >= list.Count) index = list.Count - 1;
                                double freq = list[index];
                                lastFrequency = freq;
                                sineProvider.SetFrequency(freq);
                            }
                            else
                            {
                                sineProvider.SetFrequency(lastFrequency ?? 0.0);
                            }
                            break;
                        }

                    default:
                        if (!lastFrequency.HasValue && minFreqOpt.HasValue)
                            lastFrequency = minFreqOpt.Value;
                        sineProvider.SetFrequency(lastFrequency ?? 0.0);
                        break;
                }
            }
            else
            {
                sineProvider.SetVolume(0.0f);
            }
        }
    }
}