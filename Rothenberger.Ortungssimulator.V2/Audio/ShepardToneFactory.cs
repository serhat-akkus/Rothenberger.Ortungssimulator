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
            int lutResolution  = toneType.LutResolution           ?? 2048; // v2 Default ist 2048
            float centerFreq   = (float)(toneType.CenterFrequency ?? baseFreq);
            float sigma        = (float)(toneType.ShepardSigma    ?? 1.0f);
            float masterLevel  = (float)(toneType.MasterLevel     ?? 0.7f);
            bool normalization = toneType.NormalizationEnabled    ?? true;

            // v2: octaveHalfSpan statt numComponents; Herleitung:
            // Wenn NumComponents gesetzt ist: halfSpan = (NumComponents - 1) / 2
            // Sonst aus OctaveSpan (Anzahl Oktaven beidseitig abschätzen)
            int octaveHalfSpan;
            if (toneType.NumComponents.HasValue && toneType.NumComponents.Value > 1)
            {
                octaveHalfSpan = Math.Max(1, (toneType.NumComponents.Value - 1) / 2);
            }
            else
            {
                // aus OctaveSpan eine beidseitige Spannweite ableiten (mind. 1)
                // z.B. OctaveSpan=2.0 -> halfSpan=2
                octaveHalfSpan = Math.Max(1, (int)Math.Round(octaveSpan));
            }

            // Optionale Hörbereichsbegrenzung aus ToneType übernehmen, falls vorhanden
            float audibleMin = (float)(toneType.MinFrequency ?? 55.0);
            float audibleMax = (float)(toneType.MaxFrequency ?? 1760.0);

            return new ShepardToneProvider2(
                baseFreq: baseFreq,
                radiusMeters: radius,
                duration: (float)(toneType.Duration ?? 5.0),
                sampleRate: sampleRate,
                octaveHalfSpan: octaveHalfSpan,
                lutResolution: lutResolution,
                centerFreq: centerFreq,
                sigma: sigma,
                masterLevel: masterLevel,
                normalizationEnabled: normalization,
                audibleMin: audibleMin,
                audibleMax: audibleMax);
        }
    }
}