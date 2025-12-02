using NAudio.Wave;
using System;
using System.Diagnostics;

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

        private readonly float _semitonesPerMeter;
        private readonly float _octaveShiftFraction;

        private readonly float[] _phases;

        private float _currentDistance;
        private float _depthMeters; // NEU: aktuelle Tiefe der Sondenmitte (m)
        private float _userGain = 1f;

        private bool _normalizationEnabled = false;
        private float _normGain = 1f;
        private float _targetPeak = 0.9f;
        private float _attack = 0.04f;
        private float _release = 0.005f;

        private float _masterLevel = 0.7f;

        // Probe-Steuerung
        private bool _probeEnabled = false;
        private int _probeInterval = 2048;
        private int _probeOffsetModulo = 0;

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
            bool normalizationEnabled = false, // Normalisierung aus
            float semitonesPerMeter = 12f,
            float octaveShiftFraction = 0f)
        {
            _sampleRate = sampleRate;
            _baseFreq = Math.Max(0.001f, baseFreq);
            _centerFreq = Math.Max(0.001f, centerFreq);
            _sigma = Math.Max(0.05f, sigma);
            _radiusMeters = Math.Max(0.5f, radiusMeters);
            _octaveHalfSpan = Math.Max(1, octaveHalfSpan);

            _semitonesPerMeter = semitonesPerMeter;
            _octaveShiftFraction = Math.Clamp(octaveShiftFraction, 0f, 1f);

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

            int numComponents = 2 * _octaveHalfSpan + 1;
            _phases = new float[numComponents];

            _currentDistance = _radiusMeters;
            _depthMeters = 0f; // Standard: Oberfläche
            _masterLevel = Math.Clamp(masterLevel, 0.01f, 4f);
            _normalizationEnabled = normalizationEnabled;

            RandomizePhases();

            // Probe beim Start aktivieren (Standardwerte: alle 2048 Samples)
            EnableSampleProbe(true, intervalSamples: 2048, intervalPhaseOffset: 0);
        }

        private void RandomizePhases()
        {
            var rng = new Random();
            for (int i = 0; i < _phases.Length; i++)
            {
                _phases[i] = (float)(rng.NextDouble() * 2.0 * Math.PI);
            }
        }

        public void EnableSampleProbe(bool enabled, int intervalSamples = 2048, int intervalPhaseOffset = 0)
        {
            lock (_lock)
            {
                _probeEnabled = enabled;
                _probeInterval = Math.Max(1, intervalSamples);
                _probeOffsetModulo = Math.Clamp(intervalPhaseOffset, 0, _probeInterval - 1);
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

        public void SetDepth(float depthMeters)
        {
            lock (_lock)
            {
                _depthMeters = Math.Clamp(depthMeters, 0f, _radiusMeters);
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
                return ComputeBaseFrequency(_currentDistance, _depthMeters);
            }
        }

        private float ComputeBaseFrequency(float distanceMeters, float depthMeters)
        {
            float R = _radiusMeters;
            float z = depthMeters;
            float r2dSq = R * R - z * z;
            if (r2dSq <= 0f)
            {
                return _baseFreq;
            }

            float r2d = MathF.Sqrt(r2dSq);
            float normalized = Math.Clamp(distanceMeters / r2d, 0f, 1f);
            float proximityNormalized = 1f - normalized;

            float deltaOctaves = (_semitonesPerMeter / 12f) * proximityNormalized;

            float cyclePos = deltaOctaves + _octaveShiftFraction;
            cyclePos = cyclePos - MathF.Floor(cyclePos); // in [0,1)

            float fBase = _baseFreq * (float)Math.Pow(2.0, cyclePos);
            return Math.Max(0.001f, fBase);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            float localGain;
            float localDistance;
            float localDepth;
            bool localNormEnabled; // bleibt vorhanden, wird aber ignoriert
            float localMaster;
            bool localProbeEnabled;
            int localProbeInterval;
            int localProbeOffsetModulo;

            lock (_lock)
            {
                localGain = _userGain;
                localDistance = _currentDistance;
                localDepth = _depthMeters;
                localNormEnabled = _normalizationEnabled;
                localMaster = _masterLevel;

                localProbeEnabled = _probeEnabled;
                localProbeInterval = _probeInterval;
                localProbeOffsetModulo = _probeOffsetModulo;
            }

            float baseFrequency = ComputeBaseFrequency(localDistance, localDepth);

            float peak = 0f;
            int numComponents = _phases.Length;

            for (int n = 0; n < count; n++)
            {
                float sample = 0f;

                int idx = 0;
                for (int k = -_octaveHalfSpan; k <= _octaveHalfSpan; k++, idx++)
                {
                    float fComponent = baseFrequency * (float)Math.Pow(2.0, k);

                    float log2Ratio = (float)(Math.Log(fComponent / _centerFreq) / Math.Log(2.0));
                    float scaled = log2Ratio / _sigma;
                    float amp = (float)Math.Exp(-0.5f * scaled * scaled);

                    float phaseInc = 2f * MathF.PI * fComponent / _sampleRate;
                    _phases[idx] += phaseInc;

                    if (_phases[idx] > MathF.PI * 2f)
                        _phases[idx] -= MathF.PI * 2f;
                    else if (_phases[idx] < 0f)
                        _phases[idx] += MathF.PI * 2f;

                    sample += amp * MathF.Sin(_phases[idx]);
                }

                sample /= MathF.Sqrt(numComponents);

                float abs = Math.Abs(sample);
                if (abs > peak) peak = abs;

                buffer[offset + n] = sample;

                // Stichprobe ROH: direkt an der ausgewählten Stelle
                if (localProbeEnabled && ((n + localProbeOffsetModulo) % localProbeInterval == 0))
                {
                    Debug.WriteLine($"[ShepardProbe] n={n}, raw={sample:F6}, dist={localDistance:F3}m, depth={localDepth:F3}m, fBase={baseFrequency:F3}Hz");
                }
            }

            // Dynamische Normalisierung entfernt: kein Update von _normGain

            // Konstanter finaler Gain nur über User/Master
            float finalGain = localGain * localMaster;

            for (int n = 0; n < count; n++)
            {
                buffer[offset + n] *= finalGain;

                // Stichprobe FINAL: nach Gain/Normalisierung
                if (localProbeEnabled && ((n + localProbeOffsetModulo) % localProbeInterval == 0))
                {
                    Debug.WriteLine($"[ShepardProbe] n={n}, final={buffer[offset + n]:F6}, gain={finalGain:F4}");
                }
            }

            return count;
        }
    }
}