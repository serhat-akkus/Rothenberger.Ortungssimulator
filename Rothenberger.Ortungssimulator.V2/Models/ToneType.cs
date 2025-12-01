using System.Collections.Generic;

namespace Rothenberger.Ortungssimulator.V2.Models
{
    public class ToneType
    {
        public string Id { get; set; }
        public string Name { get; set; }

        // Sinus-Parameter
        public double? MinFrequency { get; set; }
        public double? MaxFrequency { get; set; }
        public string? LogBase { get; set; }
        public List<double>? Frequencies { get; set; }

        // Shepard-Parameter (aus tonetypes.json, ohne Richtung)
        public double? BaseFrequency { get; set; }
        public double? Duration { get; set; }
        public int? SampleRate { get; set; }
        public int? NumComponents { get; set; }
    }
}