namespace Rothenberger.Ortungssimulator.V2.ViewModels
{
    public class RunTimerService : IDisposable
    {
        private IDispatcherTimer? _timer;
        private DateTime _startTime;
        private readonly Action<string> _onTick;
        public TimeSpan TimeElapsed { get; private set; }

        public RunTimerService(Action<string> onTick)
        {
            _onTick = onTick;
        }

        public void Start()
        {
            Stop();
            _startTime = DateTime.Now;
            _timer = Application.Current?.Dispatcher.CreateTimer();
            if (_timer != null)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(10);
                _timer.Tick += OnTimerTick;
                _timer.Start();
            }
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTimerTick;
                _timer = null;
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            TimeElapsed = DateTime.Now - _startTime;
            var time = string.Format("{0:mm\\:ss}.{1:000}", TimeElapsed, TimeElapsed.Milliseconds);
            _onTick(time);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}