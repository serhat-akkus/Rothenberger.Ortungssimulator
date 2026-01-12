namespace Rothenberger.Ortungssimulator.V2.Models
{
    public class TestRunResult
    {
        public string Name { get; set; } = string.Empty;
        public string ToneType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        private TimeSpan time;
        public TimeSpan Time
        {
            get => time;
            set {
                time = TimeSpan.FromSeconds(Math.Round(value.TotalSeconds, 2, MidpointRounding.AwayFromZero));
            }
        }

        public double Distance { get; set; }
        public bool ArrowEnabled { get; set; }
        public double ProbeDepthMeters { get; set; }
    }
}