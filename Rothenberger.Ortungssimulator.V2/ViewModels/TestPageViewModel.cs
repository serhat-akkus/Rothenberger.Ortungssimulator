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
        private bool isAudioActive;
        private double? lastFrequency; // letzte verwendete Frequenz merken

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

            sineProvider = new SineWaveProvider(sampleRate: 44100, frequency: 440.0, initialVolume: 0.0f);
            waveOut = new WaveOutEvent
            {
                DesiredLatency = 100
            };

            waveOut.Init(sineProvider);
            waveOut.Play();
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

            if (sineProvider != null)
            {
                sineProvider.Dispose();
                sineProvider = null;
            }
        }

        private void UpdateAudioForMousePosition()
        {
            if (!isAudioActive || sineProvider == null)
            {
                System.Diagnostics.Debug.WriteLine("[UpdateAudio] Audio inaktiv oder Provider null.");
                return;
            }

            double dxPx = CurrentMousePosition.X - ProbePosition.X;
            double dyPx = CurrentMousePosition.Y - ProbePosition.Y;
            double distancePx = Math.Sqrt(dxPx * dxPx + dyPx * dyPx);

            if (distancePx <= ProbeRadiusPixels)
            {
                double normalizedRaw = 1.0 - (distancePx / ProbeRadiusPixels);
                double normalized = Math.Max(0.0, Math.Min(1.0, normalizedRaw));
                double perceptual = Math.Pow(normalized, 0.8);

                sineProvider.SetVolume((float)perceptual);

                var minFreqOpt = selectedToneType?.MinFrequency;
                var maxFreqOpt = selectedToneType?.MaxFrequency;
                var toneTypeName = selectedToneType?.Id?.ToLowerInvariant();
                var logBaseStr = selectedToneType?.LogBase;

                System.Diagnostics.Debug.WriteLine(
                    $"[UpdateAudio] inside radius: DistPx={distancePx:F2}, normRaw={normalizedRaw:F4}, norm={normalized:F4}, vol={perceptual:F3}, " +
                    $"ToneType='{toneTypeName}', Min={minFreqOpt}, Max={maxFreqOpt}, LogBase='{logBaseStr}', lastFreq={lastFrequency}");

                switch (toneTypeName)
                {
                    case "sinus_linear":
                        if (minFreqOpt.HasValue && maxFreqOpt.HasValue && maxFreqOpt.Value != minFreqOpt.Value)
                        {
                            var freq = minFreqOpt.Value + (maxFreqOpt.Value - minFreqOpt.Value) * normalized;
                            lastFrequency = freq;
                            sineProvider.SetFrequency(freq);
                            System.Diagnostics.Debug.WriteLine($"[UpdateAudio][linear] freq={freq:F3} Hz (min={minFreqOpt.Value}, max={maxFreqOpt.Value}, norm={normalized:F4})");
                        }
                        else
                        {
                            if (!lastFrequency.HasValue && minFreqOpt.HasValue) lastFrequency = minFreqOpt.Value;
                            sineProvider.SetFrequency(lastFrequency ?? 0.0);
                            System.Diagnostics.Debug.WriteLine($"[UpdateAudio][linear] fallback freq={lastFrequency ?? 0.0:F3} Hz (ungültige min/max)");
                        }
                        break;

                    case "sinus_log":
                        if (minFreqOpt.HasValue && maxFreqOpt.HasValue && minFreqOpt.Value > 0 && maxFreqOpt.Value > minFreqOpt.Value)
                        {
                            double logBase;
                            if (string.Equals(logBaseStr, "e", StringComparison.OrdinalIgnoreCase))
                            {
                                logBase = Math.E;
                                System.Diagnostics.Debug.WriteLine("[UpdateAudio][log] LogBase='e' -> Math.E verwendet.");
                            }
                            else if (!double.TryParse(logBaseStr, NumberStyles.Float, CultureInfo.InvariantCulture, out logBase) || logBase <= 0 || Math.Abs(logBase - 1.0) < double.Epsilon)
                            {
                                logBase = Math.E; // Fallback
                                System.Diagnostics.Debug.WriteLine($"[UpdateAudio][log] ungültige LogBase='{logBaseStr}', Fallback auf e.");
                            }

                            double k = Math.Log(maxFreqOpt.Value / minFreqOpt.Value) / Math.Log(logBase);
                            double freq = minFreqOpt.Value * Math.Pow(logBase, normalized * k);

                            lastFrequency = freq;
                            sineProvider.SetFrequency(freq);
                            System.Diagnostics.Debug.WriteLine($"[UpdateAudio][log] k={k:F6}, freq={freq:F3} Hz (min={minFreqOpt.Value}, max={maxFreqOpt.Value}, base={logBase:F6}, norm={normalized:F4})");
                        }
                        else
                        {
                            if (!lastFrequency.HasValue && minFreqOpt.HasValue) lastFrequency = minFreqOpt.Value;
                            sineProvider.SetFrequency(lastFrequency ?? 0.0);
                            System.Diagnostics.Debug.WriteLine($"[UpdateAudio][log] fallback freq={lastFrequency ?? 0.0:F3} Hz (ungültige min/max)");
                        }
                        break;

                    default:
                        if (!lastFrequency.HasValue && minFreqOpt.HasValue) lastFrequency = minFreqOpt.Value;
                        sineProvider.SetFrequency(lastFrequency ?? 0.0);
                        System.Diagnostics.Debug.WriteLine($"[UpdateAudio][default] fallback freq={lastFrequency ?? 0.0:F3} Hz (ToneType unbekannt)");
                        break;
                }
            }
            else
            {
                sineProvider.SetVolume(0.0f);
                System.Diagnostics.Debug.WriteLine($"[UpdateAudio] outside radius: DistPx={distancePx:F2} > RadiusPx={ProbeRadiusPixels:F2}, vol=0.0, freq bleibt={lastFrequency}");
            }
        }
    }
}