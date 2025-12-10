using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public class BeepAudioService : IDisposable
    {
        private Timer _timer;
        private readonly int _intervalMs = 1000;
        private readonly float _beepDurationSec = 0.1f;
        private readonly int _beepFrequency = 1000;
        private bool _isPlaying;
        private readonly object _lock = new();

        public void Play()
        {
            if (_isPlaying) return;
            _isPlaying = true;
            _timer = new Timer(Beep, null, 0, _intervalMs);
        }

        public void Pause()
        {
            _isPlaying = false;
            _timer?.Dispose();
            _timer = null;
        }

        private void Beep(object state)
        {
            if (!_isPlaying) return;
            lock (_lock)
            {
                using (var waveOut = new WaveOutEvent())
                {
                    var signal = new SignalGenerator()
                    {
                        Gain = 0.5,
                        Frequency = _beepFrequency,
                        Type = SignalGeneratorType.Sin
                    }.Take(TimeSpan.FromSeconds(_beepDurationSec));
                    waveOut.Init(signal);
                    waveOut.Play();
                    // Warten bis der Ton fertig ist
                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        Thread.Sleep(10);
                    }
                }
            }
        }

        public void Dispose()
        {
            Pause();
        }
    }
}
