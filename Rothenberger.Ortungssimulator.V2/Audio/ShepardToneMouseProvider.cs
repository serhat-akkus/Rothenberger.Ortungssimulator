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

        private readonly double _shepardCyclesFromEdgeToProbe;

        private bool _disposed;
        private volatile bool _stopped;

        private readonly Action<double>? _frequencyChangedCallback;

        private readonly double _smoothingFactor;

        public WaveFormat WaveFormat => _waveFormat;
        public bool IsStopped => _stopped;
        private double _currentTNormalized = 0.5;
        private double _smoothedTNormalized = 0.5;

        
        public ShepardToneMouseProvider(ToneType toneType, int sampleRate, Action<double>? frequencyChangedCallback = null)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _mMax = toneType.MMax ?? 12;
            _nMax = toneType.NMax ?? 10;
            _fMin = toneType.FMin ?? 55.0;
            _sigma = toneType.Sigma ?? 1.0;
            _octaveOffset = toneType.OctaveOffset ?? 0.0;
            _shepardCyclesFromEdgeToProbe = toneType.ShepardCyclesFromEdgeToProbe ?? 1.0;
            _smoothingFactor = toneType.SmoothingFactor ?? 0.1;

            _phases = new double[_nMax];
            _amps = new double[_nMax];
            _freqs = new double[_nMax];

            _currentTNormalized = 0.5; // 0 = Zentrum, 1 = Rand
            _smoothedTNormalized = 0.5;
            _frequencyChangedCallback = frequencyChangedCallback;
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
            double t = 1.0 - normalizedDistance3d;
            double deltaTNormalized = 0;
            _currentTNormalized = t;

            if (_smoothingFactor > 0.0)
            {
                deltaTNormalized = _currentTNormalized - _smoothedTNormalized;
                _smoothedTNormalized += _smoothingFactor * deltaTNormalized;
            }
            else
                _smoothedTNormalized = _currentTNormalized;

            System.Diagnostics.Debug.WriteLine($"[ShepardToneMouseProvider] normalizedDistance3d (inverted): {t:F4}, " +
                $"smoothed: {_smoothedTNormalized:F4}, " +
                $"normalized: {_currentTNormalized:F4}\"" +
                $"deltaTNormalized: {deltaTNormalized:F4}\"" +
                $"smoothing factor: {_smoothingFactor:F4}");
        }

        public void RequestStop() => _stopped = true;

        private double _lastBaseFreq = 0.0;

        public double GetCurrentBaseFrequency()
        {
            return _lastBaseFreq;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_disposed || _stopped)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            double center = (_nMax - 1) / 2.0;
            // Absolutes Mapping: mContinuous wird direkt aus t berechnet
            double mShepardIndex = _shepardCyclesFromEdgeToProbe * _mMax * (1.0 - _smoothedTNormalized);
            // Oktaven-Offset auf die Basisfrequenz anwenden
            mShepardIndex -= _octaveOffset * _mMax;
            double lastFreq = 0.0;
            double baseFreq = 0.0;
            double baseEffectiveIndex = 0.0;

            for (int s = 0; s < count; s++)
            {
                double amplitudeSum = 0.0;
                for (int n = 0; n < _nMax; n++)
                {
                    double compositeIndex = n + mShepardIndex / _mMax;

                    int octaveWraps = (int)Math.Floor(compositeIndex / _nMax);
                    double effectiveIndex = compositeIndex - octaveWraps * _nMax;

                    effectiveIndex -= _octaveOffset;

                    while (effectiveIndex < 0) effectiveIndex += _nMax;
                    while (effectiveIndex >= _nMax) effectiveIndex -= _nMax;

                    double freq = _fMin * Math.Pow(2.0, effectiveIndex);
                    _freqs[n] = freq;
                    lastFreq = freq;

                    double amp = Math.Exp(-0.5 * Math.Pow((effectiveIndex - center) / _sigma, 2.0));
                    _amps[n] = amp;
                    amplitudeSum += amp;

                    // Basisfrequenz für das Zentrum berechnen (nur einmal)
                    if (n == (int)center && s == 0)
                    {
                        baseEffectiveIndex = effectiveIndex - Math.Floor(effectiveIndex);
                        baseFreq = _fMin * Math.Pow(2.0, baseEffectiveIndex - _octaveOffset);
                        _lastBaseFreq = baseFreq;
                    }
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

                if (_frequencyChangedCallback != null && s == 0)
                {
                    _frequencyChangedCallback.Invoke(baseFreq);
                }
            }

            return count;
        }

        public void Dispose() => _disposed = true;
    }
}