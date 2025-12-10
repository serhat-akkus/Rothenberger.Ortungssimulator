using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using NAudio.Wave;

namespace Rothenberger.Ortungssimulator.ViewModels
{
    public enum SweepMode
    {
        Linear,
        Logarithmic,
        Cyclic
    }

    public enum ToneType
    {
        SinusLinear,
        SinusLogarithmic,
        SinusExponential,
        Shepard
    }

    public class MeasurementResult
    {
        public TimeSpan Time { get; set; }
        public double Distance { get; set; }
        public DateTime Timestamp { get; set; }
        public string ToneType { get; set; }
        public float SinusFrequency { get; set; }
        public float SinusAmplitude { get; set; }
        public float ShepardBaseFrequency { get; set; }
        public float ShepardFrequencyRange { get; set; }
        public int ShepardNumTones { get; set; }
        public float ShepardAmplitude { get; set; }
        public float ShepardEnvelopeWidth { get; set; }
        public string ShepardSweepMode { get; set; }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private const int CanvasSizePx = 1000;
        private const double MeterPerPixel = 0.01;
        private const double ProbeRadiusMeter = 5.0;
        private const int ProbeRadiusPx = (int)(ProbeRadiusMeter / MeterPerPixel);

        private readonly Random _random = new();
        private readonly DispatcherTimer _timer;
        private TimeSpan _elapsed;
        private bool _timerRunning;
        private int _clickCount;
        private double _probeX;
        private double _probeY;
        private double _mouseX;
        private double _mouseY;
        private bool _probeVisible;
        private bool _showScale;
        private double _clickedX;
        private double _clickedY;

        private WaveOutEvent _waveOut;
        private ISoundProvider _soundProvider;
        private bool _soundActive;

        private float _sinusFrequency = 440f;
        public float SinusFrequency
        {
            get => _sinusFrequency;
            set
            {
                if (_sinusFrequency != value)
                {
                    _sinusFrequency = value;
                    OnPropertyChanged();
                    UpdateSoundProviderParameters();
                }
            }
        }

        private float _sinusAmplitude = 1f;
        public float SinusAmplitude
        {
            get => _sinusAmplitude;
            set
            {
                if (_sinusAmplitude != value)
                {
                    _sinusAmplitude = value;
                    OnPropertyChanged();
                    UpdateSoundProviderParameters();
                }
            }
        }

        private float _shepardBaseFrequency = 440f;
        public float ShepardBaseFrequency
        {
            get => _shepardBaseFrequency;
            set { if (_shepardBaseFrequency != value) { _shepardBaseFrequency = value; OnPropertyChanged(); UpdateSoundProviderParameters(); } }
        }

        private int _shepardNumTones = 6;
        public int ShepardNumTones
        {
            get => _shepardNumTones;
            set { if (_shepardNumTones != value) { _shepardNumTones = value; OnPropertyChanged(); UpdateSoundProviderParameters(); } }
        }

        private float _shepardAmplitude = 1f;
        public float ShepardAmplitude
        {
            get => _shepardAmplitude;
            set { if (_shepardAmplitude != value) { _shepardAmplitude = value; OnPropertyChanged(); UpdateSoundProviderParameters(); } }
        }

        private float _shepardFrequencyRange = 440f;
        public float ShepardFrequencyRange
        {
            get => _shepardFrequencyRange;
            set { if (_shepardFrequencyRange != value) { _shepardFrequencyRange = value; OnPropertyChanged(); UpdateSoundProviderParameters(); } }
        }

        private float _shepardEnvelopeWidth = 2.0f;
        public float ShepardEnvelopeWidth
        {
            get => _shepardEnvelopeWidth;
            set { if (_shepardEnvelopeWidth != value) { _shepardEnvelopeWidth = value; OnPropertyChanged(); UpdateSoundProviderParameters(); } }
        }

        private SweepMode _shepardSweepMode = SweepMode.Linear;
        public SweepMode ShepardSweepMode
        {
            get => _shepardSweepMode;
            set { if (_shepardSweepMode != value) { _shepardSweepMode = value; OnPropertyChanged(); UpdateSoundProviderParameters(); } }
        }

