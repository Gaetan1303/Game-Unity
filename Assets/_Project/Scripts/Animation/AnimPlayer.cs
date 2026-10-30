using System;

public struct AnimPose
{
    public string sprite;
    public float x, y, rot, sx, sy;
    public float tr, tg, tb;           // teinte multiplicative

    public static AnimPose Identity { get { return new AnimPose { sx = 1f, sy = 1f, tr = 1f, tg = 1f, tb = 1f }; } }

    public static AnimPose Lerp(AnimPose a, AnimPose b, float k)
    {
        return new AnimPose
        {
            sprite = b.sprite,
            x = a.x + (b.x - a.x) * k, y = a.y + (b.y - a.y) * k, rot = a.rot + (b.rot - a.rot) * k,
            sx = a.sx + (b.sx - a.sx) * k, sy = a.sy + (b.sy - a.sy) * k,
            tr = a.tr + (b.tr - a.tr) * k, tg = a.tg + (b.tg - a.tg) * k, tb = a.tb + (b.tb - a.tb) * k
        };
    }
}

/// <summary>
/// Lecteur de clips « pose à pose » : chaque frame est une pose-clé (sprite + décalage + rotation + échelle + teinte)
/// vers laquelle on glisse avec un easing. Permet de faire de l'anticipation / de l'élan / de l'écrasement
/// même avec peu d'images. Aucune dépendance à Unity (testable seul).
/// </summary>
public class AnimPlayer
{
    public AnimSet Set;
    public AnimClip Clip { get; private set; }
    public float Speed { get; private set; }
    public bool Finished { get; private set; }
    public float Time { get; private set; }
    public AnimPose Pose { get; private set; }
    public event Action<string> Event;
    public string ClipName { get { return Clip != null ? Clip.name : null; } }

    int frame;
    AnimPose blendFrom;
    string curSprite;

    public AnimPlayer() { Pose = AnimPose.Identity; Speed = 1f; }

    public float LengthOf(string name) { var c = Set != null ? Set.Get(name) : null; return c != null ? c.Length : 0f; }
    public float TimeOf(string name, string ev) { var c = Set != null ? Set.Get(name) : null; return c != null ? c.TimeOf(ev) : -1f; }
    public bool Has(string name) { return Set != null && Set.Get(name) != null; }

    /// <returns>false si le clip n'existe pas.</returns>
    public bool Play(string name, float speed = 1f, bool restart = false)
    {
        if (Set == null) return false;
        var c = Set.Get(name);
        if (c == null) return false;
        if (!restart && Clip == c) { Speed = speed; return true; }   // déjà en cours (ou terminé et tenu) : on ne relance pas

        blendFrom = Pose;                 // on part de la pose actuelle : transition continue entre clips
        Clip = c; Speed = speed; Time = 0f; Finished = false;
        Enter(0);
        Evaluate();
        return true;
    }

    void Enter(int i)
    {
        frame = i;
        var f = Clip.frames[i];
        if (!string.IsNullOrEmpty(f.s)) curSprite = f.s;
        if (i > 0 || Clip != null) blendFrom = Pose;   // continuité même si le frame précédent n'a pas fini de glisser
        if (!string.IsNullOrEmpty(f.ev) && Event != null) Event(f.ev);
    }

    public void Update(float dt)
    {
        if (Clip == null) return;
        if (!Finished) Time += dt * Speed;

        int guard = Clip.frames.Length + 2;
        while (!Finished && guard-- > 0)
        {
            float end = Clip.Start(frame) + AnimClip.Dur(Clip.frames[frame]);
            if (Time < end) break;
            if (frame + 1 < Clip.frames.Length) { Evaluate(); Enter(frame + 1); }
            else if (Clip.loop && Clip.Length > 0.0001f) { Time -= Clip.Length; Evaluate(); Enter(0); }
            else { Finished = true; Time = Clip.Length; }
        }
        Evaluate();
    }

    static float EaseOutCubic(float k) { float u = 1f - k; return 1f - u * u * u; }

    static void ParseTint(string hex, out float r, out float g, out float b)
    {
        r = g = b = 1f;
        if (string.IsNullOrEmpty(hex) || hex.Length < 7 || hex[0] != '#') return;
        try
        {
            r = Convert.ToInt32(hex.Substring(1, 2), 16) / 255f;
            g = Convert.ToInt32(hex.Substring(3, 2), 16) / 255f;
            b = Convert.ToInt32(hex.Substring(5, 2), 16) / 255f;
        }
        catch (FormatException) { r = g = b = 1f; }
    }

    AnimPose Target(AnimFrame f)
    {
        var p = new AnimPose { sprite = curSprite, x = f.x, y = f.y, rot = f.rot, sx = f.sx > 0f ? f.sx : 1f, sy = f.sy > 0f ? f.sy : 1f };
        ParseTint(f.tint, out p.tr, out p.tg, out p.tb);
        return p;
    }

    void Evaluate()
    {
        var f = Clip.frames[frame];
        var target = Target(f);
        float d = AnimClip.Dur(f);
        float u = Time - Clip.Start(frame);
        float k = 1f;
        if (!f.snap)
        {
            float b = f.blend > 0f ? f.blend : 0.6f;
            k = Math.Min(1f, Math.Max(0f, u / (d * b)));
            k = EaseOutCubic(k);
        }
        Pose = AnimPose.Lerp(blendFrom, target, k);
        var p = Pose; p.sprite = curSprite; Pose = p;
    }
}
