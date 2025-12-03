using System.Collections.ObjectModel;
using System.Windows.Input;
using Rothenberger.Ortungssimulator.V2.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rothenberger.Ortungssimulator.V2.Views;
using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Audio;
using Microsoft.Maui.ApplicationModel;

namespace Rothenberger.Ortungssimulator.V2.ViewModels
{
    public partial class TestPageViewModel : ObservableObject
    {
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
        [ObservableProperty] private bool isRunsCompletedVisible;
        [ObservableProperty] private double probeDepthMeters;
        [ObservableProperty] private string userNameDisplay = "";
        [ObservableProperty] private string toneTypeDisplay = "";

        [ObservableProperty] private int shepardCycleCount;
        [ObservableProperty] private double currentShepardFrequency;
        [ObservableProperty] private double shepardProgress;

        public ObservableCollection<TestRunResult> RunResults { get; } = new();

        private int currentRun = 1;
        private int totalRuns = 3;
        private DateTime runStartTime;
        private Point probeLogicalPosition;
        private bool arrowEnabled = false;
        private string userName = "";
        private ToneType selectedToneType;

        private IWavePlayer? waveOut;
        private SineWaveProvider? sineProvider;
        private bool isAudioActive;
        private double? lastFrequency;

        private IWavePlayer? shepardSweepWaveOut;
        private ShepardToneSweepProvider? shepardSweepProvider;

        private IDispatcherTimer? _uiTimer;

        // Commands
        public ICommand StartRunCommand { get; }
        public ICommand CanvasClickedCommand { get; }
        public IAsyncRelayCommand ContinueRunsCompletedCommand { get; }
        public ICommand PlayShepardSweepProviderCommand { get; }
        public ICommand StopShepardSweepProviderCommand { get; }

        public ToneType SelectedToneType { get; private set; }

        private readonly Graphics.CanvasHelper _canvas = new Graphics.CanvasHelper();
        private readonly ArrowHelper _arrowHelper = new ArrowHelper();

        private bool _anyAudioActive;

        public TestPageViewModel()
        {
            StartRunCommand = new RelayCommand(StartRun);
            CanvasClickedCommand = new RelayCommand<Point>(OnCanvasClicked);
            ContinueRunsCompletedCommand = new AsyncRelayCommand(OnContinueRunsCompletedAsync);

            // Sweep (Streaming-Provider)
            PlayShepardSweepProviderCommand = new RelayCommand(PlayShepardSweepProvider, () => CanStartShepard);
            StopShepardSweepProviderCommand = new RelayCommand(StopShepardSweepProvider, () => shepardSweepWaveOut != null);
        }

        public double EffectiveProbeRadiusMeters => _canvas.EffectiveProbeRadiusMeters;
        public double ProbeRadiusPixels => _canvas.ProbeRadiusPixels;

        public void Initialize(string name, ToneType selectedToneType, int runCount, bool arrow, double probeDepthMeters)
        {
            userName = name;
            SelectedToneType = selectedToneType;
            this.selectedToneType = selectedToneType;
            totalRuns = runCount;
            arrowEnabled = arrow;
            ProbeDepthMeters = probeDepthMeters;
            _canvas.SetDepth(ProbeDepthMeters);
            startRunButtonText = $"Starte Test-Run {currentRun}";
            OnPropertyChanged(nameof(EffectiveProbeRadiusMeters));
            OnPropertyChanged(nameof(ProbeRadiusPixels));
            UserNameDisplay = userName;
            ToneTypeDisplay = selectedToneType?.Name ?? "";
        }

