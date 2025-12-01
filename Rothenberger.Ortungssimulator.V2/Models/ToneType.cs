using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Rothenberger.Ortungssimulator.V2.Models
{
    public class ToneType
    {
        public string Id { get; set; }
        public string Name { get; set; }

        // Sinus
        [JsonPropertyName("min_frequency")]
        public double? MinFrequency { get; set; }

        [JsonPropertyName("max_frequency")]
        public double? MaxFrequency { get; set; }

        [JsonPropertyName("log_base")]
        public string? LogBase { get; set; }

        [JsonPropertyName("frequencies")]
        public List<double>? Frequencies { get; set; }

        // Shepard Basis
        [JsonPropertyName("base_freq")]
        public double? BaseFrequency { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; } // nur Signatur

        [JsonPropertyName("fs")]
        public int? SampleRate { get; set; }

        [JsonPropertyName("num_tones")]
        public int? NumComponents { get; set; }

        [JsonPropertyName("radius_meters")]
        public double? RadiusMeters { get; set; }

        [JsonPropertyName("octave_span")]
        public double? OctaveSpan { get; set; }

        [JsonPropertyName("lut_resolution")]
        public int? LutResolution { get; set; }

        // Amplitudenform
        [JsonPropertyName("center_freq")]
        public double? CenterFrequency { get; set; }

        [JsonPropertyName("sigma")]
        public double? ShepardSigma { get; set; }

        // Neue Lautstärke-/Normalisierungsparameter
        [JsonPropertyName("master_level")]
        public double? MasterLevel { get; set; }

        [JsonPropertyName("normalization_enabled")]
        public bool? NormalizationEnabled { get; set; }
    }
}