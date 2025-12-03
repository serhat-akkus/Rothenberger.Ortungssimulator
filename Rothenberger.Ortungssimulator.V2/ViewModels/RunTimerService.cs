using System;
using Microsoft.Maui.ApplicationModel;

namespace Rothenberger.Ortungssimulator.V2.ViewModels
{
    public class RunTimerService : IDisposable
    {
        private IDispatcherTimer? _timer;
        private DateTime _startTime;
        private readonly Action<string> _onTick;
        private bool _isRunning;

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
                _timer.Interval = TimeSpan.FromMilliseconds(50);
                _timer.Tick += OnTimerTick;
                _timer.Start();
                _isRunning = true;
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
            _isRunning = false;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            var text = string.Format("{0:mm\\:ss}.{1:000}", elapsed, elapsed.Milliseconds);
            _onTick(text);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}