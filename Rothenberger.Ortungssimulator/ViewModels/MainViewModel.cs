using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace Rothenberger.Ortungssimulator.ViewModels
{
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

        public MainViewModel()
        {
            CanvasClickCommand = new RelayCommand(OnCanvasClick);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (s, e) =>
            {
                Elapsed = Elapsed.Add(_timer.Interval);
            };

            ResetSimulator();
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
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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