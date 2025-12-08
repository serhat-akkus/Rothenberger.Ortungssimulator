using NAudio.Wave;
using System;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public class ShepardDiscreteAudioManager : IDisposable
    {
        private WaveOutEvent? waveOut;
        private ShepardDiscreteProvider? provider;
        private readonly int sampleRate;
        private readonly Models.ToneType toneType;
        private readonly int mMax;
        private readonly int nMax;
        private bool isInitialized;
        private float volume;

        public ShepardDiscreteAudioManager(Models.ToneType toneType, int sampleRate = 44100, float initialVolume = 0.5f)
        {
            this.toneType = toneType;
            this.sampleRate = sampleRate;
            this.volume = initialVolume;
            mMax = toneType.MMax ?? 12;
            nMax = toneType.NMax ?? 10;
            fMin = toneType.FMin ?? 55.0;
            lMin = toneType.LMin ?? 0.1;
            lMax = toneType.LMax ?? 1.0;
        }

        private readonly double fMin;
        private readonly double lMin;
        private readonly double lMax;

        public void Initialize()
        {
            if (isInitialized) return;
            provider = new ShepardDiscreteProvider(toneType, sampleRate, mMax, nMax, lMin, lMax);
            waveOut = new WaveOutEvent { DesiredLatency = 100 };
            waveOut.Init(provider);
            waveOut.Volume = volume;
            isInitialized = true;
        }

        public void Play()
        {
            if (!isInitialized) Initialize();
            if (waveOut != null && waveOut.PlaybackState != PlaybackState.Playing)
                waveOut.Play();
        }

        public void Pause()
        {
            if (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                waveOut.Pause();
        }

        public void Stop()
        {
            if (waveOut != null)
            {
                try { waveOut.Stop(); } catch { }
                waveOut.Dispose();
                waveOut = null;
            }
            provider?.Dispose();
            provider = null;
            isInitialized = false;
        }

        public void SetCurrentStep(int m)
        {
            provider?.SetCurrentStep(m);
        }

        public void SetVolume(float volume)
        {
            this.volume = volume;
            waveOut?.Volume = volume;
        }

        public ShepardDiscreteProvider? Provider => provider;

        public PlaybackState PlaybackState => waveOut?.PlaybackState ?? PlaybackState.Stopped;

        public void Dispose()
        {
            Stop();
        }
    }
}