        private void UpdateAudioForMousePosition()
        {
            if (!isAudioActive || sineProvider == null)
                return;

            // Position und Distanz holen
            double distanceMeters = _canvas.CalculateDistanceMeters(ProbePosition, CurrentMousePosition);
            double normalized = _canvas.NormalizeDistance(distanceMeters);

            // Kleines Epsilon tolerieren, um "0.0" Grenzfälle zu vermeiden
            const double eps = 1e-6;
            var toneId = selectedToneType?.Id?.ToLowerInvariant();

            // Fallback bei fehlenden ToneType/Id: hörbar machen
            if (string.IsNullOrWhiteSpace(toneId))
            {
                sineProvider.SetVolume(0.6f);
                sineProvider.SetFrequency(440.0);
                lastFrequency = 440.0;
                return;
            }

            // Lautstärke-Bedingung nicht zu streng, kurze Hörbarkeit auch bei sehr kleinen normalized
            if (distanceMeters <= 10.0 && normalized > eps)
            {
                // reguläre Pfade
                lastFrequency = toneId switch
                {
                    "sinus_linear" => AudioUpdateHelper.HandleSinusLinear(sineProvider, selectedToneType, normalized, distanceMeters, lastFrequency),
                    "sinus_log" => AudioUpdateHelper.HandleSinusLog(sineProvider, selectedToneType, normalized, distanceMeters, lastFrequency),
                    "tonleiter_diskret" => AudioUpdateHelper.HandleTonleiterDiskret(sineProvider, selectedToneType, normalized, distanceMeters, lastFrequency),
                    _ => AudioUpdateHelper.HandleDefault(sineProvider),
                };
            }
            else
            {
                // Stumm schalten außerhalb Bereich
                sineProvider.SetVolume(0.0f);
            }
        }

        public void UpdateProbeDepth(double newDepth)
        {
            ProbeDepthMeters = Math.Clamp(newDepth, 0.0, _canvas.MaxDepthMeters);
            _canvas.SetDepth(ProbeDepthMeters);
            OnPropertyChanged(nameof(EffectiveProbeRadiusMeters));
            OnPropertyChanged(nameof(ProbeRadiusPixels));
            if (!IsRunNotStarted)
            {
                UpdateAudioForMousePosition();
            }
        }

        // Sweep mit Streaming-Provider
        private void StartRun()
        {
            // sicherstellen: keine Shepard-Ausgabe aktiv
            StopShepardSweepProvider();

            IsRunNotStarted = false;
            IsArrowBoxVisible = arrowEnabled;
            IsProbeVisible = false;
            IsClickPointVisible = false;
            DistanceText = "";
            TimerText = "00:00";

            var rnd = new Random();
            probeLogicalPosition = _canvas.GetRandomProbePosition(rnd);
            ProbePosition = probeLogicalPosition;

            _uiTimer = Application.Current?.Dispatcher.CreateTimer();
            if (_uiTimer != null)
            {
                runStartTime = DateTime.Now;
                _uiTimer.Interval = TimeSpan.FromSeconds(1);
                _uiTimer.Tick += (_, _) =>
                {
                    var elapsed = DateTime.Now - runStartTime;
                    TimerText = elapsed.ToString(@"mm\:ss");
                };
                _uiTimer.Start();
            }

            if (arrowEnabled)
            {
                _arrowHelper.Start(
                    Dispatcher.GetForCurrentThread(),
                    TimeSpan.FromMilliseconds(50),
                    () => ProbePosition,
                    () => CurrentMousePosition,
                    rot => ArrowRotation = rot,
                    () => UpdateAudioForMousePosition());
            }

            InitializeAudio();
            isAudioActive = true;

            var minFreqInit = selectedToneType?.MinFrequency;
            lastFrequency = minFreqInit ?? lastFrequency ?? 0.0;
            sineProvider?.SetFrequency(lastFrequency.Value);
            UpdateAudioForMousePosition();
        }

        private void StopArrowTimer() => _arrowHelper.Stop();

