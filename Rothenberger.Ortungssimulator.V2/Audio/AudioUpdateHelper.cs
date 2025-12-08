using Rothenberger.Ortungssimulator.V2.Models;

namespace Rothenberger.Ortungssimulator.V2.Audio
{
    public static class AudioUpdateHelper
    {
        public static double? HandleSinusLinear3D(
            SineWaveAudioManager audioManager,
            ToneType toneType,
            double distance3dMouseToProbe,
            double probeRadius3DMeters,
            double? lastFrequency)
        {
            if (audioManager == null) return lastFrequency;

            double proximity3d = Distance3DHelper.CalculateProximity3D(probeRadius3DMeters, distance3dMouseToProbe);

            // Hörbarkeit gegen tatsächlichen 3D-Radius prüfen
            if (Distance3DHelper.IsOutsideProbeRadius3D(probeRadius3DMeters, distance3dMouseToProbe, proximity3d))
            {
                audioManager.Pause();
                return 0;
            }

            audioManager.Play();

            var min = toneType?.MinFrequency;
            var max = toneType?.MaxFrequency;
            if (min.HasValue && max.HasValue && max.Value > min.Value)
            {
                double freq = min.Value + (max.Value - min.Value) * proximity3d;
                audioManager.SetFrequency(freq);
                return freq;
            }

            if (!lastFrequency.HasValue && min.HasValue)
                lastFrequency = min.Value;

            audioManager.SetFrequency(lastFrequency ?? 0.0);
            return lastFrequency;
        }

        public static double? HandleSinusLog3D(
            SineWaveAudioManager audioManager,
            ToneType toneType,
            double distance3dMouseToProbe,
            double probeRadius3DMeters,
            double? lastFrequency)
        {
            if (audioManager == null) return lastFrequency;

            double proximity3d = Distance3DHelper.CalculateProximity3D(probeRadius3DMeters, distance3dMouseToProbe);

            if (Distance3DHelper.IsOutsideProbeRadius3D(probeRadius3DMeters, distance3dMouseToProbe, proximity3d))
            {
                audioManager.Pause();
                return lastFrequency;
            }

            audioManager.Play();

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
                double freq = min.Value * Math.Pow(logBase, proximity3d * k);
                audioManager.SetFrequency(freq);
                return freq;
            }

            if (!lastFrequency.HasValue && min.HasValue)
                lastFrequency = min.Value;

            audioManager.SetFrequency(lastFrequency ?? 0.0);
            return lastFrequency;
        }

        public static double? HandleTonleiterDiskret3D(
            SineWaveAudioManager audioManager,
            ToneType toneType,
            double distance3dMouseToProbe,
            double probeRadius3DMeters,
            double? lastFrequency)
        {
            if (audioManager == null) return lastFrequency;

            double proximity3d = Distance3DHelper.CalculateProximity3D(probeRadius3DMeters, distance3dMouseToProbe);

            if (Distance3DHelper.IsOutsideProbeRadius3D(probeRadius3DMeters, distance3dMouseToProbe, proximity3d))
            {
                audioManager.Pause();
                return lastFrequency;
            }

            audioManager.Play();

            var list = toneType?.Frequencies;
            if (list != null && list.Count > 0)
            {
                // Diskrete Schritte entlang der 3D-Distanz
                double stepMeters = 0.2;
                int index = (int)Math.Floor(distance3dMouseToProbe / stepMeters);
                if (index < 0) index = 0;
                if (index >= list.Count) index = list.Count - 1;

                double freq = list[index];
                audioManager.SetFrequency(freq);
                return freq;
            }

            audioManager.SetFrequency(lastFrequency ?? 0.0);
            return lastFrequency;
        }

        public static double? HandleDefault(SineWaveProvider? sineProvider)
        {
            sineProvider?.SetVolume(0.0f);
            return null;
        }

        public static double HandleShepardMouse3D(ShepardMouseAudioManager? shepardMouseAudioManager, double normalizedDistance3d)
        {
            if (shepardMouseAudioManager == null)
                return 0.0;
            shepardMouseAudioManager.UpdateDistance3DState(normalizedDistance3d);
            return shepardMouseAudioManager.Provider?.GetCurrentBaseFrequency() ?? 0.0;
        }
    }
}