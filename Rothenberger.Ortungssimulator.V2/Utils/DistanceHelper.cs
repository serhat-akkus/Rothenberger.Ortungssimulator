using System;

namespace Rothenberger.Ortungssimulator.V2.Utils
{
    // Kapselt Distanz- und Normalisierungsberechnungen für 2D/3D und Tiefeinfluss
    public static class DistanceHelper
    {
        // 2D-Abstand in Metern
        public static double Distance2D(double xMeters, double yMeters)
        {
            return Math.Sqrt(xMeters * xMeters + yMeters * yMeters);
        }

        // 3D-Abstand in Metern
        public static double Distance3D(double xMeters, double yMeters, double zMeters)
        {
            return Math.Sqrt(xMeters * xMeters + yMeters * yMeters + zMeters * zMeters);
        }

        // Effektiver 2D-Radius bei gegebener Tiefe: r2d = sqrt(R^2 - z^2), geclamped auf [0, R]
        public static double Effective2DRadius(double sphereRadiusMeters, double depthMeters)
        {
            double R = Math.Max(0.0, sphereRadiusMeters);
            double z = Math.Max(0.0, depthMeters);
            double r2dSquared = R * R - z * z;
            if (r2dSquared <= 0.0) return 0.0;
            return Math.Sqrt(r2dSquared);
        }

        // Normalisiert eine 2D-Distanz auf den effektiven 2D-Radius (Tiefe berücksichtigt), Ergebnis in [0,1]
        // Gibt zusätzlich die Nähe (1 - normalized) zurück, da diese häufig direkt verwendet wird.
        public static (double normalized, double proximity) Normalize2DWithDepth(double distanceMeters, double sphereRadiusMeters, double depthMeters)
        {
            double r2d = Effective2DRadius(sphereRadiusMeters, depthMeters);
            if (r2d <= 0.0) return (1.0, 0.0); // Kein horizontaler Spielraum
            double normalized = Math.Clamp(distanceMeters / r2d, 0.0, 1.0);
            double proximity = 1.0 - normalized;
            return (normalized, proximity);
        }
    }
}