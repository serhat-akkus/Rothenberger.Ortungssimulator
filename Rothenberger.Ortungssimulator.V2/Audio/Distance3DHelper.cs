using System;
using Microsoft.Maui.Graphics;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public static class Distance3DHelper
    {
        // Berechnet die 3D-Distanz aus horizontaler 2D-Distanz (Meter) und Sondentiefe (Meter).
        public static double Compute3dDistanceToProbe(double horiz2dMeters, double probeDepthMeters)
        {
            double hx = Math.Max(0.0, horiz2dMeters);
            double dz = Math.Max(0.0, probeDepthMeters);
            return Math.Sqrt(hx * hx + dz * dz);
        }

        // Normalisiert die Distanz auf [0..1] mit near=Tiefe, far=3D-Radius.
        // Rückgabewert: t (0=nah an far, 1=nah an near) ist NICHT invertiert; Proximity=1-t kann optional außerhalb genutzt werden.
        public static double Normalize3dDistanceToProbe(double d3, double nearDepthMeters, double farRadiusMeters)
        {
            double near = 0;
            double far  = Math.Max(near + 1e-6, farRadiusMeters);
            double t    = (d3 - near) / (far - near);
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            return t;
        }

        public static bool IsOutsideProbeRadius3D(double probeRadius3DMeters, double distance3dMouseToProbe, double proximity3d)
        {
            return distance3dMouseToProbe > probeRadius3DMeters || proximity3d <= 0.0;
        }

        public static double CalculateProximity3D(double probeRadius3DMeters, double distance3dMouseToProbe)
        {
            double proximity3d = (probeRadius3DMeters - distance3dMouseToProbe) / Math.Max(1e-6, probeRadius3DMeters);
            if (proximity3d < 0.0) proximity3d = 0.0;
            if (proximity3d > 1.0) proximity3d = 1.0;
            return proximity3d;
        }

        // Berechnet die Distanz in Metern zwischen zwei Punkten in der 2D-Darstellung.
        public static double CalculateDistanceMeters(Point from, Point to, double meterPerPixel)
        {
            double dxPx = to.X - from.X;
            double dyPx = to.Y - from.Y;
            double distancePx = Math.Sqrt(dxPx * dxPx + dyPx * dyPx);
            return distancePx * meterPerPixel;
        }
    }
}