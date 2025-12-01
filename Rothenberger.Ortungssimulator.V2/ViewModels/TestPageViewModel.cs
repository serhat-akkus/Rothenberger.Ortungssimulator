using System.Collections.ObjectModel;
using System.Windows.Input;
using Rothenberger.Ortungssimulator.V2.Models;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rothenberger.Ortungssimulator.V2.Views;
using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Audio;
using System.IO;

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
        [ObservableProperty] bool isRunsCompletedVisible;
        [ObservableProperty] private double probeDepthMeters; // neue Tiefe (0–3 m)

        public ObservableCollection<TestRunResult> RunResults { get; } = new();

        private int currentRun = 1;
        private int totalRuns = 3;
        private System.Timers.Timer timer;
        private DateTime runStartTime;
        private Point probeLogicalPosition;
        private bool arrowEnabled = false;
        private string userName = "";
        private ToneType selectedToneType;

        private IWavePlayer? waveOut;
        private SineWaveProvider? sineProvider;
        private IAdaptiveShepardProvider? shepardProvider;
        private bool isAudioActive;
        private double? lastFrequency;
        private double? _previousDistanceMeters;
        private const double MovementEpsilonMeters = 0.01;

        public ICommand StartRunCommand { get; }
        public ICommand CanvasClickedCommand { get; }
        public IAsyncRelayCommand ContinueRunsCompletedCommand { get; }

        public ToneType SelectedToneType { get; private set; }

        private readonly Graphics.CanvasHelper _canvas = new Graphics.CanvasHelper();
        private ArrowHelper _arrowHelper = new ArrowHelper();

        public TestPageViewModel()
        {
            StartRunCommand = new RelayCommand(StartRun);
            CanvasClickedCommand = new RelayCommand<Point>(OnCanvasClicked);
            ContinueRunsCompletedCommand = new AsyncRelayCommand(OnContinueRunsCompletedAsync);
        }

        // Radius auf Oberfläche (effektiv, abhängig von Tiefe) in Metern
        public double EffectiveProbeRadiusMeters => _canvas.EffectiveProbeRadiusMeters;

        // Zugriff für Zeichnung (Pixel)
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

            // Initiale Aktualisierung der abhängigen Anzeige-Properties
            OnPropertyChanged(nameof(EffectiveProbeRadiusMeters));
            OnPropertyChanged(nameof(ProbeRadiusPixels));
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
            probeLogicalPosition = _canvas.GetRandomProbePosition(rnd);
            ProbePosition = probeLogicalPosition;

            timer = new System.Timers.Timer(1000);
            runStartTime = DateTime.Now;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();

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

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var elapsed = DateTime.Now - runStartTime;
            TimerText = elapsed.ToString(@"mm\:ss");
        }

        private void StopArrowTimer()
        {
            _arrowHelper.Stop();
        }

        public void OnCanvasClicked(Point mousePosition)
        {
            if (IsRunNotStarted) return;
            timer?.Stop();
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

        // Nur Anzeige / State, kein Speichern mehr hier
        private void ShowResults()
        {
            IsRunsCompletedVisible = true;
        }

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

            waveOut = new WaveOutEvent
            {
                DesiredLatency = 100
            };

            var toneId = selectedToneType?.Id?.ToLowerInvariant();
            if (toneId == "shepard")
            {
                shepardProvider = ShepardToneFactory.CreateFromToneType(selectedToneType);
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

            double distanceMeters = _canvas.CalculateDistanceMeters(ProbePosition, CurrentMousePosition);
            double normalized = _canvas.NormalizeDistance(distanceMeters);

            var toneId = selectedToneType?.Id?.ToLowerInvariant();

            switch (toneId)
            {
                case "shepard":
                    _previousDistanceMeters = AudioUpdateHelper.HandleShepard(
                        shepardProvider!,
                        distanceMeters,
                        normalized,
                        _previousDistanceMeters,
                        MovementEpsilonMeters);
                    break;
                case "sinus_linear":
                    lastFrequency = AudioUpdateHelper.HandleSinusLinear(
                        sineProvider!,
                        selectedToneType,
                        normalized,
                        distanceMeters,
                        lastFrequency);
                    break;
                case "sinus_log":
                    lastFrequency = AudioUpdateHelper.HandleSinusLog(
                        sineProvider!,
                        selectedToneType,
                        normalized,
                        distanceMeters,
                        lastFrequency);
                    break;
                case "tonleiter_diskret":
                    lastFrequency = AudioUpdateHelper.HandleTonleiterDiskret(
                        sineProvider!,
                        selectedToneType,
                        normalized,
                        distanceMeters,
                        lastFrequency);
                    break;
                default:
                    _previousDistanceMeters = AudioUpdateHelper.HandleDefault(sineProvider, shepardProvider);
                    break;
            }
        }

        void OnContinueRunsCompleted()
        {
            IsRunsCompletedVisible = false;
        }

        void OnAllRunsFinished()
        {
            IsRunsCompletedVisible = true;
        }

        // Optional: Methode um Tiefe während Lauf dynamisch zu ändern (falls später gewünscht)
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
    }
}