using NAudio.Wave;
using System;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    // Einfacher Sine-Wave-Provider mit 32-Bit Float Samples und Runtime-Volume
    public sealed class SineWaveProvider : ISampleProvider, IDisposable
    {
        private readonly WaveFormat _waveFormat;
        private double _phase;
        private readonly object _lock = new();
        private float _volume;
        private double _frequency;

        public SineWaveProvider(int sampleRate = 44100, double frequency = 440.0, float initialVolume = 0.0f)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1); // Mono
            _frequency = frequency;
            _volume = Math.Clamp(initialVolume, 0.0f, 1.0f);
        }

        public WaveFormat WaveFormat => _waveFormat;

        public void SetVolume(float volume)
        {
            lock (_lock)
            {
                _volume = Math.Clamp(volume, 0.0f, 1.0f);
            }
        }

        public void SetFrequency(double frequency)
        {
            lock (_lock)
            {
                _frequency = Math.Max(1.0, frequency);
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            double phaseIncrement;
            float vol;

            lock (_lock)
            {
                phaseIncrement = (2.0 * Math.PI * _frequency) / _waveFormat.SampleRate;
                vol = _volume;
            }

            for (int n = 0; n < count; n++)
            {
                buffer[offset + n] = (float)(Math.Sin(_phase) * vol);
                _phase += phaseIncrement;
                if (_phase >= 2.0 * Math.PI)
                    _phase -= 2.0 * Math.PI;
            }

            return count;
        }

        public void Dispose()
        {
            // nichts zu entsorgen hier, aber für künftige Erweiterungen vorhanden
        }
    }
}