        public ObservableCollection<ToneType> ToneTypes { get; } = new()
        {
            ToneType.Shepard,
            ToneType.SinusLinear,
            ToneType.SinusLogarithmic,
            ToneType.SinusExponential
        };

        public ObservableCollection<SweepMode> SweepModes { get; } = new()
        {
            SweepMode.Linear,
            SweepMode.Logarithmic,
            SweepMode.Cyclic
        };

        private ToneType _selectedToneType = ToneType.SinusLinear;
        public ToneType SelectedToneType
        {
            get => _selectedToneType;
            set
            {
                if (_selectedToneType != value)
                {
                    _selectedToneType = value;
                    OnPropertyChanged();
                    InitSoundProvider();
                }
            }
        }

        public ObservableCollection<MeasurementResult> Results { get; } = new();

        // Formatierte Zeit (Sekunden mit zwei Nachkommastellen)
        public string ElapsedFormatted => _elapsed.TotalSeconds.ToString("F2");

        public TimeSpan Elapsed
        {
            get => _elapsed;
            set
            {
                _elapsed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ElapsedFormatted));
            }
        }

        public MainViewModel()
        {
            CanvasClickCommand = new RelayCommand(OnCanvasClick);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (_, _) => Elapsed = _elapsed.Add(_timer.Interval);
            InitSoundProvider();
            ResetSimulator();
        }

        private void InitSoundProvider()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();

            switch (SelectedToneType)
            {
                case ToneType.SinusLinear:
                case ToneType.SinusLogarithmic:
                case ToneType.SinusExponential:
                    _soundProvider = new SineWaveProvider32();
                    _soundProvider.SetWaveFormat(44100, 1);
                    _soundProvider.Frequency = SinusFrequency;
                    _soundProvider.Amplitude = SinusAmplitude;
                    break;
                case ToneType.Shepard:
                    _soundProvider = new ShepardToneProvider32
                    {
                        NumTones = ShepardNumTones,
                        EnvelopeWidth = ShepardEnvelopeWidth
                    };
                    _soundProvider.SetWaveFormat(44100, 1);
                    _soundProvider.Frequency = ShepardBaseFrequency;
                    _soundProvider.Amplitude = ShepardAmplitude;
                    break;
            }

            _waveOut = new WaveOutEvent();
            _waveOut.Init(_soundProvider);
        }

        private void UpdateSoundProviderParameters()
        {
            if (_soundProvider == null) return;

            switch (SelectedToneType)
            {
                case ToneType.SinusLinear:
                case ToneType.SinusLogarithmic:
                case ToneType.SinusExponential:
                    _soundProvider.Frequency = SinusFrequency;
                    break;
                case ToneType.Shepard:
                    if (_soundProvider is ShepardToneProvider32 shepard)
                    {
                        shepard.Frequency = ShepardBaseFrequency;
                        shepard.NumTones = ShepardNumTones;
                        shepard.EnvelopeWidth = ShepardEnvelopeWidth;
                    }
                    break;
            }

            UpdateSoundIfActive();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand CanvasClickCommand { get; }

        public double ProbeX { get => _probeX; set { _probeX = value; OnPropertyChanged(); } }
        public double ProbeY { get => _probeY; set { _probeY = value; OnPropertyChanged(); } }
        public bool ProbeVisible { get => _probeVisible; set { _probeVisible = value; OnPropertyChanged(); } }
        public double MouseX { get => _mouseX; set { _mouseX = value; OnPropertyChanged(); } }
        public double MouseY { get => _mouseY; set { _mouseY = value; OnPropertyChanged(); } }
        public bool TimerRunning { get => _timerRunning; set { _timerRunning = value; OnPropertyChanged(); } }
        public bool ShowScale { get => _showScale; set { _showScale = value; OnPropertyChanged(); } }
        public double ClickedX { get => _clickedX; set { _clickedX = value; OnPropertyChanged(); } }
        public double ClickedY { get => _clickedY; set { _clickedY = value; OnPropertyChanged(); } }

        private bool _showDistanceAfterTimer;
        public bool ShowDistanceAfterTimer { get => _showDistanceAfterTimer; set { _showDistanceAfterTimer = value; OnPropertyChanged(); } }

        public double DistanceToProbeMeter
        {
            get
            {
                if (!ShowScale) return 0;
                double dx = (ClickedX - ProbeX) * MeterPerPixel;
                double dy = (ClickedY - ProbeY) * MeterPerPixel;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public void OnCanvasMouseMove(double x, double y)
        {
            MouseX = x;
            MouseY = y;
            UpdateSoundIfActive();
        }

        private void UpdateSoundIfActive()
        {
            if (_clickCount != 1) return;

            double dx = (MouseX - ProbeX) * MeterPerPixel;
            double dy = (MouseY - ProbeY) * MeterPerPixel;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist <= ProbeRadiusMeter)
            {
                float amplitude;
                switch (SelectedToneType)
                {
                    case ToneType.SinusLinear:
                        amplitude = SinusAmplitude * (float)(1.0 - (dist / ProbeRadiusMeter));
                        break;
                    case ToneType.SinusLogarithmic:
                        amplitude = SinusAmplitude * (float)(1.0 - Math.Log10(1 + 9 * dist / ProbeRadiusMeter));
                        amplitude = Math.Max(0f, amplitude);
                        break;
                    case ToneType.SinusExponential:
                        double norm = 1.0 - (dist / ProbeRadiusMeter);
                        amplitude = SinusAmplitude * (float)((Math.Exp(4 * norm) - 1) / (Math.Exp(4) - 1));
                        amplitude = Math.Max(0f, Math.Min(SinusAmplitude, amplitude));
                        break;
                    case ToneType.Shepard:
                        amplitude = ShepardAmplitude * (float)(1.0 - (dist / ProbeRadiusMeter));
                        break;
                    default:
                        amplitude = 0f;
                        break;
                }

                if (!_soundActive)
                {
                    _waveOut.Play();
                    _soundActive = true;
                }

                _soundProvider.Amplitude = amplitude;
                _soundProvider.Frequency = GetFrequencyForToneType(dist);
            }
            else
            {
                _soundProvider.Amplitude = 0f;
            }
        }

        private float GetFrequencyForToneType(double dist)
        {
            switch (SelectedToneType)
            {
                case ToneType.Shepard:
                    double norm = 1.0 - (dist / ProbeRadiusMeter);
                    double shepardFreq = ShepardSweepMode switch
                    {
                        SweepMode.Linear => ShepardBaseFrequency + (ShepardFrequencyRange * norm),
                        SweepMode.Logarithmic => ShepardBaseFrequency * Math.Pow(2, norm * Math.Log(ShepardFrequencyRange / ShepardBaseFrequency, 2)),
                        SweepMode.Cyclic => ShepardBaseFrequency * Math.Pow(2, (norm % 1.0)),
                        _ => ShepardBaseFrequency + (ShepardFrequencyRange * norm)
                    };
                    return (float)shepardFreq;
                default:
                    return SinusFrequency;
            }
        }

        private void OnCanvasClick(object parameter)
        {
            if (parameter is not Tuple<double, double> pos) return;

            double x = pos.Item1;
            double y = pos.Item2;

            if (_clickCount == 0)
            {
                Elapsed = TimeSpan.Zero;
                TimerRunning = true;
                _timer.Start();
                _clickCount = 1;
                ShowDistanceAfterTimer = false;
            }
            else if (_clickCount == 1)
            {
                ClickedX = x;
                ClickedY = y;
                ProbeVisible = true;
                ShowScale = true;
                TimerRunning = false;
                _timer.Stop();
                _clickCount = 2;
                OnPropertyChanged(nameof(DistanceToProbeMeter));

                _soundProvider.Amplitude = 0f;
                _waveOut.Stop();
                _soundActive = false;

                ShowDistanceAfterTimer = true;

                Results.Add(new MeasurementResult
                {
                    Time = Elapsed,
                    Distance = DistanceToProbeMeter,
                    Timestamp = DateTime.Now,
                    ToneType = SelectedToneType.ToString(),
                    SinusFrequency = SinusFrequency,
                    SinusAmplitude = SinusAmplitude,
                    ShepardBaseFrequency = ShepardBaseFrequency,
                    ShepardFrequencyRange = ShepardFrequencyRange,
                    ShepardNumTones = ShepardNumTones,
                    ShepardAmplitude = ShepardAmplitude,
                    ShepardEnvelopeWidth = ShepardEnvelopeWidth,
                    ShepardSweepMode = ShepardSweepMode.ToString()
                });
            }
            else
            {
                ResetSimulator();
            }
        }

        private void ResetSimulator()
        {
            ProbeVisible = false;
            ShowScale = false;
            TimerRunning = false;
            Elapsed = TimeSpan.Zero;
            _timer.Stop();
            _clickCount = 0;
            ProbeX = _random.Next(ProbeRadiusPx, CanvasSizePx - ProbeRadiusPx);
            ProbeY = _random.Next(ProbeRadiusPx, CanvasSizePx - ProbeRadiusPx);
            ClickedX = 0;
            ClickedY = 0;
            OnPropertyChanged(nameof(DistanceToProbeMeter));

            _soundProvider.Amplitude = 0f;
            _waveOut.Stop();
            _soundActive = false;

            ShowDistanceAfterTimer = false;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public interface ISoundProvider : IWaveProvider
    {
        float Frequency { get; set; }
        float Amplitude { get; set; }
        void SetWaveFormat(int sampleRate, int channels);
    }

    public class SineWaveProvider32 : WaveProvider32, ISoundProvider
    {
        public float Frequency { get; set; }
        public float Amplitude { get; set; }
        private float _phaseAngle;

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;
            for (int n = 0; n < sampleCount; n++)
            {
                buffer[n + offset] = (float)(Amplitude * Math.Sin(_phaseAngle));
                _phaseAngle += 2 * (float)Math.PI * Frequency / sampleRate;
                if (_phaseAngle > 2 * Math.PI) _phaseAngle -= 2 * (float)Math.PI;
            }
            return sampleCount;
        }
    }

    public class LogSineWaveProvider32 : WaveProvider32, ISoundProvider
    {
        public float Frequency { get; set; }
        public float Amplitude { get; set; }
        private float _phaseAngle;

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;
            for (int n = 0; n < sampleCount; n++)
            {
                buffer[n + offset] = (float)(Amplitude * Math.Sin(_phaseAngle));
                _phaseAngle += 2 * (float)Math.PI * Frequency / sampleRate;
                if (_phaseAngle > 2 * Math.PI) _phaseAngle -= 2 * (float)Math.PI;
            }
            return sampleCount;
        }
    }

    public class ShepardToneProvider32 : WaveProvider32, ISoundProvider
    {
        public float Frequency { get; set; }
        public float Amplitude { get; set; }
        public int NumTones { get; set; } = 6;
        public float EnvelopeWidth { get; set; } = 2.0f;
        private float _phaseAngle;

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;
            for (int n = 0; n < sampleCount; n++)
            {
                float value = 0f;
                for (int i = 0; i < NumTones; i++)
                {
                    float freq = Frequency * (float)Math.Pow(2, i - NumTones / 2);
                    float amp = Amplitude * (float)Math.Exp(-0.5 * Math.Pow((i - NumTones / 2) / EnvelopeWidth, 2));
                    value += amp * (float)Math.Sin(_phaseAngle * freq / Frequency);
                }
                buffer[n + offset] = value;
                _phaseAngle += 2 * (float)Math.PI * Frequency / sampleRate;
                if (_phaseAngle > 2 * Math.PI) _phaseAngle -= 2 * (float)Math.PI;
            }
            return sampleCount;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}