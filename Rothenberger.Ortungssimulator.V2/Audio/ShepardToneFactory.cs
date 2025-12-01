using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public static class ShepardToneFactory
    {
        public static ShepardToneProvider2 CreateFromToneType(ToneType type)
        {
            return new ShepardToneProvider2(
                baseFreq: (float)(type.MinFrequency ?? 55f),
                centerFreq: (float)(type.CenterFrequency ?? 440f),
                sigma: (float)(type.Sigma ?? 1.0f),
                octaveHalfSpan: type.OctaveHalfSpan ?? 5,
                masterLevel: (float)(type.MasterLevel ?? 0.7f),
                normalizationEnabled: type.Normalize ?? true,
                audibleMin: (float)(type.AudibleMin ?? 55f),
                audibleMax: (float)(type.AudibleMax ?? 1760f),
                semitonesPerMeter: (float)(type.SemitonesPerMeter ?? 12f),
                octaveShiftFraction: (float)(type.OctaveShiftFraction ?? 0f)
            );
        }
    }
}