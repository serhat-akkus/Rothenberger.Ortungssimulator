using Microsoft.Maui.Dispatching;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public sealed class ArrowHelper
    {
        private IDispatcherTimer? _timer;

        public double ArrowRotation { get; private set; }

        public void Start(IDispatcher dispatcher, TimeSpan interval, Func<Point> getProbePosition, Func<Point> getMousePosition, Action<double> setArrowRotation, Action tickSideEffect)
        {
            Stop();

            _timer = dispatcher.CreateTimer();
            _timer.Interval = interval;
            _timer.Tick += (_, __) =>
            {
                // Rotation berechnen
                var probe = getProbePosition();
                var mouse = getMousePosition();
                var dx = probe.X - mouse.X;
                var dy = probe.Y - mouse.Y;
                ArrowRotation = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                // Rotation ins ViewModel schreiben
                setArrowRotation(ArrowRotation);

                // Zusätzliche Tick-Logik (z. B. Audio-Update)
                tickSideEffect?.Invoke();
            };
            _timer.Start();
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= null; // Timer-Handler wird beim Dispose freigegeben
                _timer = null;
            }
        }
    }
}