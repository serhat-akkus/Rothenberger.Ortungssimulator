using System;
using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public static class AudioUpdateHelper
    {
        private const double MaxAudibleDistanceMeters = 5.0;
        private const float ConstantVolume = 0.6f;

        public static double? HandleSinusLinear(
            SineWaveProvider sineProvider,
            ToneType toneType,
            double normalized,
            double distanceMeters,
            double? lastFrequency)
        {
            if (sineProvider == null) return lastFrequency;

            if (distanceMeters > MaxAudibleDistanceMeters || normalized <= 0.0)
            {
                sineProvider.SetVolume(0.0f);
                return lastFrequency;
            }

            sineProvider.SetVolume(ConstantVolume);

            var min = toneType?.MinFrequency;
            var max = toneType?.MaxFrequency;
            if (min.HasValue && max.HasValue && max.Value > min.Value)
            {
                double freq = min.Value + (max.Value - min.Value) * normalized;
                sineProvider.SetFrequency(freq);
                return freq;
            }

            if (!lastFrequency.HasValue && min.HasValue)
                lastFrequency = min.Value;

            sineProvider.SetFrequency(lastFrequency ?? 0.0);
            return lastFrequency;
        }

        public static double? HandleSinusLog(
            SineWaveProvider sineProvider,
            ToneType toneType,
            double normalized,
            double distanceMeters,
            double? lastFrequency)
        {
            if (sineProvider == null) return lastFrequency;

            if (distanceMeters > MaxAudibleDistanceMeters || normalized <= 0.0)
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
                double freq = min.Value * Math.Pow(logBase, normalized * k);
                sineProvider.SetFrequency(freq);
                return freq;
            }

            if (!lastFrequency.HasValue && min.HasValue)
                lastFrequency = min.Value;

            sineProvider.SetFrequency(lastFrequency ?? 0.0);
            return lastFrequency;
        }

        public static double? HandleTonleiterDiskret(
            SineWaveProvider sineProvider,
            ToneType toneType,
            double normalized,
            double distanceMeters,
            double? lastFrequency)
        {
            if (sineProvider == null) return lastFrequency;

            if (distanceMeters > MaxAudibleDistanceMeters || normalized <= 0.0)
            {
                sineProvider.SetVolume(0.0f);
                return lastFrequency;
            }

            sineProvider.SetVolume(ConstantVolume);

            var list = toneType?.Frequencies;
            if (list != null && list.Count > 0)
            {
                double stepMeters = 0.2;
                int index = (int)Math.Floor(distanceMeters / stepMeters);
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
    }
}