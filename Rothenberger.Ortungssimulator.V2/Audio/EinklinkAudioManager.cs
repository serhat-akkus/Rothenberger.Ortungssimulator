using Rothenberger.Ortungssimulator.V2.Audio;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public class EinklinkAudioManager
    {
        private IAudioProvider? einklinkProvider;
        private IAudioProvider? ausklinkProvider;

        public EinklinkAudioManager()
        {
            einklinkProvider = new EinklinkSoundProvider();
            ausklinkProvider = new AusklinkSoundProvider();
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
