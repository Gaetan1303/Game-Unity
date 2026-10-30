using UnityEngine;

/// <summary>
/// Réglages « pixel-perfect » partagés : 1 pixel d'art = 1/PPU unité monde, jamais d'échelle non entière à l'écran.
/// Valeurs pilotées par CameraFollow. PPU doit correspondre à celui de Editor/SpritePack.json (dossiers Hero/Enemies/Allies/Env).
/// </summary>
public static class PixelView
{
    public static float PPU = 24f;
    public static bool Enabled = true;

    public static bool Active
    {
        get { var c = Camera.main; return Enabled && c != null && c.orthographic; }
    }

    /// <summary>Rotation d'un sprite « debout » face à la caméra. En orthographique il est parallèle à l'écran (pixels non déformés).</summary>
    public static Quaternion Facing()
    {
        var cam = Camera.main;
        if (cam == null) return Quaternion.identity;
        if (Active) return cam.transform.rotation;
        Vector3 f = cam.transform.forward; f.y = 0f;
        return f.sqrMagnitude > 0.001f ? Quaternion.LookRotation(f) : Quaternion.identity;
    }

    /// <summary>Aligne une position sur la grille de pixels de l'écran (évite le scintillement / les demi-pixels).</summary>
    public static Vector3 Snap(Vector3 p)
    {
        var cam = Camera.main;
        if (cam == null || !Active) return p;
        Quaternion r = cam.transform.rotation;
        Vector3 l = Quaternion.Inverse(r) * p;
        l.x = Mathf.Round(l.x * PPU) / PPU;
        l.y = Mathf.Round(l.y * PPU) / PPU;
        return r * l;
    }
}