        public void OnCanvasClicked(Point mousePosition)
        {
            if (IsRunNotStarted) return;

            _uiTimer?.Stop();
            _uiTimer = null;
            StopArrowTimer();
            StopAudio();

            IsClickPointVisible = true;
            var clamped = _canvas.ClampToCanvas(mousePosition);
            ClickPointPosition = clamped;
            IsProbeVisible = true;

            var dist = _canvas.CalculateDistanceMeters(probeLogicalPosition, clamped);
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
                ShowResults();
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

        private void ShowResults() => IsRunsCompletedVisible = true;

        private async Task OnContinueRunsCompletedAsync()
        {
            IsRunsCompletedVisible = false;
            var resultsPageVm = new ResultsPageViewModel(RunResults, userName, selectedToneType?.Id, arrowEnabled);
            var resultsPage = new ResultsPage { BindingContext = resultsPageVm };
            await Shell.Current.Navigation.PushAsync(resultsPage);
        }

        public void UpdateArrowToProbe()
        {
            double dx = ProbePosition.X - CurrentMousePosition.X;
            double dy = ProbePosition.Y - CurrentMousePosition.Y;
            ArrowRotation = Math.Atan2(dy, dx) * 180 / Math.PI;
        }

        public void UpdateCurrentMousePosition(Point mousePosition)
        {
            CurrentMousePosition = _canvas.ClampToCanvas(mousePosition);
            if (!IsRunNotStarted)
            {
                UpdateAudioForMousePosition();
            }
        }

        private void InitializeAudio()
        {
            StopAudio();
            _anyAudioActive = true;
            waveOut = new WaveOutEvent { DesiredLatency = 100 };
            sineProvider = new SineWaveProvider(sampleRate: 44100, frequency: 440.0, initialVolume: 0.0f);
            waveOut.Init(sineProvider);

            // WICHTIG: Output-Lautstärke explizit setzen
            if (waveOut is WaveOutEvent wo)
                wo.Volume = 1.0f;

            waveOut.Play();
        }

        private void StopAudio()
        {
            _anyAudioActive = false;
            isAudioActive = false;
            if (waveOut != null)
            {
                try { waveOut.Stop(); } catch { }
                waveOut.Dispose();
                waveOut = null;
            }
            sineProvider?.Dispose();
            sineProvider = null;
        }

        private void PlayShepardSweepProvider()
        {
            if (_anyAudioActive) StopAudio();
            StopShepardSweepProvider();
            _anyAudioActive = true;

            int sampleRate = 44100;
            int mMax = 12;
            int nMax = 7;
            double fMin = 70.0;
            double sigma = 0.5;
            double semitonesPerSecond = 1.5;
            double octaveOffset = 0.0;

            ShepardCycleCount = 0;
            ShepardProgress = 0;
            CurrentShepardFrequency = fMin;

            shepardSweepProvider = new ShepardToneSweepProvider(
                sampleRate,
                mMax,
                nMax,
                fMin,
                sigma,
                semitonesPerSecond,
                cycleCount => MainThread.BeginInvokeOnMainThread(() => ShepardCycleCount = cycleCount),
                (freq, progress) => MainThread.BeginInvokeOnMainThread(() =>
                {
                    CurrentShepardFrequency = freq;
                    ShepardProgress = progress;
                }),
                octaveOffset);

            shepardSweepWaveOut = new WaveOutEvent { DesiredLatency = 100 };
            shepardSweepWaveOut.Init(shepardSweepProvider);
            shepardSweepWaveOut.Volume = 0.2f;
            shepardSweepWaveOut.Play();

            (StopShepardSweepProviderCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }

        private void StopShepardSweepProvider()
        {
            shepardSweepProvider?.RequestStop();
            try
            {
                shepardSweepWaveOut?.Volume = 0.0f;
                shepardSweepWaveOut?.Pause();
                shepardSweepWaveOut?.Stop();
                shepardSweepWaveOut?.Dispose();
            }
            catch { }

            shepardSweepWaveOut = null;
            shepardSweepProvider?.Dispose();
            shepardSweepProvider = null;

            ShepardProgress = 0;
            CurrentShepardFrequency = 0;
            (StopShepardSweepProviderCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }

        public bool CanStartShepard => IsRunNotStarted; // oder ein eigener Zustand
    }
}