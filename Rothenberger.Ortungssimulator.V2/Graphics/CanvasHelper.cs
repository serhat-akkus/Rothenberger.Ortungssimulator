using Microsoft.Maui.Graphics;

namespace Rothenberger.Ortungssimulator.V2.Graphics
{
    public sealed class CanvasHelper
    {
        public int CanvasSizePx { get; }
        public double MeterPerPixel { get; }
        public double ProbeRadiusMeters { get; }
        public double ProbeRadiusPixels => ProbeRadiusMeters / MeterPerPixel;

        public CanvasHelper(int canvasSizePx = 1000, double meterPerPixel = 0.01, double probeRadiusMeters = 5.0)
        {
            CanvasSizePx = canvasSizePx;
            MeterPerPixel = meterPerPixel;
            ProbeRadiusMeters = probeRadiusMeters;
        }

        public Point ClampToCanvas(Point p)
        {
            var x = Math.Min(Math.Max(p.X, 0), CanvasSizePx);
            var y = Math.Min(Math.Max(p.Y, 0), CanvasSizePx);
            return new Point(x, y);
        }

        public double CalculateDistanceMeters(Point from, Point to)
        {
            double dxPx = to.X - from.X;
            double dyPx = to.Y - from.Y;
            double distancePx = Math.Sqrt(dxPx * dxPx + dyPx * dyPx);
            return distancePx * MeterPerPixel;
        }

        public Point GetRandomProbePosition(Random rnd)
        {
            return new Point(rnd.NextDouble() * CanvasSizePx, rnd.NextDouble() * CanvasSizePx);
        }

        public double NormalizeDistance(double distanceMeters)
        {
            if (distanceMeters > ProbeRadiusMeters) return 0.0;
            var normalized = 1.0 - (distanceMeters / ProbeRadiusMeters);
            return Math.Clamp(normalized, 0.0, 1.0);
        }
    }
}