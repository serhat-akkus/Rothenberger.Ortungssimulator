using System.Collections.Generic;

namespace Rothenberger.Ortungssimulator.V2.Models
{
    public class ToneType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double? MinFrequency { get; set; }
        public double? MaxFrequency { get; set; }
        public string? LogBase { get; set; }
        public List<double>? Frequencies { get; set; }
    }
}