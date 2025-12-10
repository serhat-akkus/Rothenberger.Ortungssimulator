using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public class EinklinkAudioManager
    {
        private EinklinkSoundProvider? einklinkProvider;
        private AusklinkSoundProvider? ausklinkProvider;

        public EinklinkAudioManager(ToneType toneType)
        {
            double freq1 = toneType?.DoubleBeepFrequency1 ?? 261.6;
            double freq2 = toneType?.DoubleBeepFrequency2 ?? 349.2;
            int durationMs = toneType?.DoubleBeepDurationMs ?? 200;
            einklinkProvider = new EinklinkSoundProvider(freq1, freq2, durationMs);

            double ausklinkFreq1 = toneType?.AusklinkDoubleBeepFrequency1 ?? 349.2;
            double ausklinkFreq2 = toneType?.AusklinkDoubleBeepFrequency2 ?? 261.6;
            ausklinkProvider = new AusklinkSoundProvider(ausklinkFreq1, ausklinkFreq2, durationMs);
        }

        public void PlayEinklinkSound()
        {
            einklinkProvider?.Play();
        }

        public void PlayAusklinkSound()
        {
            ausklinkProvider?.Play();
        }
    }
}
