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
        [ObservableProperty] private double currentShepardMouseFrequency;

        public ObservableCollection<TestRunResult> RunResults { get; } = new();

        private const float VOLUME = 0.5f;
        private const double MeterPerPixel = 0.01;
        private const double ProbeRadius3d = 5.0;
        //private const int UpdateIntervalSamples = 2048;
        private const int UpdateIntervalSamples = 256;
        private const int SampleRate = 44100;
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

        public ToneType SelectedToneType
        {
            get => selectedToneType;
            set
            {
                if (SetProperty(ref selectedToneType, value))
                {
                    OnPropertyChanged(nameof(IsDebugToneType));
                }
            }
        }

        public bool IsDebugToneType => SelectedToneType?.Debug == true;

        private readonly Graphics.CanvasHelper _canvas = new Graphics.CanvasHelper();
        private readonly ArrowHelper _arrowHelper = new ArrowHelper();

        private bool _anyAudioActive;

        private SineWaveAudioManager? sineAudioManager;
        private ShepardMouseAudioManager? shepardMouseAudioManager;
        private ShepardSweepAudioManager? _shepardSweepAudioManager;
        private ShepardDiscreteAudioManager? shepardDiscreteAudioManager;

        private bool _isEingeklinkt = false;

        private EinklinkAudioManager? einklinkAudioManager;

        private BeepAudioService? beepAudioService;

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
            beepAudioService = new BeepAudioService();
        }

        public bool IsMouseOverCanvas(Point mousePosition, double canvasX, double canvasY, double canvasWidth, double canvasHeight)
        {
            return mousePosition.X >= canvasX && mousePosition.Y >= canvasY
                && mousePosition.X < canvasX + canvasWidth && mousePosition.Y < canvasY + canvasHeight;
        }

        private double CalculateProbeRadiusMetersIn2d(double ProbeRadiusMeters, double DepthMeters)
        {
            var R = ProbeRadiusMeters;
            var z = DepthMeters;
            var sq = R * R - z * z;
            if (sq <= 0) return 0.0;
            return Math.Sqrt(sq);
        }

        public void UpdateAudioForMousePosition(bool isMouseOverCanvas)
        {
            IsArrowBoxVisible = isMouseOverCanvas;

            double horiz2dMeters = Distance3DHelper.CalculateDistanceMeters(ProbePosition, CurrentMousePosition, MeterPerPixel);
            bool isInRadius = horiz2dMeters <= ProbeRadiusMetersIn2d;

            // Beep nur außerhalb des Sondenradius und im Canvas
            if (!isInRadius)
            {
                beepAudioService?.Play();
                sineAudioManager?.Pause();
                shepardMouseAudioManager?.Pause();
                shepardDiscreteAudioManager?.Pause();
                return;
            }
            else
            {
                beepAudioService?.Pause();
            }

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
            bool isInRadius3d = distance3dMouseToProbe <= _canvas.ProbeRadiusMeters;
            double normalizedDistance3d = Distance3DHelper.CalculateProximity3D(ProbeRadius3d, distance3dMouseToProbe);

            if (isInRadius3d)
            {
                lastFrequency = toneId switch
                {
                    "sinus_linear" => AudioUpdateHelper.HandleSinusLinear3D(sineAudioManager, selectedToneType, distance3dMouseToProbe, _canvas.ProbeRadiusMeters, lastFrequency),
                    "sinus_log" => AudioUpdateHelper.HandleSinusLog3D(sineAudioManager, selectedToneType, distance3dMouseToProbe, _canvas.ProbeRadiusMeters, lastFrequency),
                    "tonleiter_diskret" => AudioUpdateHelper.HandleTonleiterDiskret3D(sineAudioManager, selectedToneType, distance3dMouseToProbe, _canvas.ProbeRadiusMeters, lastFrequency),
                    "shepard" => AudioUpdateHelper.HandleShepardMouse3D(shepardMouseAudioManager, normalizedDistance3d),
                    "shepard_tonleiter" => AudioUpdateHelper.HandleShepardTonleiter(shepardDiscreteAudioManager, selectedToneType, distance3dMouseToProbe, _canvas.ProbeRadiusMeters),
                    _ => AudioUpdateHelper.HandleDefault(sineAudioManager?.Provider),
                };

                CurrentFrequency = lastFrequency ?? 0.0;
                CurrentShepardMouseFrequency = lastFrequency ?? 0.0;

                switch (toneId)
                {
                    case "shepard":
                        shepardMouseAudioManager?.Play();
                        break;
                    case "shepard_tonleiter":
                        shepardDiscreteAudioManager?.Play();
                        break;
                    default:
                        sineAudioManager?.Play();
                        break;
                }
            }
            else
            {
                switch (toneId)
                {
                    case "shepard":
                        shepardMouseAudioManager?.Pause();
                        break;
                    case "shepard_tonleiter":
                        shepardDiscreteAudioManager?.Pause();
                        break;
                    default:
                        sineAudioManager?.Pause();
                        break;
                }

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
            else if (toneId == "shepard_tonleiter")
            {
                // Diskrete Shepard-Tonleiter
                StopAudio();
                shepardDiscreteAudioManager = new ShepardDiscreteAudioManager(selectedToneType, SampleRate, VOLUME);
                shepardDiscreteAudioManager.Initialize();
                isAudioActive = true;
                lastFrequency = 0.0;
            }
            else
            {
                InitializeSinewaveAudio(); // Sine
                isAudioActive = true;

                var minFreqInit = selectedToneType?.MinFrequency;
                lastFrequency = minFreqInit ?? lastFrequency ?? 0.0;
            }
        }

        private void StopArrowTimer() => _arrowHelper.Stop();

        public void OnCanvasClicked(Point mousePosition)
        {
            beepAudioService?.Pause();
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
            sineAudioManager = new SineWaveAudioManager(selectedToneType, 44100, 440.0, 0.0f);
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

            shepardDiscreteAudioManager?.Stop();
            shepardDiscreteAudioManager?.Dispose();
            shepardDiscreteAudioManager = null;

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

        private void OnShepardMouseFrequencyChanged(double frequency)
        {
            System.Diagnostics.Debug.WriteLine($"[ShepardMouseFrequency] {frequency:F2} Hz");
            MainThread.BeginInvokeOnMainThread(() => CurrentShepardMouseFrequency = frequency);
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
                OnShepardSweepFreqProgress,
                UpdateIntervalSamples);

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
            shepardMouseAudioManager = new ShepardMouseAudioManager(selectedToneType, SampleRate, VOLUME, OnShepardMouseFrequencyChanged);
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
            beepAudioService?.Pause();
            beepAudioService?.Dispose();
            beepAudioService = null;
            _runTimerService?.Dispose();
            _uiTimer?.Stop();
            _uiTimer = null;
        }
    }
}