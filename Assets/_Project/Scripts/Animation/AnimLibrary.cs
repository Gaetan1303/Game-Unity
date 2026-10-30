using System.Collections.Generic;
using UnityEngine;

/// <summary>Charge Resources/Animations/&lt;nom&gt;.json et les sprites associés (avec cache).</summary>
public static class AnimLibrary
{
    static readonly Dictionary<string, AnimSet> sets = new Dictionary<string, AnimSet>();
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    static readonly HashSet<string> warned = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCaches() { sets.Clear(); sprites.Clear(); warned.Clear(); }

    public static void Warn(string key, string msg) { if (warned.Add(key)) Debug.LogWarning(msg); }

    public static AnimSet Load(string name)
    {
        AnimSet s;
        if (sets.TryGetValue(name, out s) && s != null) return s;
        var ta = Resources.Load<TextAsset>("Animations/" + name);
        if (ta == null) { Warn("set:" + name, $"Animations/{name}.json introuvable dans un dossier Resources."); return null; }
        s = JsonUtility.FromJson<AnimSet>(ta.text);
        sets[name] = s;
        return s;
    }

    /// <summary>
    /// Résout un nom de sprite. "attack1_0|walk_2" = essaie attack1_0, sinon walk_2 : les nouvelles frames générées
    /// (AssetPack) prennent automatiquement la place des anciennes dès qu'elles existent dans Resources/Sprites.
    /// </summary>
    public static Sprite GetSprite(AnimSet set, string name, Sprite self)
    {
        if (string.IsNullOrEmpty(name) || name == "$self") return self;
        string key = set.folder + "/" + name;
        Sprite sp;
        if (sprites.TryGetValue(key, out sp)) return sp;
        foreach (var cand in name.Split('|'))
        {
            sp = Resources.Load<Sprite>("Sprites/" + set.folder + "/" + cand.Trim());
            if (sp != null) break;
        }
        if (sp == null) Warn("spr:" + key, $"Sprite introuvable : Sprites/{key}");
        sprites[key] = sp;
        return sp;
    }
}
