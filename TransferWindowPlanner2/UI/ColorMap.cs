using UnityEngine;

namespace TransferWindowPlanner2.UI
{
public static class ColorMap
{
    public static Color MapColor(float value, float min, float max)
    {
        if (!float.IsFinite(value)) { return ErrorColor; }

        var t = Mathf.RoundToInt(Mathf.InverseLerp(min, max, value) * 255);
        if (t < 0) { t = 0; }
        if (t > 255) { t = 255; }
        return ColorValues[t];
    }

    public static Color MapColorReverse(float value, float min, float max) => MapColor(-value, -max, -min);

    private static readonly Color[] ColorValues =
    {
        // This is the CET-L20 color map by Peter Kovesi.
        // It is available at colorcet.com under a CC4.0-BY license.
        new Color(0.189f, 0.189f, 0.189f),
        new Color(0.193f, 0.191f, 0.199f),
        new Color(0.197f, 0.192f, 0.209f),
        new Color(0.200f, 0.193f, 0.220f),
        new Color(0.204f, 0.195f, 0.230f),
        new Color(0.207f, 0.196f, 0.240f),
        new Color(0.210f, 0.197f, 0.249f),
        new Color(0.213f, 0.199f, 0.259f),
        new Color(0.217f, 0.200f, 0.269f),
        new Color(0.219f, 0.201f, 0.279f),
        new Color(0.222f, 0.203f, 0.289f),
        new Color(0.225f, 0.204f, 0.299f),
        new Color(0.227f, 0.206f, 0.309f),
        new Color(0.230f, 0.207f, 0.319f),
        new Color(0.232f, 0.209f, 0.329f),
        new Color(0.235f, 0.210f, 0.339f),
        new Color(0.237f, 0.211f, 0.348f),
        new Color(0.239f, 0.213f, 0.358f),
        new Color(0.241f, 0.214f, 0.368f),
        new Color(0.243f, 0.216f, 0.377f),
        new Color(0.245f, 0.217f, 0.387f),
        new Color(0.246f, 0.219f, 0.397f),
        new Color(0.248f, 0.221f, 0.406f),
        new Color(0.250f, 0.222f, 0.416f),
        new Color(0.251f, 0.224f, 0.425f),
        new Color(0.252f, 0.225f, 0.435f),
        new Color(0.254f, 0.227f, 0.444f),
        new Color(0.255f, 0.229f, 0.454f),
        new Color(0.256f, 0.230f, 0.463f),
        new Color(0.257f, 0.232f, 0.472f),
        new Color(0.258f, 0.234f, 0.481f),
        new Color(0.259f, 0.236f, 0.491f),
        new Color(0.260f, 0.238f, 0.500f),
        new Color(0.260f, 0.239f, 0.509f),
        new Color(0.261f, 0.241f, 0.518f),
        new Color(0.261f, 0.243f, 0.527f),
        new Color(0.262f, 0.245f, 0.535f),
        new Color(0.262f, 0.247f, 0.544f),
        new Color(0.262f, 0.249f, 0.553f),
        new Color(0.263f, 0.251f, 0.561f),
        new Color(0.263f, 0.253f, 0.570f),
        new Color(0.263f, 0.255f, 0.578f),
        new Color(0.263f, 0.258f, 0.587f),
        new Color(0.263f, 0.260f, 0.595f),
        new Color(0.262f, 0.262f, 0.603f),
        new Color(0.262f, 0.264f, 0.611f),
        new Color(0.262f, 0.267f, 0.619f),
        new Color(0.262f, 0.269f, 0.627f),
        new Color(0.261f, 0.272f, 0.635f),
        new Color(0.261f, 0.274f, 0.642f),
        new Color(0.260f, 0.277f, 0.650f),
        new Color(0.259f, 0.279f, 0.657f),
        new Color(0.259f, 0.282f, 0.664f),
        new Color(0.258f, 0.284f, 0.671f),
        new Color(0.257f, 0.287f, 0.678f),
        new Color(0.256f, 0.290f, 0.685f),
        new Color(0.255f, 0.293f, 0.692f),
        new Color(0.254f, 0.296f, 0.698f),
        new Color(0.253f, 0.298f, 0.704f),
        new Color(0.252f, 0.301f, 0.711f),
        new Color(0.251f, 0.304f, 0.717f),
        new Color(0.250f, 0.308f, 0.722f),
        new Color(0.248f, 0.311f, 0.728f),
        new Color(0.247f, 0.314f, 0.733f),
        new Color(0.246f, 0.317f, 0.739f),
        new Color(0.244f, 0.321f, 0.744f),
        new Color(0.243f, 0.324f, 0.749f),
        new Color(0.241f, 0.327f, 0.753f),
        new Color(0.240f, 0.331f, 0.757f),
        new Color(0.238f, 0.335f, 0.762f),
        new Color(0.236f, 0.338f, 0.765f),
        new Color(0.235f, 0.342f, 0.769f),
        new Color(0.233f, 0.346f, 0.772f),
        new Color(0.231f, 0.350f, 0.775f),
        new Color(0.229f, 0.354f, 0.778f),
        new Color(0.228f, 0.358f, 0.780f),
        new Color(0.226f, 0.362f, 0.782f),
        new Color(0.224f, 0.366f, 0.784f),
        new Color(0.222f, 0.370f, 0.785f),
        new Color(0.220f, 0.375f, 0.786f),
        new Color(0.218f, 0.379f, 0.787f),
        new Color(0.215f, 0.384f, 0.787f),
        new Color(0.213f, 0.388f, 0.787f),
        new Color(0.211f, 0.393f, 0.786f),
        new Color(0.208f, 0.398f, 0.785f),
        new Color(0.206f, 0.403f, 0.783f),
        new Color(0.203f, 0.408f, 0.780f),
        new Color(0.200f, 0.413f, 0.777f),
        new Color(0.197f, 0.419f, 0.774f),
        new Color(0.193f, 0.424f, 0.769f),
        new Color(0.190f, 0.430f, 0.764f),
        new Color(0.186f, 0.435f, 0.758f),
        new Color(0.181f, 0.441f, 0.751f),
        new Color(0.176f, 0.447f, 0.743f),
        new Color(0.170f, 0.453f, 0.735f),
        new Color(0.165f, 0.459f, 0.726f),
        new Color(0.160f, 0.465f, 0.717f),
        new Color(0.155f, 0.471f, 0.709f),
        new Color(0.150f, 0.477f, 0.700f),
        new Color(0.146f, 0.482f, 0.692f),
        new Color(0.143f, 0.487f, 0.683f),
        new Color(0.140f, 0.493f, 0.675f),
        new Color(0.137f, 0.498f, 0.666f),
        new Color(0.135f, 0.503f, 0.658f),
        new Color(0.134f, 0.508f, 0.650f),
        new Color(0.133f, 0.513f, 0.641f),
        new Color(0.133f, 0.518f, 0.633f),
        new Color(0.134f, 0.522f, 0.625f),
        new Color(0.135f, 0.527f, 0.617f),
        new Color(0.137f, 0.531f, 0.609f),
        new Color(0.139f, 0.536f, 0.600f),
        new Color(0.142f, 0.540f, 0.592f),
        new Color(0.146f, 0.545f, 0.584f),
        new Color(0.150f, 0.549f, 0.577f),
        new Color(0.155f, 0.553f, 0.569f),
        new Color(0.160f, 0.557f, 0.561f),
        new Color(0.166f, 0.561f, 0.553f),
        new Color(0.172f, 0.565f, 0.545f),
        new Color(0.178f, 0.569f, 0.537f),
        new Color(0.185f, 0.573f, 0.530f),
        new Color(0.192f, 0.577f, 0.522f),
        new Color(0.199f, 0.580f, 0.514f),
        new Color(0.207f, 0.584f, 0.507f),
        new Color(0.215f, 0.587f, 0.499f),
        new Color(0.223f, 0.591f, 0.491f),
        new Color(0.231f, 0.594f, 0.484f),
        new Color(0.240f, 0.598f, 0.476f),
        new Color(0.248f, 0.601f, 0.469f),
        new Color(0.257f, 0.604f, 0.461f),
        new Color(0.267f, 0.608f, 0.454f),
        new Color(0.276f, 0.611f, 0.446f),
        new Color(0.285f, 0.614f, 0.439f),
        new Color(0.295f, 0.617f, 0.432f),
        new Color(0.305f, 0.620f, 0.424f),
        new Color(0.315f, 0.623f, 0.417f),
        new Color(0.325f, 0.625f, 0.409f),
        new Color(0.335f, 0.628f, 0.402f),
        new Color(0.345f, 0.631f, 0.395f),
        new Color(0.356f, 0.633f, 0.387f),
        new Color(0.367f, 0.636f, 0.380f),
        new Color(0.377f, 0.638f, 0.372f),
        new Color(0.388f, 0.641f, 0.365f),
        new Color(0.399f, 0.643f, 0.358f),
        new Color(0.411f, 0.645f, 0.350f),
        new Color(0.422f, 0.648f, 0.343f),
        new Color(0.433f, 0.650f, 0.335f),
        new Color(0.445f, 0.652f, 0.328f),
        new Color(0.456f, 0.654f, 0.321f),
        new Color(0.467f, 0.656f, 0.313f),
        new Color(0.477f, 0.658f, 0.306f),
        new Color(0.488f, 0.660f, 0.299f),
        new Color(0.498f, 0.662f, 0.291f),
        new Color(0.509f, 0.664f, 0.284f),
        new Color(0.519f, 0.666f, 0.277f),
        new Color(0.529f, 0.668f, 0.269f),
        new Color(0.539f, 0.670f, 0.262f),
        new Color(0.549f, 0.672f, 0.255f),
        new Color(0.559f, 0.673f, 0.248f),
        new Color(0.569f, 0.675f, 0.240f),
        new Color(0.578f, 0.677f, 0.233f),
        new Color(0.588f, 0.679f, 0.226f),
        new Color(0.598f, 0.681f, 0.218f),
        new Color(0.608f, 0.682f, 0.211f),
        new Color(0.617f, 0.684f, 0.204f),
        new Color(0.627f, 0.686f, 0.197f),
        new Color(0.637f, 0.687f, 0.190f),
        new Color(0.646f, 0.689f, 0.182f),
        new Color(0.656f, 0.690f, 0.175f),
        new Color(0.666f, 0.692f, 0.168f),
        new Color(0.675f, 0.693f, 0.161f),
        new Color(0.685f, 0.695f, 0.154f),
        new Color(0.695f, 0.696f, 0.146f),
        new Color(0.704f, 0.698f, 0.139f),
        new Color(0.714f, 0.699f, 0.132f),
        new Color(0.724f, 0.700f, 0.126f),
        new Color(0.734f, 0.702f, 0.119f),
        new Color(0.744f, 0.703f, 0.112f),
        new Color(0.754f, 0.704f, 0.106f),
        new Color(0.764f, 0.705f, 0.100f),
        new Color(0.774f, 0.706f, 0.094f),
        new Color(0.784f, 0.707f, 0.088f),
        new Color(0.795f, 0.708f, 0.083f),
        new Color(0.805f, 0.708f, 0.079f),
        new Color(0.816f, 0.709f, 0.076f),
        new Color(0.826f, 0.710f, 0.073f),
        new Color(0.837f, 0.710f, 0.071f),
        new Color(0.847f, 0.711f, 0.071f),
        new Color(0.858f, 0.711f, 0.071f),
        new Color(0.869f, 0.712f, 0.072f),
        new Color(0.878f, 0.713f, 0.073f),
        new Color(0.887f, 0.714f, 0.074f),
        new Color(0.895f, 0.715f, 0.075f),
        new Color(0.902f, 0.717f, 0.075f),
        new Color(0.909f, 0.719f, 0.076f),
        new Color(0.916f, 0.721f, 0.077f),
        new Color(0.922f, 0.723f, 0.077f),
        new Color(0.927f, 0.725f, 0.077f),
        new Color(0.932f, 0.728f, 0.078f),
        new Color(0.937f, 0.730f, 0.078f),
        new Color(0.942f, 0.733f, 0.078f),
        new Color(0.946f, 0.736f, 0.078f),
        new Color(0.950f, 0.739f, 0.079f),
        new Color(0.954f, 0.742f, 0.079f),
        new Color(0.958f, 0.745f, 0.079f),
        new Color(0.961f, 0.748f, 0.079f),
        new Color(0.965f, 0.752f, 0.079f),
        new Color(0.968f, 0.755f, 0.079f),
        new Color(0.970f, 0.759f, 0.079f),
        new Color(0.973f, 0.762f, 0.079f),
        new Color(0.976f, 0.766f, 0.078f),
        new Color(0.978f, 0.770f, 0.078f),
        new Color(0.980f, 0.774f, 0.078f),
        new Color(0.982f, 0.777f, 0.078f),
        new Color(0.984f, 0.781f, 0.078f),
        new Color(0.986f, 0.785f, 0.077f),
        new Color(0.987f, 0.789f, 0.077f),
        new Color(0.989f, 0.793f, 0.077f),
        new Color(0.990f, 0.797f, 0.076f),
        new Color(0.992f, 0.802f, 0.076f),
        new Color(0.993f, 0.806f, 0.075f),
        new Color(0.994f, 0.810f, 0.075f),
        new Color(0.995f, 0.814f, 0.074f),
        new Color(0.996f, 0.819f, 0.074f),
        new Color(0.996f, 0.823f, 0.073f),
        new Color(0.997f, 0.827f, 0.073f),
        new Color(0.998f, 0.832f, 0.072f),
        new Color(0.998f, 0.836f, 0.072f),
        new Color(0.998f, 0.841f, 0.071f),
        new Color(0.999f, 0.845f, 0.070f),
        new Color(0.999f, 0.850f, 0.070f),
        new Color(0.999f, 0.854f, 0.069f),
        new Color(0.999f, 0.859f, 0.068f),
        new Color(0.999f, 0.864f, 0.067f),
        new Color(0.999f, 0.868f, 0.066f),
        new Color(0.998f, 0.873f, 0.066f),
        new Color(0.998f, 0.878f, 0.065f),
        new Color(0.998f, 0.882f, 0.064f),
        new Color(0.997f, 0.887f, 0.063f),
        new Color(0.997f, 0.892f, 0.062f),
        new Color(0.996f, 0.897f, 0.061f),
        new Color(0.995f, 0.901f, 0.060f),
        new Color(0.994f, 0.906f, 0.059f),
        new Color(0.993f, 0.911f, 0.057f),
        new Color(0.993f, 0.916f, 0.056f),
        new Color(0.991f, 0.921f, 0.055f),
        new Color(0.990f, 0.926f, 0.054f),
        new Color(0.989f, 0.930f, 0.053f),
        new Color(0.988f, 0.935f, 0.051f),
        new Color(0.987f, 0.940f, 0.050f),
        new Color(0.985f, 0.945f, 0.049f),
        new Color(0.984f, 0.950f, 0.047f),
        new Color(0.982f, 0.955f, 0.045f),
        new Color(0.981f, 0.960f, 0.044f),
        new Color(0.979f, 0.965f, 0.042f),
        new Color(0.977f, 0.970f, 0.041f),
        new Color(0.975f, 0.975f, 0.039f),
    };

    private static readonly Color ErrorColor = ColorValues[0];
}
}
