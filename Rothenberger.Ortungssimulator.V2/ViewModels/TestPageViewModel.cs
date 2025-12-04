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

        // Felder ergänzen (oberhalb des ViewModel-Konstruktors):
        private IWavePlayer? shepardMouseWaveOut;
        private ShepardToneMouseProvider? shepardMouseProvider;

        private bool _isEingeklinkt = false;

        private IAudioProvider? einklinkProvider;
        private IAudioProvider? ausklinkProvider;

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
        public double EinklinkRadiusMeters => EffectiveProbeRadiusMeters / 20.0;
        public double EinklinkRadiusPixels => ProbeRadiusPixels / 20.0;

        public void Initialize(string name, ToneType selectedToneType, int runCount, bool arrow, bool showProbeRadius, double probeDepthMeters)
        {
            userName = name;
            SelectedToneType = selectedToneType;
            this.selectedToneType = selectedToneType;
            totalRuns = runCount;
            arrowEnabled = arrow;
            IsProberadiusEnabled = showProbeRadius;
            ProbeDepthMeters = probeDepthMeters;
            _canvas.SetDepth(ProbeDepthMeters);
            startRunButtonText = $"Starte Test-Run {currentRun}";
            OnPropertyChanged(nameof(EffectiveProbeRadiusMeters));
            OnPropertyChanged(nameof(ProbeRadiusPixels));
            UserNameDisplay = userName;
            ToneTypeDisplay = selectedToneType?.Name ?? "";

            einklinkProvider = new EinklinkSoundProvider();
            ausklinkProvider = new AusklinkSoundProvider();
        }

        public void UpdateAudioForMousePosition()
        {
            if (!isAudioActive)
                return;

            if (CurrentMousePosition.X == -1 && CurrentMousePosition.Y == -1)
            {
                sineProvider?.SetVolume(0.0f);
                if (shepardMouseWaveOut is WaveOutEvent wo)
                    wo.Volume = 0.0f;
                return;
            }

            double horiz2dMeters = _canvas.CalculateDistanceMeters(ProbePosition, CurrentMousePosition);

            // --- EINKLINK-LOGIK --- (MUSS VOR ALLEM ANDEREN STEHEN!)
            if (!_isEingeklinkt && horiz2dMeters <= EinklinkRadiusMeters)
            {
                _isEingeklinkt = true;
                
                if (shepardMouseWaveOut is WaveOutEvent wo)
                {
                    wo.Pause(); // Shepard-Ton wirklich pausieren
                }
                sineProvider?.SetVolume(0.0f);

                PlayEinklinkSound();
                return;
            }
            else if (_isEingeklinkt && horiz2dMeters > EinklinkRadiusMeters)
            {
                _isEingeklinkt = false;
                PlayAusklinkSound();
                if (shepardMouseWaveOut is WaveOutEvent wo)
                {
                    wo.Play(); // Shepard-Ton wieder aktivieren
                }
                // Provider wieder aktivieren: Methode läuft weiter!
            }

            // Nur ausführen, wenn NICHT eingeklinkt!
            if (_isEingeklinkt)
                return;

            // ... ab hier die bisherige Provider-Logik ...
            var toneId = selectedToneType?.Type?.ToLowerInvariant();
            double d3Meters = Distance3DHelper.Compute3D(horiz2dMeters, ProbeDepthMeters);
            const double eps = 1e-6;

            if (string.IsNullOrWhiteSpace(toneId))
            {
                if (sineProvider != null)
                {
                    sineProvider.SetVolume(0.6f);
                    sineProvider.SetFrequency(440.0);
                    lastFrequency = 440.0;
                }
                return;
            }

            bool audible = horiz2dMeters <= _canvas.ProbeRadiusMeters;

            if (toneId == "shepard")
            {
                if (shepardMouseProvider == null || shepardMouseWaveOut == null)
                    return;

                // Shepard-Ton nur innerhalb des 2D-Radius erzeugen!
                if (horiz2dMeters <= EffectiveProbeRadiusMeters)
                {
                    AudioUpdateHelper.UpdateShepardMouse3D(
                        shepardMouseProvider,
                        horiz2dMeters,
                        ProbeDepthMeters,
                        _canvas.ProbeRadiusMeters);

                    if (shepardMouseWaveOut is WaveOutEvent wo)
                        wo.Volume = 0.25f;
                }
                else
                {
                    if (shepardMouseWaveOut is WaveOutEvent wo)
                        wo.Volume = 0.0f;
                }

                lastFrequency = 0.0;
                return;
            }

            if (sineProvider == null)
                return;

            if (audible)
            {
                lastFrequency = toneId switch
                {
                    "sinus_linear"      => AudioUpdateHelper.HandleSinusLinear3D(sineProvider, selectedToneType, horiz2dMeters, ProbeDepthMeters, _canvas.ProbeRadiusMeters, lastFrequency),
                    "sinus_log"         => AudioUpdateHelper.HandleSinusLog3D(sineProvider, selectedToneType, horiz2dMeters, ProbeDepthMeters, _canvas.ProbeRadiusMeters, lastFrequency),
                    "tonleiter_diskret" => AudioUpdateHelper.HandleTonleiterDiskret3D(sineProvider, selectedToneType, horiz2dMeters, ProbeDepthMeters, _canvas.ProbeRadiusMeters, lastFrequency),
                    _                   => AudioUpdateHelper.HandleDefault(sineProvider),
                };

                CurrentFrequency = lastFrequency ?? 0.0;
            }
            else
            {
                sineProvider.SetVolume(0.0f);
                CurrentFrequency = 0.0;
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
                InitializeAudio(); // Sine
                isAudioActive = true;

                var minFreqInit = selectedToneType?.MinFrequency;
                lastFrequency = minFreqInit ?? lastFrequency ?? 0.0;
                sineProvider?.SetFrequency(lastFrequency.Value);
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

            // Sinus-Output stoppen
            if (waveOut != null)
            {
                try { waveOut.Stop(); } catch { }
                waveOut.Dispose();
                waveOut = null;
            }
            sineProvider?.Dispose();
            sineProvider = null;

            // Shepard (mausgesteuert) stoppen
            if (shepardMouseWaveOut != null)
            {
                try { shepardMouseWaveOut.Stop(); } catch { }
                shepardMouseWaveOut.Dispose();
                shepardMouseWaveOut = null;
            }
            shepardMouseProvider = null;

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

            // Nutze den geladenen ToneType für "shepard"
            var shepardType = selectedToneType;

            int sampleRate = 44100;
            int mMax = shepardType.MMax ?? 12;
            int nMax = shepardType.NMax ?? 7;
            double fMin = shepardType.FMin ?? 70.0;
            double sigma = shepardType.Sigma ?? 0.5;
            double semitonesPerSecond = shepardType.SensitivitySemitonesPerSecond ?? 1.5;
            double octaveOffset = shepardType.OctaveOffset ?? 0.0;

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
                OnShepardSweepCycleCount,
                OnShepardSweepFreqProgress,
                octaveOffset);

            shepardSweepWaveOut = new WaveOutEvent { DesiredLatency = 100 };
            shepardSweepWaveOut.Init(shepardSweepProvider);
            shepardSweepWaveOut.Volume = 0.2f;
            shepardSweepWaveOut.Play();

            double horiz2dMeters = _canvas.CalculateDistanceMeters(ProbePosition, CurrentMousePosition);
            shepardSweepProvider.UpdateDistance3DState(horiz2dMeters, ProbeDepthMeters, EffectiveProbeRadiusMeters);

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
        private void InitializeShepardMouseAudio()
        {
            StopAudio();

            int sampleRate = 44100;
            int mMax = selectedToneType.MMax ?? 12;
            int nMax = selectedToneType.NMax ?? 10;
            double fMin = selectedToneType.FMin ?? 55.0;
            double sigma = selectedToneType.Sigma ?? 0.8;
            double desiredCycles = selectedToneType.ShepardCyclesFromEdgeToProbe ?? 3.0;
            double shepardRadius = _canvas.ProbeRadiusMeters; // z.B. 5.0

            // Halbtöne pro Meter, damit auf shepardRadius genau desiredCycles Zyklen durchlaufen werden:
            double semitonesPerMeter = (desiredCycles * mMax) / shepardRadius;

            shepardMouseProvider?.Dispose();
            shepardMouseWaveOut?.Dispose();

            shepardMouseProvider = new ShepardToneMouseProvider(
                sampleRate: sampleRate,
                mMax: mMax,
                nMax: nMax,
                fMin: fMin,
                sigma: sigma,
                sensitivitySemitonesPerSecond: semitonesPerMeter,
                octaveOffset: 0.0,
                updateIntervalSamples: 2048);

            shepardMouseWaveOut = new WaveOutEvent { DesiredLatency = 100 };
            shepardMouseWaveOut.Init(shepardMouseProvider);
            shepardMouseWaveOut.Volume = 0.25f;
            shepardMouseWaveOut.Play();

            double horiz2dMeters = _canvas.CalculateDistanceMeters(ProbePosition, CurrentMousePosition);
            shepardMouseProvider.UpdateDistance3DState(horiz2dMeters, ProbeDepthMeters, EffectiveProbeRadiusMeters);

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

        private async void PlayEinklinkSound()
        {
            einklinkProvider?.Play();
            CurrentFrequency = 0.0;
        }

        private void PlayAusklinkSound()
        {
            ausklinkProvider?.Play();
        }
    }
}