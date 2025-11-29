using NAudio.Wave;
using System;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    // Shepard-Ton Provider mit dynamischer Richtung (up/down/none) und Gain-Steuerung.
    public sealed class ShepardToneProvider : ISampleProvider
    {
        private readonly int _sampleRate;
        private readonly float _baseFreq;
        private readonly float _duration;
        private readonly int _numComponents;
        private long _samplePosition;

        private readonly object _lock = new();
        private string _direction; // "up", "down", "none"
        private float _gain;       // 0..1

        public WaveFormat WaveFormat { get; }

        public ShepardToneProvider(
            float baseFreq = 440f,
            string direction = "none",
            float duration = 5f,
            int sampleRate = 44100,
            int numComponents = 10)
        {
            _baseFreq = Math.Max(20f, baseFreq);
            _direction = NormalizeDirection(direction);
            _duration = Math.Max(0.1f, duration);
            _sampleRate = sampleRate;
            _numComponents = Math.Max(4, numComponents);
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _samplePosition = 0;
            _gain = 1.0f;
        }

        public void SetDirection(string direction)
        {
            lock (_lock)
            {
                _direction = NormalizeDirection(direction);
            }
        }

        public void SetGain(float gain)
        {
            lock (_lock)
            {
                _gain = Math.Clamp(gain, 0f, 1f);
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _samplePosition = 0;
            }
        }

        private static string NormalizeDirection(string d) =>
            d switch
            {
                "up" => "up",
                "down" => "down",
                _ => "none"
            };

        public int Read(float[] buffer, int offset, int count)
        {
            string localDirection;
            float localGain;
            lock (_lock)
            {
                localDirection = _direction;
                localGain = _gain;
            }

            for (int n = 0; n < count; n++)
            {
                float t = (float)_samplePosition / _sampleRate;

                // Grundfrequenzverlauf abhängig von Richtung
                float freq = _baseFreq;
                if (localDirection == "up")
                {
                    freq = _baseFreq * (float)Math.Pow(2.0, t / _duration);
                }
                else if (localDirection == "down")
                {
                    freq = _baseFreq * (float)Math.Pow(2.0, -t / _duration);
                }
                // "none" => konstante Basisfrequenz

                float sample = 0f;

                // Komponenten über Oktaven
                for (int k = 0; k < _numComponents; k++)
                {
                    float f = freq * (float)Math.Pow(2.0, k);
                    // Gauß-Gewichtung im log2 Raum (Zentrum ca. 440 Hz)
                    float log2Ratio = (float)(Math.Log(f / 440.0) / Math.Log(2.0));
                    float amp = (float)Math.Exp(-0.5 * log2Ratio * log2Ratio);
                    sample += amp * (float)Math.Sin(2.0 * Math.PI * f * t);
                }

                // Mittelung + globaler Gain
                buffer[offset + n] = sample / _numComponents * 0.2f * localGain;

                _samplePosition++;
                // Zyklischer Reset, damit die Exponentialfunktion nicht unendlich läuft
                if (_samplePosition >= (long)(_duration * _sampleRate))
                {
                    _samplePosition = 0;
                }
            }
            return count;
        }
    }
}
