using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using NAudio.Wave;

namespace Rothenberger.Ortungssimulator.ViewModels
{
    public enum ToneType
    {
        SinusLinear,
        SinusLogarithmic,
        Shepard
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private const int CanvasSizePx = 1000;
        private const double MeterPerPixel = 0.01;
        private const double ProbeRadiusMeter = 5.0;
        private const int ProbeRadiusPx = (int)(ProbeRadiusMeter / MeterPerPixel);

        private Random _random = new Random();
        private DispatcherTimer _timer;
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

        // Audio
        private WaveOutEvent _waveOut;
        private ISoundProvider _soundProvider;
        private bool _soundActive;

        public ObservableCollection<ToneType> ToneTypes { get; } = new ObservableCollection<ToneType>
        {
            ToneType.Shepard,
            ToneType.SinusLinear,
            ToneType.SinusLogarithmic
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

        public MainViewModel()
        {
            CanvasClickCommand = new RelayCommand(OnCanvasClick);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (s, e) =>
            {
                Elapsed = Elapsed.Add(_timer.Interval);
            };

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
                    _soundProvider = new SineWaveProvider32();
                    break;
                case ToneType.SinusLogarithmic:
                    _soundProvider = new LogSineWaveProvider32();
                    break;
                case ToneType.Shepard:
                    _soundProvider = new ShepardToneProvider32();
                    break;
            }
            _soundProvider.SetWaveFormat(44100, 1);
            _soundProvider.Frequency = 440f;
            _soundProvider.Amplitude = 0f;
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_soundProvider);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand CanvasClickCommand { get; }

        public double ProbeX
        {
            get => _probeX;
            set { _probeX = value; OnPropertyChanged(); }
        }

        public double ProbeY
        {
            get => _probeY;
            set { _probeY = value; OnPropertyChanged(); }
        }

        public bool ProbeVisible
        {
            get => _probeVisible;
            set { _probeVisible = value; OnPropertyChanged(); }
        }

        public double MouseX
        {
            get => _mouseX;
            set { _mouseX = value; OnPropertyChanged(); }
        }

        public double MouseY
        {
            get => _mouseY;
            set { _mouseY = value; OnPropertyChanged(); }
        }

        public TimeSpan Elapsed
        {
            get => _elapsed;
            set { _elapsed = value; OnPropertyChanged(); }
        }

        public bool TimerRunning
        {
            get => _timerRunning;
            set { _timerRunning = value; OnPropertyChanged(); }
        }

        public bool ShowScale
        {
            get => _showScale;
            set { _showScale = value; OnPropertyChanged(); }
        }

        public double ClickedX
        {
            get => _clickedX;
            set { _clickedX = value; OnPropertyChanged(); }
        }

        public double ClickedY
        {
            get => _clickedY;
            set { _clickedY = value; OnPropertyChanged(); }
        }

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

            if (_clickCount == 1) // Nur nach erstem Klick
            {
                double dx = (MouseX - ProbeX) * MeterPerPixel;
                double dy = (MouseY - ProbeY) * MeterPerPixel;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist <= ProbeRadiusMeter)
                {
                    float amplitude;
                    switch (SelectedToneType)
                    {
                        case ToneType.SinusLinear:
                            amplitude = (float)(1.0 - (dist / ProbeRadiusMeter));
                            break;
                        case ToneType.SinusLogarithmic:
                            amplitude = (float)(1.0 - Math.Log10(1 + 9 * dist / ProbeRadiusMeter)); // log. Anstieg
                            amplitude = Math.Max(0f, amplitude);
                            break;
                        case ToneType.Shepard:
                            amplitude = (float)(1.0 - (dist / ProbeRadiusMeter));
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
        }

        private float GetFrequencyForToneType(double dist)
        {
            switch (SelectedToneType)
            {
                case ToneType.Shepard:
                    // Shepard-Ton: Frequenz zyklisch im Bereich 220-880 Hz
                    double norm = 1.0 - (dist / ProbeRadiusMeter);
                    double shepardFreq = 220 + 660 * norm;
                    return (float)shepardFreq;
                case ToneType.SinusLinear:
                case ToneType.SinusLogarithmic:
                default:
                    return 440f;
            }
        }

        private void OnCanvasClick(object parameter)
        {
            if (parameter is Tuple<double, double> pos)
            {
                double x = pos.Item1;
                double y = pos.Item2;

                if (_clickCount == 0)
                {
                    // Start timer
                    Elapsed = TimeSpan.Zero;
                    TimerRunning = true;
                    _timer.Start();
                    _clickCount = 1;
                }
                else if (_clickCount == 1)
                {
                    // Show probe and clicked position, stop timer
                    ClickedX = x;
                    ClickedY = y;
                    ProbeVisible = true;
                    ShowScale = true;
                    TimerRunning = false;
                    _timer.Stop();
                    _clickCount = 2;
                    OnPropertyChanged(nameof(DistanceToProbeMeter));

                    // Ton stoppen
                    _soundProvider.Amplitude = 0f;
                    _waveOut.Stop();
                    _soundActive = false;
                }
                else
                {
                    // Reset
                    ResetSimulator();
                }
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
            // Random position for probe, ensure it's fully inside canvas
            ProbeX = _random.Next(ProbeRadiusPx, CanvasSizePx - ProbeRadiusPx);
            ProbeY = _random.Next(ProbeRadiusPx, CanvasSizePx - ProbeRadiusPx);
            ClickedX = 0;
            ClickedY = 0;
            OnPropertyChanged(nameof(DistanceToProbeMeter));

            // Ton stoppen
            _soundProvider.Amplitude = 0f;
            _waveOut.Stop();
            _soundActive = false;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // Interface für SoundProvider
    public interface ISoundProvider : IWaveProvider
    {
        float Frequency { get; set; }
        float Amplitude { get; set; }
        void SetWaveFormat(int sampleRate, int channels);
    }

    // Sinus linear
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

    // Sinus logarithmisch
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
                // Logarithmischer Anstieg: Amplitude wird im ViewModel berechnet
                buffer[n + offset] = (float)(Amplitude * Math.Sin(_phaseAngle));
                _phaseAngle += 2 * (float)Math.PI * Frequency / sampleRate;
                if (_phaseAngle > 2 * Math.PI) _phaseAngle -= 2 * (float)Math.PI;
            }
            return sampleCount;
        }
    }

    // Shepard-Ton
    public class ShepardToneProvider32 : WaveProvider32, ISoundProvider
    {
        public float Frequency { get; set; }
        public float Amplitude { get; set; }
        private float _phaseAngle;
        private const int NumTones = 6;

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;
            for (int n = 0; n < sampleCount; n++)
            {
                float value = 0f;
                for (int i = 0; i < NumTones; i++)
                {
                    // Shepard-Ton: Überlagerung von Sinustönen im Oktavabstand mit abnehmender Amplitude
                    float freq = Frequency * (float)Math.Pow(2, i - NumTones / 2);
                    float amp = Amplitude * (float)Math.Exp(-0.5 * Math.Pow((i - NumTones / 2) / 2.0, 2));
                    value += amp * (float)Math.Sin(_phaseAngle * freq / Frequency);
                }
                buffer[n + offset] = value;
                _phaseAngle += 2 * (float)Math.PI * Frequency / sampleRate;
                if (_phaseAngle > 2 * Math.PI) _phaseAngle -= 2 * (float)Math.PI;
            }
            return sampleCount;
        }
    }

    // Simple RelayCommand implementation
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