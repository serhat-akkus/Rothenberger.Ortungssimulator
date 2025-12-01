using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public static class ShepardToneFactory
    {
        public static ShepardToneProvider CreateFromToneType(ToneType toneType)
        {
            float baseFreq      = (float)(toneType.BaseFrequency       ?? 440.0);
            float radius        = (float)(toneType.RadiusMeters        ?? 5.0);
            int sampleRate      = toneType.SampleRate                  ?? 44100;
            int numComponents   = toneType.NumComponents               ?? 10;
            float octaveSpan    = (float)(toneType.OctaveSpan          ?? 2.0);
            int lutResolution   = toneType.LutResolution               ?? 512;
            float centerFreq    = (float)(toneType.CenterFrequency     ?? baseFreq);
            float sigma         = (float)(toneType.ShepardSigma        ?? 1.0f);
            float masterLevel   = (float)(toneType.MasterLevel         ?? 0.7f);
            bool normalization  = toneType.NormalizationEnabled        ?? true;

            return new ShepardToneProvider(
                baseFreq: baseFreq,
                radiusMeters: radius,
                duration: (float)(toneType.Duration ?? 5.0),
                sampleRate: sampleRate,
                numComponents: numComponents,
                octaveSpan: octaveSpan,
                lutResolution: lutResolution,
                centerFreq: centerFreq,
                sigma: sigma,
                masterLevel: masterLevel,
                normalizationEnabled: normalization);
        }
    }
}