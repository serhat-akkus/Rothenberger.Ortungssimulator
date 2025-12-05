namespace Rothenberger.Ortungssimulator.V2.Graphics
{
    public sealed class CanvasHelper(
        int canvasSizePx = 1000,
        double meterPerPixel = 0.01,
        double probeRadiusMeters = 5.0)
    {
        public int CanvasSizePx { get; } = canvasSizePx;
        public double MeterPerPixel { get; } = meterPerPixel;

        // Kugelradius im 3D-Raum (z.B. 5 m)
        public double ProbeRadiusMeters { get; } = probeRadiusMeters;

        public Point ClampToCanvas(Point p)
        {
            var x = Math.Min(Math.Max(p.X, 0), CanvasSizePx);
            var y = Math.Min(Math.Max(p.Y, 0), CanvasSizePx);
            return new Point(x, y);
        }

        public Point GetRandomProbePosition(Random rnd)
        {
            return new Point(rnd.NextDouble() * CanvasSizePx, rnd.NextDouble() * CanvasSizePx);
        }
    }
}