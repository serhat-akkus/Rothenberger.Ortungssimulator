using Microsoft.Maui.Graphics;

namespace Rothenberger.Ortungssimulator.V2.Graphics
{
    public sealed class CanvasHelper
    {
        public int CanvasSizePx { get; }
        public double MeterPerPixel { get; }

        // Kugelradius im 3D-Raum (z.B. 5 m)
        public double ProbeRadiusMeters { get; }

        // Maximaltiefe sinnvollerweise <= Kugelradius
        public double MaxDepthMeters { get; }

        private double depthMeters;
        public double DepthMeters
        {
            get => depthMeters;
            private set => depthMeters = Math.Clamp(value, 0.0, MaxDepthMeters);
        }

        // Physikalisch korrekter Schnittkreisradius auf der Oberfläche: r(z) = sqrt(R^2 - z^2)
        public double EffectiveProbeRadiusMeters
        {
            get
            {
                var R = ProbeRadiusMeters;
                var z = DepthMeters;
                var sq = R * R - z * z;
                if (sq <= 0) return 0.0;
                return Math.Sqrt(sq);
            }
        }

        public double ProbeRadiusPixels => EffectiveProbeRadiusMeters / MeterPerPixel;

        public CanvasHelper(
            int canvasSizePx = 1000,
            double meterPerPixel = 0.01,
            double probeRadiusMeters = 5.0,
            double maxDepthMeters = 3.0) // zurück auf 3.0 m
        {
            CanvasSizePx = canvasSizePx;
            MeterPerPixel = meterPerPixel;
            ProbeRadiusMeters = probeRadiusMeters;
            MaxDepthMeters = Math.Max(0.0, maxDepthMeters);
            DepthMeters = 0.0;
        }

        public void SetDepth(double depthMeters) => DepthMeters = depthMeters;

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

        // Normalisierung 1.0 (Probe berührt) .. 0.0 (außerhalb des Schnittkreisradius)
        public double NormalizeDistance(double distanceMeters)
        {
            var effectiveRadius = EffectiveProbeRadiusMeters;
            if (effectiveRadius <= 0.0) return 0.0;
            if (distanceMeters > effectiveRadius) return 0.0;
            var normalized = 1.0 - (distanceMeters / effectiveRadius);
            return Math.Clamp(normalized, 0.0, 1.0);
        }
    }
}