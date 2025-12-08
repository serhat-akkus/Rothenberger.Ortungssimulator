using NAudio.Wave;
using System;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public class ShepardMouseAudioManager : IDisposable
    {
        private WaveOutEvent? waveOut;
        private ShepardToneMouseProvider? provider;
        private readonly int sampleRate;
        private readonly Models.ToneType toneType;
        private readonly int updateIntervalSamples;
        private bool isInitialized;
        private float volume;
        private readonly Action<double>? _frequencyChangedCallback;

        public ShepardMouseAudioManager(Models.ToneType toneType, int sampleRate = 44100, int updateIntervalSamples = 2048, float initialVolume = 0.5f, Action<double>? frequencyChangedCallback = null)
        {
            this.toneType = toneType;
            this.sampleRate = sampleRate;
            this.updateIntervalSamples = updateIntervalSamples;
            this.volume = initialVolume;
            _frequencyChangedCallback = frequencyChangedCallback;
        }

        public void Initialize()
        {
            if (isInitialized) return;
            provider = new ShepardToneMouseProvider(toneType, sampleRate, updateIntervalSamples, _frequencyChangedCallback);
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

        public void UpdateDistance3DState(double normalizedDistance3d)
        {
            provider?.UpdateDistance3DState(normalizedDistance3d);
        }

        public void SetVolume(float volume)
        {
            this.volume = volume;
            waveOut?.Volume = volume;
        }

        public ShepardToneMouseProvider? Provider => provider;

        public PlaybackState PlaybackState => waveOut?.PlaybackState ?? PlaybackState.Stopped;

        public void Dispose()
        {
            Stop();
        }
    }
}
