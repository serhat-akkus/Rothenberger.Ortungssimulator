using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Audio;
using Rothenberger.Ortungssimulator.V2.Models;
using System;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public class ShepardSweepAudioManager : IDisposable
    {
        private IWavePlayer? _waveOut;
        private ShepardToneSweepProvider? _provider;
        private readonly Action<int>? _cycleCallback;
        private readonly Action<double, double>? _freqProgressCallback;
        private readonly int _sampleRate;
        private readonly ToneType _toneType;
        private bool _disposed;

        public ShepardSweepAudioManager(ToneType toneType, int sampleRate,
            Action<int>? cycleCallback,
            Action<double, double>? freqProgressCallback)
        {
            _toneType = toneType;
            _sampleRate = sampleRate;
            _cycleCallback = cycleCallback;
            _freqProgressCallback = freqProgressCallback;
        }

        public void Start(double horiz2dMeters, double probeDepthMeters, double probeRadiusMeters, float volume = 0.5f)
        {
            Dispose();
            _provider = new ShepardToneSweepProvider(
                _toneType,
                _sampleRate,
                _cycleCallback,
                _freqProgressCallback);
            _waveOut = new WaveOutEvent { DesiredLatency = 100 };
            _waveOut.Init(_provider);
            _waveOut.Volume = volume;
            _waveOut.Play();
            _provider.UpdateDistance3DState(horiz2dMeters, probeDepthMeters, probeRadiusMeters);
        }

        public void UpdateDistance(double horiz2dMeters, double probeDepthMeters, double probeRadiusMeters)
        {
            _provider?.UpdateDistance3DState(horiz2dMeters, probeDepthMeters, probeRadiusMeters);
        }

        public void Stop()
        {
            _provider?.RequestStop();
            try
            {
                if (_waveOut != null)
                {
                    _waveOut.Volume = 0.0f;
                    _waveOut.Pause();
                    _waveOut.Stop();
                    _waveOut.Dispose();
                }
            }
            catch { }
            _waveOut = null;
            _provider?.Dispose();
            _provider = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
        }
    }
}
