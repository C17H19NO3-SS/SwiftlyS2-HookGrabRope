using System.Globalization;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HookGrabRope.Utils;

/// <summary>
/// CS2 CBeam varliklarini olusturma, konum guncelleme ve guvenli yok etme (Despawn) yardimcilari.
/// amx_hook.sma icindeki TE_BEAMENTPOINT gecici varliginin Source 2 CBeam karsiligidir.
/// </summary>
public static class BeamUtil
{
    public static CBeam? CreateOrUpdateBeam(
        ISwiftlyCore core,
        CBeam? existingBeam,
        Vector startPos,
        Vector endPos,
        Color color,
        float width)
    {
        try
        {
            if (existingBeam is { IsValid: true })
            {
                existingBeam.Teleport(startPos, null, null);
                existingBeam.EndPos = endPos;
                existingBeam.EndPosUpdated();
                return existingBeam;
            }

            var beam = core.EntitySystem.CreateEntityByDesignerName<CBeam>("beam");
            if (beam is not { IsValid: true }) return null;

            beam.RenderMode = RenderMode_t.kRenderNormal;
            beam.Render = color;
            beam.Width = Math.Clamp(width, 0.5f, 20.0f);
            beam.EndWidth = Math.Clamp(width, 0.5f, 20.0f);
            beam.Teleport(startPos, QAngle.Zero, Vector.Zero);
            beam.EndPos = endPos;
            beam.DispatchSpawn();

            beam.RenderModeUpdated();
            beam.RenderUpdated();
            beam.WidthUpdated();
            beam.EndWidthUpdated();
            beam.EndPosUpdated();

            return beam;
        }
        catch
        {
            return null;
        }
    }

    public static void DestroyBeam(ref CBeam? beam)
    {
        if (beam == null) return;
        try
        {
            if (beam.IsValid)
            {
                beam.Despawn();
            }
        }
        catch { }
        finally
        {
            beam = null;
        }
    }

    public static Color ParseHexColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var clean = hex.Trim().TrimStart('#');
        if (clean.Length == 6 &&
            byte.TryParse(clean.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
            byte.TryParse(clean.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
            byte.TryParse(clean.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return new Color(r, g, b, (byte)255);
        }
        return fallback;
    }
}
