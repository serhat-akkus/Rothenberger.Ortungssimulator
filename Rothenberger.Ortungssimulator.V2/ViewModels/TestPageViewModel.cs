using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Audio;
using Rothenberger.Ortungssimulator.V2.Models;
using Rothenberger.Ortungssimulator.V2.Views;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Rothenberger.Ortungssimulator.V2.ViewModels
{
    public partial class TestPageViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty] private string timerText = "00:00";
        [ObservableProperty] private string distanceText = "";
        [ObservableProperty] private string startRunButtonText = "Starte Test-Run 1";
        [ObservableProperty] private bool isRunNotStarted = true;
        [ObservableProperty] private bool isProbeVisible = false;
        [ObservableProperty] private bool isProberadiusEnabled = false;
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
        [ObservableProperty] private double currentFrequency;

        public ObservableCollection<TestRunResult> RunResults { get; } = new();

        private const float VOLUME = 0.5f;
        private const double MeterPerPixel = 0.01;
        private const double ProbeRadius3d = 5.0;
        private const double MaxDepthMeters = 3.0;
        private int currentRun = 1;
        private int totalRuns = 3;
        private DateTime runStartTime;
        private Point probeLogicalPosition;
        private bool arrowEnabled = false;
        private string userName = "";
        private ToneType selectedToneType;

        private bool isAudioActive;
        private double? lastFrequency;

        private IDispatcherTimer? _uiTimer;
        private RunTimerService? _runTimerService;

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

        private SineWaveAudioManager? sineAudioManager;
        private ShepardMouseAudioManager? shepardMouseAudioManager;
        private ShepardSweepAudioManager? _shepardSweepAudioManager;

        private bool _isEingeklinkt = false;

        private EinklinkAudioManager? einklinkAudioManager;

        public TestPageViewModel()
        {
            StartRunCommand = new RelayCommand(StartRun);
            CanvasClickedCommand = new RelayCommand<Point>(OnCanvasClicked);
            ContinueRunsCompletedCommand = new AsyncRelayCommand(OnContinueRunsCompletedAsync);

            // Sweep (Streaming-Provider)
            PlayShepardSweepProviderCommand = new RelayCommand(PlayShepardSweepProvider, () => CanStartShepard);
            StopShepardSweepProviderCommand = new RelayCommand(StopShepardSweepProvider);
        }

        public double ProbeRadiusMetersIn2d => CalculateProbeRadiusMetersIn2d(ProbeRadius3d, ProbeDepthMeters);
        public double ProbeRadiusPixelsIn2d => ProbeRadiusMetersIn2d / MeterPerPixel;
        public double EinklinkRadiusMeters => ProbeRadiusMetersIn2d / 20.0;
        public double EinklinkRadiusPixels => ProbeRadiusPixelsIn2d / 20.0;

        public void Initialize(string name, ToneType selectedToneType, int runCount, bool arrow, bool showProbeRadius, double probeDepthMeters)
        {
            userName = name;
            SelectedToneType = selectedToneType;
            this.selectedToneType = selectedToneType;
            totalRuns = runCount;
            arrowEnabled = arrow;
            IsProberadiusEnabled = showProbeRadius;
            ProbeDepthMeters = probeDepthMeters;
            startRunButtonText = $"Starte Test-Run {currentRun}";
            OnPropertyChanged(nameof(ProbeRadiusMetersIn2d));
            OnPropertyChanged(nameof(ProbeRadiusPixelsIn2d));
            UserNameDisplay = userName;
            ToneTypeDisplay = selectedToneType?.Name ?? "";

            einklinkAudioManager = new EinklinkAudioManager();
        }

        private double CalculateProbeRadiusMetersIn2d(double ProbeRadiusMeters, double DepthMeters)
        {
            var R = ProbeRadiusMeters;
            var z = DepthMeters;
            var sq = R * R - z * z;
            if (sq <= 0) return 0.0;
            return Math.Sqrt(sq);
        }

        public void UpdateAudioForMousePosition()
        {
            if (!isAudioActive)
                return;

            if (CurrentMousePosition.X == -1 && CurrentMousePosition.Y == -1)
            {
                sineAudioManager?.Pause();
                shepardMouseAudioManager?.Pause();
                return;
            }

            double horiz2dMeters = Distance3DHelper.CalculateDistanceMeters(ProbePosition, CurrentMousePosition, MeterPerPixel);

            // --- EINKLINK-LOGIK --- (MUSS VOR ALLEM ANDEREN STEHEN!)
            _isEingeklinkt = ProcessEinklinkAusklink(horiz2dMeters, _isEingeklinkt);

            var toneId = selectedToneType?.Type?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(toneId))
            {
                if (sineAudioManager != null)
                {
                    sineAudioManager.Play();
                    lastFrequency = 440.0;
                }
                return;
            }

            double distance3dMouseToProbe = Distance3DHelper.Compute3dDistanceToProbe(horiz2dMeters, ProbeDepthMeters);
            bool isInRadius = distance3dMouseToProbe <= _canvas.ProbeRadiusMeters;
            double normalizedDistance3d = Distance3DHelper.CalculateProximity3D(ProbeRadius3d, distance3dMouseToProbe);

            if (toneId == "shepard")
            {
                // Shepard-Ton nur innerhalb des 2D-Radius erzeugen!
                if (isInRadius)
                {

                    shepardMouseAudioManager?.UpdateDistance3DState(normalizedDistance3d);
                    shepardMouseAudioManager?.Play();
                }
                else
                {
                    shepardMouseAudioManager?.Pause();
                }

                lastFrequency = 0.0;
                return;
            }

            if (isInRadius)
            {
                lastFrequency = toneId switch
                {
                    "sinus_linear" => AudioUpdateHelper.HandleSinusLinear3D(sineAudioManager, selectedToneType, distance3dMouseToProbe, _canvas.ProbeRadiusMeters, lastFrequency),
                    "sinus_log" => AudioUpdateHelper.HandleSinusLog3D(sineAudioManager, selectedToneType, distance3dMouseToProbe, _canvas.ProbeRadiusMeters, lastFrequency),
                    "tonleiter_diskret" => AudioUpdateHelper.HandleTonleiterDiskret3D(sineAudioManager, selectedToneType, distance3dMouseToProbe, _canvas.ProbeRadiusMeters, lastFrequency),
                    _ => AudioUpdateHelper.HandleDefault(sineAudioManager?.Provider),
                };

                CurrentFrequency = lastFrequency ?? 0.0;
                sineAudioManager?.Play();
            }
            else
            {
                sineAudioManager?.Pause();
                CurrentFrequency = 0.0;
            }
        }

        private bool ProcessEinklinkAusklink(double horiz2dMeters, bool isEingeklinkt)
        {
            if (!isEingeklinkt && horiz2dMeters <= EinklinkRadiusMeters)
            {
                shepardMouseAudioManager?.Pause();
                sineAudioManager?.Pause();

                einklinkAudioManager?.PlayEinklinkSound();
                CurrentFrequency = 0.0;

                shepardMouseAudioManager?.Play();
                sineAudioManager?.Play();

                return true; // Eingeklinkt
            }
            else if (isEingeklinkt && horiz2dMeters > EinklinkRadiusMeters)
            {
                einklinkAudioManager?.PlayAusklinkSound();
                shepardMouseAudioManager?.Play();
                sineAudioManager?.Play();
                return false; // Ausgeklinkt
            }

            return isEingeklinkt;
        }

        public void UpdateProbeDepth(double newDepth)
        {
            ProbeDepthMeters = Math.Clamp(newDepth, 0.0, MaxDepthMeters);
            OnPropertyChanged(nameof(ProbeRadiusMetersIn2d));
            OnPropertyChanged(nameof(ProbeRadiusPixelsIn2d));
            if (!IsRunNotStarted)
            {
                UpdateAudioForMousePosition();
            }
        }

        // Sweep mit Streaming-Provider
        private void StartRun()
        {
            CurrentMousePosition = new Point(-1, -1);

            IsRunNotStarted = false;
            IsArrowBoxVisible = arrowEnabled;
            IsProbeVisible = false;
            IsClickPointVisible = false;
            DistanceText = "";
            TimerText = "00:00";

            var rnd = new Random();
            probeLogicalPosition = _canvas.GetRandomProbePosition(rnd);
            ProbePosition = probeLogicalPosition;

            _runTimerService?.Dispose();
            _runTimerService = new RunTimerService(text => TimerText = text);
            _runTimerService.Start();

            if (arrowEnabled)
            {
                var dispatcher = Dispatcher.GetForCurrentThread();
                if (dispatcher != null)
                {
                    _arrowHelper.Start(
                        dispatcher,
                        TimeSpan.FromMilliseconds(50),
                        () => ProbePosition,
                        () => CurrentMousePosition,
                        rot => ArrowRotation = rot);
                }
                // Optional: else-Block für Logging oder Fehlerbehandlung, falls dispatcher null ist
            }

            // Initialisierung abhängig vom Tontyp
            var toneId = selectedToneType?.Type?.ToLowerInvariant();
            if (toneId == "shepard")
            {
                InitializeShepardMouseAudio();
                isAudioActive = true;
                lastFrequency = 0.0; // Shepard wird über Progress angezeigt
            }
            else
            {
                InitializeSinewaveAudio(); // Sine
                isAudioActive = true;

                var minFreqInit = selectedToneType?.MinFrequency;
                lastFrequency = minFreqInit ?? lastFrequency ?? 0.0;
            }

            UpdateAudioForMousePosition();
        }

        private void StopArrowTimer() => _arrowHelper.Stop();

        public void OnCanvasClicked(Point mousePosition)
        {
            if (IsRunNotStarted) return;

            _runTimerService?.Stop();

            _uiTimer?.Stop();
            _uiTimer = null;
            StopArrowTimer();
            StopAudio();

            IsClickPointVisible = true;
            var clamped = _canvas.ClampToCanvas(mousePosition);
            ClickPointPosition = clamped;
            IsProbeVisible = true;

            var dist = Distance3DHelper.CalculateDistanceMeters(probeLogicalPosition, clamped, MeterPerPixel);
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
            var resultsPageVm = new ResultsPageViewModel(RunResults, userName, selectedToneType?.Type, arrowEnabled);
            var resultsPage = new ResultsPage { BindingContext = resultsPageVm };
            await Shell.Current.Navigation.PushAsync(resultsPage);
        }

        public void UpdateArrowToProbe()
        {
            double dx = ProbePosition.X - CurrentMousePosition.X;
            double dy = ProbePosition.Y - CurrentMousePosition.Y;
            ArrowRotation = Math.Atan2(dy, dx) * 180 / Math.PI;
        }

        private void InitializeSinewaveAudio()
        {
            StopAudio();
            _anyAudioActive = true;
            sineAudioManager = new SineWaveAudioManager(44100, 440.0, 0.0f);
            sineAudioManager.Initialize();
            sineAudioManager.SetVolume(VOLUME);
        }

        private void StopAudio()
        {
            _anyAudioActive = false;
            isAudioActive = false;

            sineAudioManager?.Stop();
            sineAudioManager?.Dispose();
            sineAudioManager = null;

            shepardMouseAudioManager?.Stop();
            shepardMouseAudioManager?.Dispose();
            shepardMouseAudioManager = null;

            // optional: auch den Shepard-Sweep stoppen, falls aktiv
            StopShepardSweepProvider();
        }

        // 1. Callback-Handler im ViewModel definieren:
        private void OnShepardSweepCycleCount(int cycleCount)
        {
            MainThread.BeginInvokeOnMainThread(() => ShepardCycleCount = cycleCount);
        }

        private void OnShepardSweepFreqProgress(double freq, double progress)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentFrequency = freq;
                ShepardProgress = progress;
            });
        }

        // 2. PlayShepardSweepProvider entsprechend anpassen:
        private void PlayShepardSweepProvider()
        {
            if (_anyAudioActive) StopAudio();
            StopShepardSweepProvider();
            _anyAudioActive = true;

            int sampleRate = 44100;
            ShepardCycleCount = 0;
            ShepardProgress = 0;
            CurrentShepardFrequency = selectedToneType.FMin ?? 0;

            _shepardSweepAudioManager = new ShepardSweepAudioManager(
                selectedToneType,
                sampleRate,
                OnShepardSweepCycleCount,
                OnShepardSweepFreqProgress);

            double horiz2dMeters = Distance3DHelper.CalculateDistanceMeters(ProbePosition, CurrentMousePosition, MeterPerPixel);
            _shepardSweepAudioManager.Start(horiz2dMeters, ProbeDepthMeters, ProbeRadiusMetersIn2d, VOLUME);

            (StopShepardSweepProviderCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }

        private void StopShepardSweepProvider()
        {
            _shepardSweepAudioManager?.Stop();
            _shepardSweepAudioManager?.Dispose();
            _shepardSweepAudioManager = null;

            ShepardProgress = 0;
            CurrentShepardFrequency = 0;
            (StopShepardSweepProviderCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
        private void InitializeShepardMouseAudio()
        {
            StopAudio();
            shepardMouseAudioManager = new ShepardMouseAudioManager(selectedToneType, 44100, 2048, VOLUME);
            shepardMouseAudioManager.Initialize();

            double horiz2dMeters = Distance3DHelper.CalculateDistanceMeters(ProbePosition, CurrentMousePosition, MeterPerPixel);
            double distance3dMouseToProbe = Distance3DHelper.Compute3dDistanceToProbe(horiz2dMeters, ProbeDepthMeters);
            double t = Distance3DHelper.CalculateProximity3D(ProbeRadius3d, distance3dMouseToProbe);

            shepardMouseAudioManager.UpdateDistance3DState(t);
            _anyAudioActive = true;
        }

        public bool CanStartShepard => IsRunNotStarted; // oder ein eigener Zustand

        private bool _disposed;

        public void Dispose()
        {
            _disposed = true;
            StopAudio();
            _runTimerService?.Dispose();
            _uiTimer?.Stop();
            _uiTimer = null;
        }
    }
}