using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Audio;
using System;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public class SineWaveAudioManager : IDisposable
    {
        private WaveOutEvent? waveOut;
        private SineWaveProvider? sineProvider;
        private readonly int sampleRate;
        private readonly double initialFrequency;
        private readonly float initialVolume;
        private bool isInitialized;

        public SineWaveAudioManager(int sampleRate = 44100, double frequency = 440.0, float volume = 0.0f)
        {
            this.sampleRate = sampleRate;
            this.initialFrequency = frequency;
            this.initialVolume = volume;
        }

        public void Initialize()
        {
            if (isInitialized) return;
            waveOut = new WaveOutEvent { DesiredLatency = 100 };
            sineProvider = new SineWaveProvider(sampleRate, initialFrequency, initialVolume);
            waveOut.Init(sineProvider);
            waveOut.Volume = 1.0f;
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
            sineProvider?.Dispose();
            sineProvider = null;
            isInitialized = false;
        }

        public void SetFrequency(double frequency)
        {
            sineProvider?.SetFrequency(frequency);
        }

        public void SetVolume(float volume)
        {
            sineProvider?.SetVolume(volume);
        }

        public SineWaveProvider? Provider => sineProvider;

        public void Dispose()
        {
            Stop();
        }
    }
}
