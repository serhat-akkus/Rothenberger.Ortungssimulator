using System;
using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public sealed class ShepardToneSweepProvider : ISampleProvider, IDisposable
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
        private readonly Action<int>? _cycleCallback;
        private readonly Action<double, double>? _freqProgressCallback;
        private readonly int _updateIntervalSamples;
        private readonly double _semitonesPerSample;

        private double _mContinuous;
        private int _lastCycleIndex;
        private int _sampleCounter;
        private bool _disposed;
        private volatile bool _stopped;

        public WaveFormat WaveFormat => _waveFormat;
        public bool IsStopped => _stopped;

        public ShepardToneSweepProvider(
            ToneType toneType,
            int sampleRate,
            Action<int>? cycleCallback,
            Action<double, double>? freqProgressCallback,
            int updateIntervalSamples = 2048)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _mMax = toneType.MMax ?? 12;
            _nMax = toneType.NMax ?? 10;
            _fMin = toneType.FMin ?? 55.0;
            _sigma = toneType.Sigma ?? 1.0;
            _octaveOffset = toneType.OctaveOffset ?? 0.0;
            _cycleCallback = cycleCallback;
            _freqProgressCallback = freqProgressCallback;
            _updateIntervalSamples = Math.Max(1, updateIntervalSamples);
            double semitonesPerSecond = toneType.SensitivitySemitonesPerSecond ?? 3.0;
            _semitonesPerSample = semitonesPerSecond / sampleRate;

            _phases = new double[_nMax];
            _amps   = new double[_nMax];
            _freqs  = new double[_nMax];
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

            for (int s = 0; s < count; s++)
            {
                _mContinuous += _semitonesPerSample;

                int currentCycleIndex = (int)Math.Floor(_mContinuous / _mMax);
                if (currentCycleIndex > _lastCycleIndex)
                {
                    _lastCycleIndex = currentCycleIndex;
                    _cycleCallback?.Invoke(_lastCycleIndex);
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
                    _freqProgressCallback?.Invoke(virtualBase, progress);
                }
            }

            return count;
        }

        public void Dispose() => _disposed = true;
    }
}