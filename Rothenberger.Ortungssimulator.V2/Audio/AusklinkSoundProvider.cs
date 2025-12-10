using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

public class AusklinkSoundProvider : IAudioProvider
{
    private readonly object _lock = new();
    private IWavePlayer? _waveOut;
    private SignalGenerator? _signalGen;
    private System.Timers.Timer? _timer1;
    private System.Timers.Timer? _timer2;
    private readonly double _freq1;
    private readonly double _freq2;

    public AusklinkSoundProvider(double freq1, double freq2)
    {
        _freq1 = freq1;
        _freq2 = freq2;
    }

    public void Play()
    {
        Stop();
        // First beep: configurable
        _signalGen = new SignalGenerator()
        {
            Frequency = _freq1,
            Type = SignalGeneratorType.Sin
        };
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_signalGen);
        _waveOut.Play();

        _timer1 = new System.Timers.Timer(200);
        _timer1.Elapsed += (s, e) =>
        {
            _timer1.Dispose();
            // Second beep: configurable
            _signalGen = new SignalGenerator()
            {
                Frequency = _freq2,
                Type = SignalGeneratorType.Sin
            };
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_signalGen);
            _waveOut.Play();

            _timer2 = new System.Timers.Timer(200);
            _timer2.Elapsed += (s2, e2) =>
            {
                _timer2.Dispose();
                Stop();
            };
            _timer2.AutoReset = false;
            _timer2.Start();
        };
        _timer1.AutoReset = false;
        _timer1.Start();
    }

    public void Stop()
    {
        lock (_lock)
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _signalGen = null;
            _timer1?.Dispose();
            _timer1 = null;
            _timer2?.Dispose();
            _timer2 = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}