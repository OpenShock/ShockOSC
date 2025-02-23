using System.Globalization;

namespace OpenShock.ShockOSC.Utils;

public static class MathUtils
{
    public static float LerpFloat(float min, float max, float t) => min + (max - min) * t;
    public static float Saturate(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    public static uint LerpUShort(ushort min, ushort max, float t) => (ushort)(min + (max - min) * t);
    public static uint ClampUint(uint value, uint min, uint max) => value < min ? min : value > max ? max : value;
    public static ushort ClampUShort(ushort value, ushort min, ushort max) => value < min ? min : value > max ? max : value;
    public static byte ClampByte(byte value, byte min, byte max) => value < min ? min : value > max ? max : value;

    public static float DurationInSeconds(this uint duration) => MathF.Round(duration / 1000f, 1);
    public static string DurationInSecondsString(this uint duration) => DurationInSeconds(duration).ToString(CultureInfo.InvariantCulture);
}