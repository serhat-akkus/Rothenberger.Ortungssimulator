using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public static class ShepardToneFactory
    {
        public static ShepardToneProvider2 CreateFromToneType(ToneType toneType)
        {
            float baseFreq     = (float)(toneType.BaseFrequency   ?? 440.0);
            float radius       = (float)(toneType.RadiusMeters    ?? 5.0);
            int sampleRate     = toneType.SampleRate              ?? 44100;
            float octaveSpan   = (float)(toneType.OctaveSpan      ?? 2.0);
            int lutResolution  = toneType.LutResolution           ?? 2048;
            float centerFreq   = (float)(toneType.CenterFrequency ?? baseFreq);
            float sigma        = (float)(toneType.ShepardSigma    ?? 1.0f);
            float masterLevel  = (float)(toneType.MasterLevel     ?? 0.7f);
            bool normalization = toneType.NormalizationEnabled    ?? true;

            int octaveHalfSpan = toneType.NumComponents.HasValue && toneType.NumComponents.Value > 1
                ? Math.Max(1, (toneType.NumComponents.Value - 1) / 2)
                : Math.Max(1, (int)Math.Round(octaveSpan));

            float audibleMin = (float)(toneType.MinFrequency ?? 55.0);
            float audibleMax = (float)(toneType.MaxFrequency ?? 1760.0);

            // Neu: Semitone-Rate pro Meter und Startversatz innerhalb der Oktave
            float semitonesPerMeter = toneType.SemitonesPerMeter ?? (12f / radius); // 1 Oktave pro Radius als Default
            float octaveShiftFraction = toneType.OctaveShiftFraction ?? 0f;

            return new ShepardToneProvider2(
                baseFreq: baseFreq,
                radiusMeters: radius,
                duration: (float)(toneType.Duration ?? 5.0),
                sampleRate: sampleRate,
                octaveHalfSpan: octaveHalfSpan,
                centerFreq: centerFreq,
                sigma: sigma,
                masterLevel: masterLevel,
                normalizationEnabled: normalization,
                audibleMin: audibleMin,
                audibleMax: audibleMax,
                semitonesPerMeter: semitonesPerMeter,
                octaveShiftFraction: octaveShiftFraction);
        }
    }
}