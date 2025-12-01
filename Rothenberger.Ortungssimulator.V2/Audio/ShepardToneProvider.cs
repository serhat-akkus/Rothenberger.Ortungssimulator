using NAudio.Wave;
using System;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    // Positionsbasierter Shepard-Ton (Distanz -> Basisfrequenz)
    // Tonerzeugung angepasst: Gauß-Amplitude (centerFreq, sigma) + dynamische Normalisierung (Peak-Tracking)
    // Kein zeitlicher Zyklus, Frequenz ausschließlich von Maus-/Distanzposition abhängig.
    public sealed class ShepardToneProvider : ISampleProvider
    {
        private readonly int _sampleRate;
        private readonly float _baseFreq;
        private readonly float _centerFreq;
        private readonly float _sigma;
        private readonly float _radiusMeters;
        private readonly int _numComponents;
        private readonly float _octaveSpan;
        private readonly float[] _frequencyLut;
        private readonly int _lutResolution;
        private readonly float[] _phases;

        private float _currentDistance;
        private float _userGain;

        // Dynamische Normalisierung (Peak)
        private bool _normalizationEnabled = true;
        private float _normGain = 1f;
        private float _targetPeak = 0.9f;
        private float _attack = 0.04f;
        private float _release = 0.005f;

        // Master-Level (zusätzlicher globaler Skalierungsfaktor)
        private float _masterLevel = 0.7f;

        private readonly object _lock = new();

        public WaveFormat WaveFormat { get; }

        public ShepardToneProvider(
            float baseFreq = 440f,
            float radiusMeters = 5f,
            float duration = 5f,      // nur für Signatur beibehalten
            int sampleRate = 44100,
            int numComponents = 10,
            float octaveSpan = 2f,
            int lutResolution = 512,
            float centerFreq = 440f,
            float sigma = 1.0f,
            float masterLevel = 0.7f,
            bool normalizationEnabled = true)
        {
            _baseFreq = Math.Max(20f, baseFreq);
            _centerFreq = Math.Clamp(centerFreq, 20f, 20000f);
            _sigma = Math.Max(0.05f, sigma);
            _radiusMeters = Math.Max(0.5f, radiusMeters);
            _sampleRate = sampleRate;
            _numComponents = Math.Max(4, numComponents);
            _octaveSpan = Math.Max(0.25f, octaveSpan);
            _lutResolution = Math.Clamp(lutResolution, 32, 8192);

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _frequencyLut = new float[_lutResolution];
            _phases = new float[_numComponents];

            _currentDistance = _radiusMeters;
            _userGain = 1f;
            _masterLevel = Math.Clamp(masterLevel, 0.01f, 4f);
            _normalizationEnabled = normalizationEnabled;

            BuildFrequencyLut();
        }

        private void BuildFrequencyLut()
        {
            for (int i = 0; i < _lutResolution; i++)
            {
                float n = (float)i / (_lutResolution - 1);
                float inv = 1f - n;
                float f = _baseFreq * (float)Math.Pow(2.0, _octaveSpan * inv);
                f = Math.Clamp(f, 20f, 20000f);
                _frequencyLut[i] = f;
            }
        }

        public void SetGain(float gain)
        {
            lock (_lock)
            {
                _userGain = Math.Clamp(gain, 0f, 1f);
            }
        }

        public void SetMasterLevel(float level)
        {
            lock (_lock)
            {
                _masterLevel = Math.Clamp(level, 0.01f, 4f);
            }
        }

        public void EnableNormalization(bool enabled)
        {
            lock (_lock)
            {
                _normalizationEnabled = enabled;
            }
        }

        public void SetDistance(float distanceMeters)
        {
            lock (_lock)
            {
                _currentDistance = Math.Clamp(distanceMeters, 0f, _radiusMeters);
            }
        }

        public void SetPosition(float xMeters, float yMeters)
        {
            float d = MathF.Sqrt(xMeters * xMeters + yMeters * yMeters);
            SetDistance(d);
        }

        public float GetCurrentBaseFrequency()
        {
            lock (_lock)
            {
                return LookupFrequency(_currentDistance);
            }
        }

        private float LookupFrequency(float distance)
        {
            float n = distance / _radiusMeters;
            n = Math.Clamp(n, 0f, 1f);
            float pos = n * (_lutResolution - 1);
            int i0 = (int)pos;
            int i1 = Math.Min(i0 + 1, _lutResolution - 1);
            float frac = pos - i0;
            return _frequencyLut[i0] + (_frequencyLut[i1] - _frequencyLut[i0]) * frac;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            float localGain;
            float localDistance;
            bool localNormEnabled;
            float localMaster;
            lock (_lock)
            {
                localGain = _userGain;
                localDistance = _currentDistance;
                localNormEnabled = _normalizationEnabled;
                localMaster = _masterLevel;
            }

            float baseFrequency = LookupFrequency(localDistance);

            float peak = 0f;

            for (int n = 0; n < count; n++)
            {
                float sample = 0f;

                for (int k = 0; k < _numComponents; k++)
                {
                    float fComponent = baseFrequency * (float)Math.Pow(2.0, k);
                    if (fComponent > 20000f) break;

                    float log2Ratio = (float)(Math.Log(fComponent / _centerFreq) / Math.Log(2.0));
                    float scaled = log2Ratio / _sigma;
                    float amp = (float)Math.Exp(-0.5f * scaled * scaled);

                    float phaseInc = 2f * MathF.PI * fComponent / _sampleRate;
                    _phases[k] += phaseInc;
                    if (_phases[k] > MathF.PI * 2f)
                        _phases[k] -= MathF.PI * 2f;

                    sample += amp * MathF.Sin(_phases[k]);
                }

                // Psychoakustische Pegel-Normalisierung: sqrt statt volle Division
                sample /= MathF.Sqrt(_numComponents);

                float abs = Math.Abs(sample);
                if (abs > peak) peak = abs;

                buffer[offset + n] = sample;
            }

            if (localNormEnabled && peak > 0f)
            {
                // Ziel: Peak -> _targetPeak
                float desiredGain = _targetPeak / peak;
                if (desiredGain < _normGain)
                {
                    // Absenken (Release langsamer)
                    _normGain = _normGain * (1f - _release) + desiredGain * _release;
                }
                else
                {
                    // Anheben (Attack schneller)
                    _normGain = _normGain * (1f - _attack) + desiredGain * _attack;
                }
            }

            float finalGain = localGain * localMaster * _normGain;

            for (int n = 0; n < count; n++)
            {
                buffer[offset + n] *= finalGain;
            }

            return count;
        }
    }
}
