using System;
using NAudio.Wave;
using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public static class AudioUpdateHelper
    {
        private const float ConstantVolume = 0.6f;

        // NEU: 3D-Varianten – bevorzugt verwenden
        public static double? HandleSinusLinear3D(
            SineWaveProvider sineProvider,
            ToneType toneType,
            double horiz2dMeters,
            double probeDepthMeters,
            double probeRadius3DMeters,
            double? lastFrequency)
        {
            if (sineProvider == null) return lastFrequency;

            double d3 = Distance3DHelper.Compute3D(horiz2dMeters, probeDepthMeters);

            // Nähe über (Radius - Distanz) / Radius, nicht über (Tiefe..Radius)-Intervall
            double proximity = (probeRadius3DMeters - d3) / Math.Max(1e-6, probeRadius3DMeters);
            if (proximity < 0.0) proximity = 0.0;
            if (proximity > 1.0) proximity = 1.0;

            // Hörbarkeit gegen tatsächlichen 3D-Radius prüfen
            if (d3 > probeRadius3DMeters || proximity <= 0.0)
            {
                sineProvider.SetVolume(0.0f);
                return lastFrequency;
            }

            sineProvider.SetVolume(ConstantVolume);

            var min = toneType?.MinFrequency;
            var max = toneType?.MaxFrequency;
            if (min.HasValue && max.HasValue && max.Value > min.Value)
            {
                double freq = min.Value + (max.Value - min.Value) * proximity;
                sineProvider.SetFrequency(freq);
                return freq;
            }

            if (!lastFrequency.HasValue && min.HasValue)
                lastFrequency = min.Value;

            sineProvider.SetFrequency(lastFrequency ?? 0.0);
            return lastFrequency;
        }

        public static double? HandleSinusLog3D(
            SineWaveProvider sineProvider,
            ToneType toneType,
            double horiz2dMeters,
            double probeDepthMeters,
            double probeRadius3DMeters,
            double? lastFrequency)
        {
            if (sineProvider == null) return lastFrequency;

            double d3 = Distance3DHelper.Compute3D(horiz2dMeters, probeDepthMeters);

            double proximity = (probeRadius3DMeters - d3) / Math.Max(1e-6, probeRadius3DMeters);
            if (proximity < 0.0) proximity = 0.0;
            if (proximity > 1.0) proximity = 1.0;

            if (d3 > probeRadius3DMeters || proximity <= 0.0)
            {
                sineProvider.SetVolume(0.0f);
                return lastFrequency;
            }

            sineProvider.SetVolume(ConstantVolume);

            var min = toneType?.MinFrequency;
            var max = toneType?.MaxFrequency;
            if (min.HasValue && max.HasValue && max.Value > min.Value && min.Value > 0)
            {
                double logBase;
                if (string.Equals(toneType?.LogBase, "e", StringComparison.OrdinalIgnoreCase))
                    logBase = Math.E;
                else if (!double.TryParse(toneType?.LogBase,
                         System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture,
                         out logBase) || logBase <= 0.0 || Math.Abs(logBase - 1.0) < double.Epsilon)
                    logBase = Math.E;

                double k = Math.Log(max.Value / min.Value) / Math.Log(logBase);
                double freq = min.Value * Math.Pow(logBase, proximity * k);
                sineProvider.SetFrequency(freq);
                return freq;
            }

            if (!lastFrequency.HasValue && min.HasValue)
                lastFrequency = min.Value;

            sineProvider.SetFrequency(lastFrequency ?? 0.0);
            return lastFrequency;
        }

        public static double? HandleTonleiterDiskret3D(
            SineWaveProvider sineProvider,
            ToneType toneType,
            double horiz2dMeters,
            double probeDepthMeters,
            double probeRadius3DMeters,
            double? lastFrequency)
        {
            if (sineProvider == null) return lastFrequency;

            double d3 = Distance3DHelper.Compute3D(horiz2dMeters, probeDepthMeters);

            double proximity = (probeRadius3DMeters - d3) / Math.Max(1e-6, probeRadius3DMeters);
            if (proximity < 0.0) proximity = 0.0;
            if (proximity > 1.0) proximity = 1.0;

            if (d3 > probeRadius3DMeters || proximity <= 0.0)
            {
                sineProvider.SetVolume(0.0f);
                return lastFrequency;
            }

            sineProvider.SetVolume(ConstantVolume);

            var list = toneType?.Frequencies;
            if (list != null && list.Count > 0)
            {
                // Diskrete Schritte entlang der 3D-Distanz
                double stepMeters = 0.2;
                int index = (int)Math.Floor(d3 / stepMeters);
                if (index < 0) index = 0;
                if (index >= list.Count) index = list.Count - 1;

                double freq = list[index];
                sineProvider.SetFrequency(freq);
                return freq;
            }

            sineProvider.SetFrequency(lastFrequency ?? 0.0);
            return lastFrequency;
        }

        public static double? HandleDefault(SineWaveProvider? sineProvider)
        {
            if (sineProvider != null)
                sineProvider.SetVolume(0.0f);
            return null;
        }

        /// <summary>
        /// Aktualisiert nur den Zustand des bereits initialisierten ShepardToneMouseProvider.
        /// Keine Initialisierung/Start von Audio-Objekten.
        /// </summary>
        public static double UpdateShepardMouse3D(
            ShepardToneMouseProvider provider,
            double horiz2dMeters,
            double probeDepthMeters,
            double probeRadius3DMeters)
        {
            if (provider == null) return 0.0;
            provider.UpdateDistance3DState(horiz2dMeters, probeDepthMeters, probeRadius3DMeters);
            // Rückgabe: hier fMin nicht bekannt; 0.0 genügt, UI kommt über Progress-Callback.
            return 0.0;
        }
    }
}