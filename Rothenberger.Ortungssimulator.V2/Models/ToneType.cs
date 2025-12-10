using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Rothenberger.Ortungssimulator.V2.Models
{
    public class ToneType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double? MinFrequency { get; set; }
        public double? MaxFrequency { get; set; }
        public string LogBase { get; set; }
        public List<double> Frequencies { get; set; }
        public int? MMax { get; set; }
        public int? NMax { get; set; }
        public double? FMin { get; set; }
        public double? Sigma { get; set; }
        public double? SensitivitySemitonesPerSecond { get; set; }
        public double? OctaveOffset { get; set; }
        public double? ShepardCyclesFromEdgeToProbe { get; set; }
        public string Type { get; set; }
        public bool Debug { get; set; }
        public double? LMin { get; set; }
        public double? LMax { get; set; }
        public int? LatencyInMs { get; set; } // <--- NEU
        public double? SmoothingFactor { get; set; } // <--- NEU
        public int? UpdateIntervalSamples { get; set; } // <--- NEU
        public double? DoubleBeepFrequency1 { get; set; } // <--- NEU
        public double? DoubleBeepFrequency2 { get; set; } // <--- NEU
        public double? AusklinkDoubleBeepFrequency1 { get; set; } // <--- NEU
        public double? AusklinkDoubleBeepFrequency2 { get; set; } // <--- NEU
        public double? IdleBeepFrequency { get; set; } // <--- NEU

        [JsonConstructor]
        public ToneType(
            string id,
            string name,
            double? minFrequency = null,
            double? maxFrequency = null,
            string logBase = null,
            List<double> frequencies = null,
            int? mMax = null,
            int? nMax = null,
            double? fMin = null,
            double? sigma = null,
            double? sensitivitySemitonesPerSecond = null,
            double? octaveOffset = null,
            double? shepardCyclesFromEdgeToProbe = null,
            string type = null,
            bool debug = false,
            double? lMin = null,
            double? lMax = null,
            int? latencyInMs = null, // <--- NEU
            double? smoothingFactor = null, // <--- NEU
            int? updateIntervalSamples = null, // <--- NEU
            double? doubleBeepFrequency1 = null, // <--- NEU
            double? doubleBeepFrequency2 = null, // <--- NEU
            double? ausklinkDoubleBeepFrequency1 = null, // <--- NEU
            double? ausklinkDoubleBeepFrequency2 = null, // <--- NEU
            double? idleBeepFrequency = null // <--- NEU
        )
        {
            Id = id;
            Name = name;
            MinFrequency = minFrequency;
            MaxFrequency = maxFrequency;
            LogBase = logBase;
            Frequencies = frequencies;
            MMax = mMax;
            NMax = nMax;
            FMin = fMin;
            Sigma = sigma;
            SensitivitySemitonesPerSecond = sensitivitySemitonesPerSecond;
            OctaveOffset = octaveOffset;
            ShepardCyclesFromEdgeToProbe = shepardCyclesFromEdgeToProbe;
            Type = type ?? id;
            Debug = debug;
            LMin = lMin;
            LMax = lMax;
            LatencyInMs = latencyInMs; // <--- NEU
            SmoothingFactor = smoothingFactor; // <--- NEU
            UpdateIntervalSamples = updateIntervalSamples; // <--- NEU
            DoubleBeepFrequency1 = doubleBeepFrequency1; // <--- NEU
            DoubleBeepFrequency2 = doubleBeepFrequency2; // <--- NEU
            AusklinkDoubleBeepFrequency1 = ausklinkDoubleBeepFrequency1; // <--- NEU
            AusklinkDoubleBeepFrequency2 = ausklinkDoubleBeepFrequency2; // <--- NEU
            IdleBeepFrequency = idleBeepFrequency; // <--- NEU
        }
    }
}