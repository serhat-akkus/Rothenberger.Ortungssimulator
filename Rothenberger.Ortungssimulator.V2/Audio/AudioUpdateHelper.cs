using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public static class AudioUpdateHelper
    {
        public static double? HandleShepard(
            ShepardToneProvider shepardProvider,
            double distanceMeters,
            double normalized,
            double? previousDistanceMeters,
            double movementEpsilonMeters)
        {
            if (shepardProvider == null)
                return previousDistanceMeters;

            if (normalized > 0.0)
            {
                shepardProvider.SetGain((float)Math.Pow(normalized, 0.8));

                if (previousDistanceMeters.HasValue)
                {
                    double delta = previousDistanceMeters.Value - distanceMeters;
                    string direction =
                        Math.Abs(delta) < movementEpsilonMeters ? "none" :
                        (delta > 0 ? "up" : "down");

                    shepardProvider.SetDirection(direction);
                }

                return distanceMeters;
            }

            shepardProvider.SetGain(0f);
            shepardProvider.SetDirection("none");
            return null;
        }

        public static double? HandleSinusLinear(
            SineWaveProvider sineProvider,
            ToneType toneType,
            double normalized,
            double? lastFrequency)
        {
            if (sineProvider == null)
                return lastFrequency;

            if (normalized > 0.0)
            {
                sineProvider.SetVolume((float)Math.Pow(normalized, 0.8));

                var minFreqOpt = toneType?.MinFrequency;
                var maxFreqOpt = toneType?.MaxFrequency;

                if (minFreqOpt.HasValue && maxFreqOpt.HasValue && maxFreqOpt.Value > minFreqOpt.Value)
                {
                    double freq = minFreqOpt.Value + (maxFreqOpt.Value - minFreqOpt.Value) * normalized;
                    sineProvider.SetFrequency(freq);
                    return freq;
                }

                if (!lastFrequency.HasValue && minFreqOpt.HasValue)
                    lastFrequency = minFreqOpt.Value;

                sineProvider.SetFrequency(lastFrequency ?? 0.0);
                return lastFrequency;
            }

            sineProvider.SetVolume(0.0f);
            return lastFrequency;
        }

        public static double? HandleSinusLog(
            SineWaveProvider sineProvider,
            ToneType toneType,
            double normalized,
            double? lastFrequency)
        {
            if (sineProvider == null)
                return lastFrequency;

            if (normalized > 0.0)
            {
                sineProvider.SetVolume((float)Math.Pow(normalized, 0.8));

                var minFreqOpt = toneType?.MinFrequency;
                var maxFreqOpt = toneType?.MaxFrequency;

                if (minFreqOpt.HasValue && maxFreqOpt.HasValue && maxFreqOpt.Value > minFreqOpt.Value && minFreqOpt.Value > 0)
                {
                    double logBase;
                    var logBaseStr = toneType?.LogBase;
                    if (string.Equals(logBaseStr, "e", StringComparison.OrdinalIgnoreCase))
                        logBase = Math.E;
                    else if (!double.TryParse(logBaseStr, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out logBase) ||
                             logBase <= 0.0 || Math.Abs(logBase - 1.0) < double.Epsilon)
                        logBase = Math.E;

                    double k = Math.Log(maxFreqOpt.Value / minFreqOpt.Value) / Math.Log(logBase);
                    double freq = minFreqOpt.Value * Math.Pow(logBase, normalized * k);
                    sineProvider.SetFrequency(freq);
                    return freq;
                }

                if (!lastFrequency.HasValue && minFreqOpt.HasValue)
                    lastFrequency = minFreqOpt.Value;

                sineProvider.SetFrequency(lastFrequency ?? 0.0);
                return lastFrequency;
            }

            sineProvider.SetVolume(0.0f);
            return lastFrequency;
        }

        public static double? HandleTonleiterDiskret(
            SineWaveProvider sineProvider,
            ToneType toneType,
            double normalized,
            double distanceMeters,
            double? lastFrequency)
        {
            if (sineProvider == null)
                return lastFrequency;

            if (normalized > 0.0)
            {
                sineProvider.SetVolume((float)Math.Pow(normalized, 0.8));

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

            sineProvider.SetVolume(0.0f);
            return lastFrequency;
        }

        public static double? HandleDefault(SineWaveProvider? sineProvider, ShepardToneProvider? shepardProvider)
        {
            if (sineProvider != null)
            {
                sineProvider.SetVolume(0.0f);
            }
            if (shepardProvider != null)
            {
                shepardProvider.SetGain(0f);
                shepardProvider.SetDirection("none");
            }
            return null;
        }
    }
}