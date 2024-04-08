namespace OpenShock.ShockOsc.Utils;

public static class MathUtils
{
    public static float LerpFloat(float min, float max, float t) => min + (max - min) * t;
    public static float ClampFloat(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    public static uint LerpUint(uint min, uint max, float t) => (uint)(min + (max - min) * t);
    public static uint ClampUint(uint value, uint min, uint max) => value < min ? min : value > max ? max : value;
}