namespace Rothenberger.Ortungssimulator.V2.Models
{
    public class TestRunResult
    {
        public string Name { get; set; } = string.Empty;
        public string ToneType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public TimeSpan Time { get; set; }
        public double Distance { get; set; }
        public bool ArrowEnabled { get; set; }
    }
}