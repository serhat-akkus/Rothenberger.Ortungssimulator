using NAudio.Wave;
using System;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public sealed class ShepardToneProvider2 : IAdaptiveShepardProvider
    {
        private readonly int _sampleRate;
        private readonly float _baseFreq;
        private readonly float _centerFreq;
        private readonly float _sigma;
        private readonly float _radiusMeters;
        private readonly int _octaveHalfSpan;
        private readonly float _audibleMin;
        private readonly float _audibleMax;

        // NEU: ortsgebundene Semitone-Steuerung
        // semitonesPerMeter > 0: steigender Ton mit Distanz, < 0: fallender Ton
        private readonly float _semitonesPerMeter;
        // Startversatz (0..1) innerhalb der Oktave
        private readonly float _octaveShiftFraction;

        // LUT entfällt, wir berechnen direkt über die Formel
        private readonly float[] _phases;

        private float _currentDistance;
        private float _userGain = 1f;

        private bool _normalizationEnabled = true;
        private float _normGain = 1f;
        private float _targetPeak = 0.9f;
        private float _attack = 0.04f;
        private float _release = 0.005f;

        private float _masterLevel = 0.7f;

        private readonly object _lock = new();

        public WaveFormat WaveFormat { get; }

        public ShepardToneProvider2(
            float baseFreq = 55f,
            float radiusMeters = 5f,
            float duration = 5f,
            int sampleRate = 44100,
            int octaveHalfSpan = 5,
            float centerFreq = 440f,
            float sigma = 1.0f,
            float masterLevel = 0.7f,
            bool normalizationEnabled = true,
            float audibleMin = 55f,
            float audibleMax = 1760f,
            // NEU: Parameter für ortsgebundene Semitone-Rate und Startoffset
            float semitonesPerMeter = 12f,          // 12 st/m => 1 Oktave pro Meter
            float octaveShiftFraction = 0f)         // 0..1 Startversatz innerhalb der Oktave
        {
            _sampleRate = sampleRate;
            _baseFreq = Math.Max(20f, baseFreq);
            _centerFreq = Math.Clamp(centerFreq, 20f, 20000f);
            _sigma = Math.Max(0.05f, sigma);
            _radiusMeters = Math.Max(0.5f, radiusMeters);
            _octaveHalfSpan = Math.Max(1, octaveHalfSpan);

            _audibleMin = Math.Clamp(audibleMin, 20f, 20000f);
            _audibleMax = Math.Clamp(audibleMax, _audibleMin * 2f, 20000f);

            _semitonesPerMeter = semitonesPerMeter;
            _octaveShiftFraction = Math.Clamp(octaveShiftFraction, 0f, 1f);

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

            int numComponents = 2 * _octaveHalfSpan + 1;
            _phases = new float[numComponents];

            _currentDistance = _radiusMeters;
            _masterLevel = Math.Clamp(masterLevel, 0.01f, 4f);
            _normalizationEnabled = normalizationEnabled;

            RandomizePhases();
        }

        private void RandomizePhases()
        {
            var rng = new Random();
            for (int i = 0; i < _phases.Length; i++)
            {
                _phases[i] = (float)(rng.NextDouble() * 2.0 * Math.PI);
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
                return ComputeBaseFrequency(_currentDistance);
            }
        }

        // NEU: direkte semitone-basierte Abbildung mit Oktav-Wrap
        private float ComputeBaseFrequency(float distanceMeters)
        {
            // Nähe statt Distanz: bei Annäherung (distance ↓) steigt proximity ↑
            float proximity = MathF.Max(0f, _radiusMeters - distanceMeters);

            // Δlog2(f_base) = (semitonesPerMeter/12) * proximity
            float deltaOctaves = (_semitonesPerMeter / 12f) * proximity;

            // Startoffset innerhalb der Oktave addieren und auf [0,1) falten
            float cyclePos = deltaOctaves + _octaveShiftFraction;
            cyclePos -= MathF.Floor(cyclePos); // frac(cyclePos)

            // Basisfrequenz innerhalb einer Oktave um baseFreq
            float fBase = _baseFreq * (float)Math.Pow(2.0, cyclePos);
            return Math.Clamp(fBase, 20f, 20000f);
        }

        private float FoldToAudible(float f)
        {
            if (f <= 0f) return _audibleMin;
            while (f < _audibleMin) f *= 2f;
            while (f > _audibleMax) f /= 2f;
            return f;
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

            float baseFrequency = ComputeBaseFrequency(localDistance);

            float peak = 0f;
            int numComponents = _phases.Length;

            for (int n = 0; n < count; n++)
            {
                float sample = 0f;

                int idx = 0;
                for (int k = -_octaveHalfSpan; k <= _octaveHalfSpan; k++, idx++)
                {
                    float fComponent = baseFrequency * (float)Math.Pow(2.0, k);

                    fComponent = FoldToAudible(fComponent);
                    if (fComponent < 20f || fComponent > 20000f) continue;

                    float log2Ratio = (float)(Math.Log(fComponent / _centerFreq) / Math.Log(2.0));
                    float scaled = log2Ratio / _sigma;
                    float amp = (float)Math.Exp(-0.5f * scaled * scaled);

                    float phaseInc = 2f * MathF.PI * fComponent / _sampleRate;
                    _phases[idx] += phaseInc;
                    if (_phases[idx] > MathF.PI * 2f)
                        _phases[idx] -= MathF.PI * 2f;

                    sample += amp * MathF.Sin(_phases[idx]);
                }

                sample /= MathF.Sqrt(numComponents);

                float abs = Math.Abs(sample);
                if (abs > peak) peak = abs;

                buffer[offset + n] = sample;
            }

            if (localNormEnabled && peak > 0f)
            {
                float desiredGain = _targetPeak / peak;
                if (desiredGain < _normGain)
                {
                    _normGain = _normGain * (1f - _release) + desiredGain * _release;
                }
                else
                {
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