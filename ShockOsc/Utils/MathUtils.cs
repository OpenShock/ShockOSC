using System.Globalization;

namespace OpenShock.ShockOsc.Utils;

public static class MathUtils
{
    public static float LerpFloat(float min, float max, float t) => min + (max - min) * t;
    public static float Saturate(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    public static uint LerpUint(uint min, uint max, float t) => (uint)(min + (max - min) * t);
    public static uint ClampUint(uint value, uint min, uint max) => value < min ? min : value > max ? max : value;

    public static float DurationInSeconds(this uint duration) => MathF.Round(duration / 1000f, 1);
    public static string DurationInSecondsString(this uint duration) => DurationInSeconds(duration).ToString(CultureInfo.InvariantCulture);
}