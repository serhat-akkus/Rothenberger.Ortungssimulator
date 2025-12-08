using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

public class AusklinkSoundProvider : IAudioProvider
{
    private readonly object _lock = new();
    private IWavePlayer? _waveOut;
    private SignalGenerator? _signalGen;

    public void Play()
    {
        Stop();
        _signalGen = new SignalGenerator()
        {
            Frequency = 440, // z.B. tieferer Ton
            Type = SignalGeneratorType.Sin
        };
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_signalGen);
        _waveOut.Play();

        // Stop nach kurzer Zeit (z.B. 200ms)
        var timer = new System.Timers.Timer(200);
        timer.Elapsed += (s, e) =>
        {
            timer.Dispose();
            Stop();
        };
        timer.AutoReset = false;
        timer.Start();
    }

    public void Stop()
    {
        lock (_lock)
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _signalGen = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}