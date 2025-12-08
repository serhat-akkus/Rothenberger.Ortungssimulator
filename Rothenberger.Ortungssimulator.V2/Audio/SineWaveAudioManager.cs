using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Audio;
using Rothenberger.Ortungssimulator.V2.Models;
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
        private readonly ToneType toneType;
        private bool isInitialized;

        public SineWaveAudioManager(ToneType toneType, int sampleRate = 44100, double frequency = 440.0, float volume = 0.0f)
        {
            this.toneType = toneType;
            this.sampleRate = sampleRate;
            this.initialFrequency = frequency;
            this.initialVolume = volume;
        }

        public void Initialize()
        {
            if (isInitialized) return;
            sineProvider = new SineWaveProvider(sampleRate, initialFrequency, initialVolume);
            int latency = toneType.LatencyInMs ?? 100;
            waveOut = new WaveOutEvent { DesiredLatency = latency };
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
