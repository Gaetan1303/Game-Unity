using System;
using System.Collections.Generic;

/// <summary>
/// Données d'animation chargées depuis Resources/Animations/*.json (modifiables sans toucher au code).
/// Toutes les valeurs absentes du JSON valent 0 : t=0 -> 0.1 s, sx/sy=0 -> 1, blend=0 -> 0.6.
/// </summary>
[Serializable]
public class AnimFrame
{
    public string s;      // nom du sprite ("" = garde le précédent, "$self" = le sprite de l'acteur)
    public float t;       // durée (secondes)
    public float x, y;    // décalage en unités monde ; x est relatif à la direction regardée (+ = vers l'avant)
    public float rot;     // degrés, + = penché vers l'avant
    public float sx, sy;  // échelle
    public float blend;   // part de la durée utilisée pour glisser depuis la pose précédente (0..1)
    public bool snap;     // vrai = pas de transition (coupure franche)
    public string tint;   // "#RRGGBB" (multiplie la couleur du sprite)
    public string ev;     // évènement déclenché à l'entrée dans ce frame (hit, release, step, land, slash...)
}

[Serializable]
public class AnimClip
{
    public string name;
    public bool loop;
    public AnimFrame[] frames;

    [NonSerialized] float[] starts;
    [NonSerialized] float length;

    public static float Dur(AnimFrame f) { return f.t > 0f ? f.t : 0.1f; }

    void Build()
    {
        if (starts != null) return;
        starts = new float[frames.Length];
        float acc = 0f;
        for (int i = 0; i < frames.Length; i++) { starts[i] = acc; acc += Dur(frames[i]); }
        length = acc;
    }

    public float Length { get { Build(); return length; } }
    public float Start(int i) { Build(); return starts[i]; }

    /// <summary>Instant (en secondes de clip) où le premier frame portant cet évènement commence, ou -1.</summary>
    public float TimeOf(string ev)
    {
        Build();
        for (int i = 0; i < frames.Length; i++) if (frames[i].ev == ev) return starts[i];
        return -1f;
    }
}

[Serializable]
public class AnimSet
{
    public string actor;
    public string folder;       // sous-dossier de Resources/Sprites contenant les images
    public bool facesRight;     // l'art d'origine regarde vers la droite ?
    public AnimClip[] clips;

    [NonSerialized] Dictionary<string, AnimClip> map;

    public AnimClip Get(string name)
    {
        if (clips == null) return null;
        if (map == null)
        {
            map = new Dictionary<string, AnimClip>();
            foreach (var c in clips) if (c != null && c.frames != null && c.frames.Length > 0) map[c.name] = c;
        }
        AnimClip r;
        return map.TryGetValue(name, out r) ? r : null;
    }
}
