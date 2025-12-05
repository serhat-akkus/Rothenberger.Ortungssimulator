using System;

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
            double near = Math.Max(0.0, nearDepthMeters);
            double far  = Math.Max(near + 1e-6, farRadiusMeters);
            double t    = (d3 - near) / (far - near);
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            return t;
        }
    }
}