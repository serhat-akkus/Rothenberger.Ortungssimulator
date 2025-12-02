using NAudio.Wave;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    // Erweiterung für NAudio-ISampleProvider mit steuerbaren Shepard-Parametern
    public interface IAdaptiveShepardProvider : ISampleProvider
    {
        void SetGain(float gain);
        void SetMasterLevel(float level);
        void EnableNormalization(bool enabled);
        void SetDistance(float distanceMeters);
        void SetPosition(float xMeters, float yMeters);
    }
}