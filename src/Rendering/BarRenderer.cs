using System.Text;
using static NavPathfinder.Demo.Rendering.ColorPalette;

namespace NavPathfinder.Demo.Rendering;

/// <summary>
/// Reusable bar and sparkline rendering helpers for the metrics HUD.
/// All methods append directly to a StringBuilder for zero-allocation rendering.
/// </summary>
public static class BarRenderer
{
    /// <summary>Renders a pressure bar (0–1 scale, 10 blocks, green→gold→crimson).</summary>
    public static void AppendPressureBar(StringBuilder sb, float pressure)
    {
        int filled = Math.Clamp((int)Math.Round(pressure * 10), 0, 10);
        string color = pressure < 0.4f ? Green : pressure < 0.7f ? Yellow : Red;
        sb.Append(color);
        sb.Append(new string('█', filled));
        sb.Append("\u001b[38;5;240m");
        sb.Append(new string('░', 10 - filled));
        sb.Append(Reset);
    }

    /// <summary>Renders a threat bar (0–1 scale, 10 blocks, cyan→gold→crimson).</summary>
    public static void AppendThreatBar(StringBuilder sb, float threat)
    {
        int filled = Math.Clamp((int)Math.Round(threat * 10), 0, 10);
        string color = threat < 0.3f ? Cyan : threat < 0.65f ? Yellow : Red;
        sb.Append(color);
        sb.Append(new string('█', filled));
        sb.Append("\u001b[38;5;240m");
        sb.Append(new string('░', 10 - filled));
        sb.Append(Reset);
    }

    /// <summary>Renders a morale bar (0–1 scale, 8 blocks, crimson→gold→emerald).</summary>
    public static void AppendMoraleBar(StringBuilder sb, float morale)
    {
        int filled = Math.Clamp((int)(morale * 8), 0, 8);
        string color = morale > 0.7f ? BoldGreen : morale > 0.4f ? Yellow : Red;
        sb.Append(color);
        sb.Append(new string('█', filled));
        sb.Append("\u001b[38;5;240m");
        sb.Append(new string('▒', 8 - filled));
        sb.Append(Reset);
    }

    /// <summary>Renders a resource pool bar (current/max, configurable width).</summary>
    public static void AppendResourceBar(StringBuilder sb, int current, int max, string color, int width)
    {
        int filled = max > 0 ? Math.Clamp((int)((float)current / max * width), 0, width) : 0;
        sb.Append(color);
        for (int i = 0; i < filled; i++) sb.Append('█');
        sb.Append("\u001b[38;5;240m");
        for (int i = filled; i < width; i++) sb.Append('░');
        sb.Append(Reset);
    }

    /// <summary>Renders a sparkline from a circular buffer of float values.</summary>
    public static void AppendSparkline(StringBuilder sb, float[] history, int currentIndex,
        float min, float max, int width)
    {
        float range = max - min;
        if (range < 0.001f) range = 1f;
        int histLen = history.Length;
        for (int i = 0; i < width; i++)
        {
            int hi = (currentIndex - width + i + histLen * 100) % histLen;
            float v = Math.Clamp((history[hi] - min) / range, 0f, 1f);
            int si = Math.Clamp((int)(v * (GlyphSets.Spark.Length - 1)), 0, GlyphSets.Spark.Length - 1);
            // Colour sparkline bars by value: green→gold→crimson
            string sparkColor = v > 0.65f ? Green : v > 0.3f ? Yellow : Red;
            sb.Append(sparkColor);
            sb.Append(GlyphSets.Spark[si]);
        }
        sb.Append(Reset);
    }

    /// <summary>Centre-pads text to a given width.</summary>
    public static string Pad(string text, int width)
    {
        if (text.Length >= width) return text[..width];
        int left  = (width - text.Length) / 2;
        int right = width - text.Length - left;
        return new string(' ', left) + text + new string(' ', right);
    }
}
