using System;
using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public class ShepardDiscreteProvider : ISampleProvider, IDisposable
    {
        private readonly WaveFormat _waveFormat;
        private readonly int _mMax;
        private readonly int _nMax;
        private readonly double _fMin;
        private readonly double _lMin;
        private readonly double _lMax;
        private readonly double[] _phases;
        private readonly double[] _amps;
        private readonly double[] _freqs;
        private int _currentStep; // m: aktueller Tonleiter-Schritt
        private bool _disposed;
        private volatile bool _stopped;

        public WaveFormat WaveFormat => _waveFormat;
        public bool IsStopped => _stopped;

        public ShepardDiscreteProvider(ToneType toneType, int sampleRate, int mMax = 12, int nMax = 10, double lMin = 0.1, double lMax = 1.0)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _mMax = mMax;
            _nMax = nMax;
            _fMin = toneType.FMin ?? 55.0;
            _lMin = lMin;
            _lMax = lMax;
            _phases = new double[_nMax];
            _amps = new double[_nMax];
            _freqs = new double[_nMax];
            _currentStep = 1;
        }

        public void SetCurrentStep(int m)
        {
            _currentStep = Math.Max(1, Math.Min(_mMax, m));
        }

        public void RequestStop() => _stopped = true;

        public int Read(float[] buffer, int offset, int count)
        {
            if (_disposed || _stopped)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            for (int s = 0; s < count; s++)
            {
                double amplitudeSum = 0.0;
                double sampleValue = 0.0;
                for (int n = 1; n <= _nMax; n++)
                {
                    // Frequenz nach Formel
                    double freq = _fMin * Math.Pow(2.0, ((n - 1) * _mMax + _currentStep - 1) / (double)_mMax);
                    _freqs[n - 1] = freq;

                    // Amplitude nach Formel
                    double theta = 2.0 * Math.PI * ((n - 1) * _mMax + _currentStep - 1) / (_mMax * _nMax);
                    double amp = _lMin + (_lMax - _lMin) * (1.0 - Math.Cos(theta));
                    _amps[n - 1] = amp;
                    amplitudeSum += amp;

                    sampleValue += amp * Math.Sin(_phases[n - 1]);
                    _phases[n - 1] += 2.0 * Math.PI * freq / _waveFormat.SampleRate;
                    if (_phases[n - 1] >= 2.0 * Math.PI) _phases[n - 1] -= 2.0 * Math.PI;
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
