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
        public int? LatencyInMs { get; set; }
        public double? SmoothingFactor { get; set; }
        public int? UpdateIntervalSamples { get; set; }
        public double? EinklinkDoubleBeepFrequency1 { get; set; }
        public double? EinklinkDoubleBeepFrequency2 { get; set; }
        public double? AusklinkDoubleBeepFrequency1 { get; set; }
        public double? AusklinkDoubleBeepFrequency2 { get; set; }
        public double? IdleBeepFrequency { get; set; }
        public int? DoubleBeepDurationMs { get; set; }
        public int? IdleBeepIntervalMs { get; set; }

        // NEU: Sondentiefe-Min/Max
        public double? ProbeDepthMin { get; set; }
        public double? ProbeDepthMax { get; set; }

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
            int? latencyInMs = null,
            double? smoothingFactor = null,
            int? updateIntervalSamples = null,
            double? einklinkDoubleBeepFrequency1 = null,
            double? einklinkDoubleBeepFrequency2 = null,
            double? ausklinkDoubleBeepFrequency1 = null,
            double? ausklinkDoubleBeepFrequency2 = null,
            double? idleBeepFrequency = null,
            int? doubleBeepDurationMs = null,
            int? idleBeepIntervalMs = null,
            double? probeDepthMin = null, // <--- NEU
            double? probeDepthMax = null  // <--- NEU
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
            LatencyInMs = latencyInMs;
            SmoothingFactor = smoothingFactor == null || smoothingFactor == 0 ? 1 : smoothingFactor;
            UpdateIntervalSamples = updateIntervalSamples;
            EinklinkDoubleBeepFrequency1 = einklinkDoubleBeepFrequency1;
            EinklinkDoubleBeepFrequency2 = einklinkDoubleBeepFrequency2;
            AusklinkDoubleBeepFrequency1 = ausklinkDoubleBeepFrequency1;
            AusklinkDoubleBeepFrequency2 = ausklinkDoubleBeepFrequency2;
            IdleBeepFrequency = idleBeepFrequency;
            DoubleBeepDurationMs = doubleBeepDurationMs;
            IdleBeepIntervalMs = idleBeepIntervalMs;
            ProbeDepthMin = probeDepthMin;
            ProbeDepthMax = probeDepthMax;
        }
    }
}