using NAudio.Wave;
using System;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    // Zyklischer Shepard-Ton mit symmetrischen Komponenten und Oktav-Wrap
    // - Frequenzsteuerung über Distanz (wie v1), aber Spektrum bleibt zyklisch stabil.
    // - Komponenten über mehrere Oktaven beidseitig (k ∈ [-K, +K]).
    // - Gauß-Hülle im Log2-Frequenzraum um centerFreq/sigma.
    // - Frequenzen werden in einen definierten Hörbereich gefaltet (Modulo über Oktaven).
    public sealed class ShepardToneProvider2 : IAdaptiveShepardProvider
    {
        private readonly int _sampleRate;
        private readonly float _baseFreq;
        private readonly float _centerFreq;
        private readonly float _sigma;
        private readonly float _radiusMeters;
        private readonly int _octaveHalfSpan;   // Anzahl Oktaven beidseitig (z.B. 5 -> 11 Komponenten)
        private readonly float _audibleMin;
        private readonly float _audibleMax;
        private readonly int _lutResolution;

        private readonly float[] _frequencyLut;
        private readonly float[] _phases;

        private float _currentDistance;
        private float _userGain = 1f;

        // Optional: leichte Pegelstabilisierung
        private bool _normalizationEnabled = true;
        private float _normGain = 1f;
        private float _targetPeak = 0.9f;
        private float _attack = 0.04f;
        private float _release = 0.005f;

        private float _masterLevel = 0.7f;

        private readonly object _lock = new();

        public WaveFormat WaveFormat { get; }

        public ShepardToneProvider2(
            float baseFreq = 55f,            // Grundton (z.B. A1)
            float radiusMeters = 5f,
            float duration = 5f,             // nur Signatur
            int sampleRate = 44100,
            int octaveHalfSpan = 5,          // ergibt 2*octaveHalfSpan+1 Komponenten
            int lutResolution = 2048,
            float centerFreq = 440f,         // Zentrum der Gauß-Hülle
            float sigma = 1.0f,              // Breite der Hülle im Log2-Raum
            float masterLevel = 0.7f,
            bool normalizationEnabled = true,
            float audibleMin = 55f,
            float audibleMax = 1760f)        // 55 Hz bis 1760 Hz (A1..A6)
        {
            _sampleRate = sampleRate;
            _baseFreq = Math.Max(20f, baseFreq);
            _centerFreq = Math.Clamp(centerFreq, 20f, 20000f);
            _sigma = Math.Max(0.05f, sigma);
            _radiusMeters = Math.Max(0.5f, radiusMeters);
            _octaveHalfSpan = Math.Max(1, octaveHalfSpan);
            _lutResolution = Math.Clamp(lutResolution, 32, 8192);

            _audibleMin = Math.Clamp(audibleMin, 20f, 20000f);
            _audibleMax = Math.Clamp(audibleMax, _audibleMin * 2f, 20000f);

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _frequencyLut = new float[_lutResolution];

            int numComponents = 2 * _octaveHalfSpan + 1;
            _phases = new float[numComponents];

            _currentDistance = _radiusMeters;
            _masterLevel = Math.Clamp(masterLevel, 0.01f, 4f);
            _normalizationEnabled = normalizationEnabled;

            BuildFrequencyLut();
            RandomizePhases();
        }

        private void BuildFrequencyLut()
        {
            // Distanz -> Basisfrequenz im Bereich [baseFreq, baseFreq*2) zyklisch (eine Oktave),
            // sodass die Illusion bei Bewegung umläuft.
            for (int i = 0; i < _lutResolution; i++)
            {
                float n = (float)i / (_lutResolution - 1); // 0..1
                // Map auf eine Oktave: baseFreq * 2^n
                float f = _baseFreq * (float)Math.Pow(2.0, n);
                f = Math.Clamp(f, 20f, 20000f);
                _frequencyLut[i] = f;
            }
        }

        private void RandomizePhases()
        {
            // Zufällige Startphasen vermeiden Artefakte
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
                return LookupFrequencyWrapped(_currentDistance);
            }
        }

        private float LookupFrequencyWrapped(float distance)
        {
            // Normiere Distanz auf 0..1 und invertiere sie:
            // nahe (n≈0) -> hohe Frequenz, fern (n≈1) -> niedrige Frequenz
            float n = distance / _radiusMeters;     // 0..1
            float inv = 1f - n;

            // Interpolationsposition (zyklisch über eine Oktave)
            float pos = inv * (_lutResolution - 1);
            int i0 = (int)pos;
            int i1 = (i0 + 1) % _lutResolution;
            float frac = pos - i0;

            float f0 = _frequencyLut[i0];
            float f1 = _frequencyLut[i1];
            return f0 + (f1 - f0) * frac;
        }

        // Faltet Frequenzen zyklisch in den hörbaren Bereich [audibleMin, audibleMax]
        private float FoldToAudible(float f)
        {
            if (f <= 0f) return _audibleMin;
            // Bringe f durch Multiplikation/Division mit 2^k in den Bereich
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

            float baseFrequency = LookupFrequencyWrapped(localDistance);

            float peak = 0f;
            int numComponents = _phases.Length;

            for (int n = 0; n < count; n++)
            {
                float sample = 0f;

                // Komponenten über k ∈ [-octaveHalfSpan, +octaveHalfSpan]
                int idx = 0;
                for (int k = -_octaveHalfSpan; k <= _octaveHalfSpan; k++, idx++)
                {
                    // Frequenz mit Oktavversatz
                    float fComponent = baseFrequency * (float)Math.Pow(2.0, k);

                    // In hörbaren Bereich falten (zyklisch)
                    fComponent = FoldToAudible(fComponent);
                    if (fComponent < 20f || fComponent > 20000f) continue;

                    // Gauß-Hülle im Log2-Raum um centerFreq
                    float log2Ratio = (float)(Math.Log(fComponent / _centerFreq) / Math.Log(2.0));
                    float scaled = log2Ratio / _sigma;
                    float amp = (float)Math.Exp(-0.5f * scaled * scaled);

                    // Phase und Beitrag
                    float phaseInc = 2f * MathF.PI * fComponent / _sampleRate;
                    _phases[idx] += phaseInc;
                    if (_phases[idx] > MathF.PI * 2f)
                        _phases[idx] -= MathF.PI * 2f;

                    sample += amp * MathF.Sin(_phases[idx]);
                }

                // Pegel normalisieren nach Anzahl Komponenten
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