using System;
using NAudio.Wave;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    /// <summary>
    /// Shepard-Ton Provider, dessen Basisfrequenz-Progress ausschließlich durch Mausbewegung
    /// relativ zur Sonde gesteuert wird:
    /// - Bewegung zur Sonde (t ↓ Richtung 0) -> steigender Ton (mContinuous ↑)
    /// - Bewegung von der Sonde weg (t ↑ Richtung 1) -> fallender Ton (mContinuous ↓)
    /// - Stillstand -> Tonhöhe konstant
    /// </summary>
    public sealed class ShepardToneMouseProvider : ISampleProvider, IDisposable
    {
        private readonly WaveFormat _waveFormat;
        private readonly int _mMax;
        private readonly int _nMax;
        private readonly double _fMin;
        private readonly double _sigma;
        private readonly double _octaveOffset;
        private readonly double[] _phases;
        private readonly double[] _amps;
        private readonly double[] _freqs;

        private readonly int _updateIntervalSamples;
        private readonly double _sensitivitySemitonesPerSecond; // Skaliert die Tonhöhenänderung je normierter T-Geschwindigkeit

        private double _mContinuous;
        private int _lastCycleIndex;
        private int _sampleCounter;
        private bool _disposed;
        private volatile bool _stopped;

        // Maus-/Distanz-Zustände
        private double _lastTNormalized;    // zur Geschwindigkeitsberechnung
        private long _lastUpdateTicks;      // ms (Environment.TickCount64)
        private double _semitoneVelocityPerSecond; // aus T-Geschwindigkeit abgeleitet

        private readonly Action<double>? frequencyCallback;

        public WaveFormat WaveFormat => _waveFormat;
        public bool IsStopped => _stopped;

        /// <param name="sampleRate">Samplerate</param>
        /// <param name="mMax">Anzahl Halbtöne pro Zyklus (z.B. 12)</param>
        /// <param name="nMax">Anzahl überlagerter Sinus-Obertöne (z.B. 12)</param>
        /// <param name="fMin">Basisfrequenz für Index 0 (z.B. 55 Hz)</param>
        /// <param name="sigma">Breite der Gauß-Amplitudenverteilung</param>
        /// <param name="sensitivitySemitonesPerSecond">
        /// Skala, wie stark die Tonhöhe je Sekunde bei maximaler normierter Annäherungs-/Entfernungs-Rate (|dt/ds|=1) verändert wird.
        /// Beispiel: 12 -> eine Oktave pro Sekunde bei voller Annäherungsgeschwindigkeit.
        /// </param>
        /// <param name="octaveOffset">Optionaler Oktavversatz</param>
        /// <param name="updateIntervalSamples">Intervall für Progress-Callback</param>
        /// <param name="frequencyCallback"></param>
        public ShepardToneMouseProvider(
            int sampleRate,
            int mMax,
            int nMax,
            double fMin,
            double sigma,
            double sensitivitySemitonesPerSecond,
            Action<double>? frequencyCallback = null,
            double octaveOffset = 0.0,
            int updateIntervalSamples = 2048)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _mMax = mMax;
            _nMax = nMax;
            _fMin = fMin;
            _sigma = sigma;
            _octaveOffset = octaveOffset;
            this.frequencyCallback = frequencyCallback;

            _updateIntervalSamples = Math.Max(128, updateIntervalSamples);
            _sensitivitySemitonesPerSecond = sensitivitySemitonesPerSecond;

            _phases = new double[_nMax];
            _amps = new double[_nMax];
            _freqs = new double[_nMax];

            _lastUpdateTicks = Environment.TickCount64;
            _lastTNormalized = 0.5; // Neutraler Start
        }

        /// <summary>
        /// Wird bei Mausbewegung aufgerufen. Berechnet die 3D-Distanz und normiert sie,
        /// bestimmt daraus die Änderungsrate von t und leitet eine Semiton-Geschwindigkeit pro Sekunde ab.
        /// </summary>
        /// <param name="horiz2dMeters">Horizontale 2D-Distanz Maus->Sonde</param>
        /// <param name="probeDepthMeters">Sondentiefe</param>
        /// <param name="probeRadius3DMeters">Radius für Normalisierung</param>
        public void UpdateDistance3DState(double horiz2dMeters, double probeDepthMeters, double probeRadius3DMeters)
        {
            double d3 = Distance3DHelper.Compute3D(horiz2dMeters, probeDepthMeters);
            double t = Distance3DHelper.Normalize(d3, probeDepthMeters, probeRadius3DMeters); // 0=nah, 1=fern

            // Zeitdifferenz seit letztem Update
            long nowTicks = Environment.TickCount64;
            long dtMs = nowTicks - _lastUpdateTicks;
            if (dtMs <= 0) dtMs = 1; // Schutz gegen Division durch 0
            double dtSec = dtMs / 1000.0;

            // t-Geschwindigkeit: negativ bei Annäherung (t sinkt), positiv bei Entfernung (t steigt)
            double tVelocityPerSecond = (t - _lastTNormalized) / dtSec;

            // Semiton-Geschwindigkeit: invertiere Vorzeichen so, dass Annäherung -> steigend (positiv)
            // t sinkt bei Annäherung, tVelocity < 0, dadurch -tVelocity > 0 -> steigender Ton.
            _semitoneVelocityPerSecond = -tVelocityPerSecond * _sensitivitySemitonesPerSecond;

            // Zustände aktualisieren
            _lastTNormalized = t;
            _lastUpdateTicks = nowTicks;
        }

        public void RequestStop() => _stopped = true;

        public int Read(float[] buffer, int offset, int count)
        {
            if (_disposed || _stopped)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            double center = (_nMax - 1) / 2.0;
            double semitonesPerSample = _semitoneVelocityPerSecond / _waveFormat.SampleRate;

            for (int s = 0; s < count; s++)
            {
                // Tonhöhenänderung ausschließlich aus Mausbewegung
                _mContinuous += semitonesPerSample;

                int currentCycleIndex = (int)Math.Floor(_mContinuous / _mMax);
                if (currentCycleIndex > _lastCycleIndex)
                {
                    _lastCycleIndex = currentCycleIndex;
                }

                double amplitudeSum = 0.0;
                for (int n = 0; n < _nMax; n++)
                {
                    double compositeIndex = n + _mContinuous / _mMax;

                    int octaveWraps = (int)Math.Floor(compositeIndex / _nMax);
                    double effectiveIndex = compositeIndex - octaveWraps * _nMax;

                    effectiveIndex -= _octaveOffset;

                    while (effectiveIndex < 0) effectiveIndex += _nMax;
                    while (effectiveIndex >= _nMax) effectiveIndex -= _nMax;

                    double freq = _fMin * Math.Pow(2.0, effectiveIndex);
                    _freqs[n] = freq;

                    double amp = Math.Exp(-0.5 * Math.Pow((effectiveIndex - center) / _sigma, 2.0));
                    _amps[n] = amp;
                    amplitudeSum += amp;
                }

                double sampleValue = 0.0;
                for (int n = 0; n < _nMax; n++)
                {
                    sampleValue += _amps[n] * Math.Sin(_phases[n]);
                    _phases[n] += 2.0 * Math.PI * _freqs[n] / _waveFormat.SampleRate;
                    if (_phases[n] >= 2.0 * Math.PI) _phases[n] -= 2.0 * Math.PI;
                }

                if (amplitudeSum > 1e-9)
                    sampleValue /= amplitudeSum;

                buffer[offset + s] = (float)sampleValue;

                _sampleCounter++;
                if (_sampleCounter >= _updateIntervalSamples)
                {
                    _sampleCounter = 0;
                    double progress = (_mContinuous % _mMax) / _mMax;
                    double virtualBase = _fMin * Math.Pow(2.0, (progress * _nMax - _octaveOffset) / _nMax);
                    frequencyCallback?.Invoke(virtualBase);
                }
            }

            return count;
        }

        public void Dispose() => _disposed = true;
    }
}