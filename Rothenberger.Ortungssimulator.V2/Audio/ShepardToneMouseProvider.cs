using System;
using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Models;

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

        private readonly double _sensitivitySemitonesPerSecond; // Skaliert die Tonhöhenänderung je normierter T-Geschwindigkeit

        private double _mContinuous;
        private bool _disposed;
        private volatile bool _stopped;

        // Maus-/Distanz-Zustände
        private double _lastTNormalized;    // zur Geschwindigkeitsberechnung
        private long _lastUpdateTicks;      // ms (Environment.TickCount64)
        private double _semitoneVelocityPerSecond; // aus T-Geschwindigkeit abgeleitet

        public WaveFormat WaveFormat => _waveFormat;
        public bool IsStopped => _stopped;
        private double _maxSemitoneVelocityPerSecond = MaxSemitoneVelocityPerSecond;
        private const double MaxSemitoneVelocityPerSecond = 3.0;

        /// <summary>
        /// Shepard-Ton Provider, dessen Parameter direkt aus einem ToneType-Objekt gelesen werden.
        /// Die Tonhöhenänderung und weitere Eigenschaften werden aus der Konfiguration des ToneType bestimmt.
        /// </summary>
        /// <param name="toneType">Konfigurationsobjekt mit allen relevanten Parametern für den Shepard-Ton</param>
        /// <param name="sampleRate">Samplerate</param>
        /// <param name="updateIntervalSamples">Intervall für Progress-Callback</param>
        public ShepardToneMouseProvider(ToneType toneType, int sampleRate, int updateIntervalSamples = 2048)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _mMax = toneType.MMax ?? 12;
            _nMax = toneType.NMax ?? 10;
            _fMin = toneType.FMin ?? 55.0;
            _sigma = toneType.Sigma ?? 1.0;
            _octaveOffset = toneType.OctaveOffset ?? 0.0;
            _sensitivitySemitonesPerSecond = toneType.SensitivitySemitonesPerSecond ?? 3.0;
            _maxSemitoneVelocityPerSecond = toneType.MaxSemitoneVelocityPerSecond ?? MaxSemitoneVelocityPerSecond;

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
        public void UpdateDistance3DState(double normalizedDistance3d)
        {
            // Invertiere den Wert, damit Annäherung (Maus näher an Sonde) zu steigendem Ton führt
            double t = 1.0 - normalizedDistance3d;

            long nowTicks = Environment.TickCount64;
            double dtSec = GetElapsedSecondsSinceLastUpdate(nowTicks);

            System.Diagnostics.Debug.WriteLine($"[ShepardToneMouseProvider] normalizedDistance3d (inverted): {t:F4}");

            double tVelocityPerSecond = (t - _lastTNormalized) / dtSec;
            _semitoneVelocityPerSecond = UpdateSemitoneVelocity(tVelocityPerSecond, _maxSemitoneVelocityPerSecond, _sensitivitySemitonesPerSecond);

            _lastTNormalized = t;
            _lastUpdateTicks = nowTicks;
        }

        private static double UpdateSemitoneVelocity(double tVelocityPerSecond, double velocityCap, double sensitivitySemitonesPerSecond) =>
            // Semiton-Geschwindigkeit: invertiere Vorzeichen so, dass Annäherung -> steigend (positiv)
            // t sinkt bei Annäherung, tVelocity < 0, dadurch -tVelocity > 0 -> steigender Ton.
            velocityCap == 0
                ? -tVelocityPerSecond * sensitivitySemitonesPerSecond
                : Math.Clamp(
                    -tVelocityPerSecond * sensitivitySemitonesPerSecond,
                    -velocityCap,
                    velocityCap);

        private double GetElapsedSecondsSinceLastUpdate(long nowTicks)
        {
            long dtMs = nowTicks - _lastUpdateTicks;
            if (dtMs <= 0) dtMs = 1; // Schutz gegen Division durch 0
            double dtSec = dtMs / 1000.0;
            return dtSec;
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
            }

            return count;
        }

        public void Dispose() => _disposed = true;
    }
}