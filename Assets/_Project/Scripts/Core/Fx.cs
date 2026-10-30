using System.Collections.Generic;
using UnityEngine;

/// <summary>Spawn d'effets 2D pixelisés : fondu, zoom, rotation, face caméra, orientation par rotation (jamais par retournement).</summary>
public static class Fx
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly HashSet<string> warned = new HashSet<string>();

    /// <summary>Direction vers laquelle chaque arc "bombe" dans l'image d'origine (y vers le haut). Mesuré sur les sprites.</summary>
    static readonly Dictionary<string, Vector2> bulge = new Dictionary<string, Vector2>
    {
        { "slash_01", new Vector2(0f, -1f) }, { "slash_02", new Vector2(0f, -1f) },
        { "slash_03", new Vector2(1f, 0f) },  { "slash_04", new Vector2(0f, 1f) },
    };

    public static Sprite Get(string name)
    {
        if (cache.TryGetValue(name, out var s) && s != null) return s;
        s = Resources.Load<Sprite>("Sprites/Fx/" + name);
        if (s == null && warned.Add(name)) Debug.LogWarning($"Sprite FX introuvable : Sprites/Fx/{name} (menu Jeu > 0 - Importer les sprites)");
        cache[name] = s;
        return s;
    }

    /// <param name="size0">taille monde au début (unités)</param><param name="size1">taille à la fin</param>
    /// <param name="life">durée ; float.PositiveInfinity = permanent (détruit par l'appelant)</param>
    public static FxSprite Spawn(string sprite, Vector3 pos, float size0, float size1, Color color, float life,
                                 float spin = 0f, bool flat = false, bool flipX = false, float angle = 0f,
                                 float stretchY = 1f, Vector3 vel = default(Vector3), int order = 50)
    {
        var sp = Get(sprite);
        if (sp == null) return null;
        var go = new GameObject("FX_" + sprite);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.sortingOrder = order; sr.flipX = flipX;
        var fx = go.AddComponent<FxSprite>();
        fx.sr = sr; fx.life = life; fx.size0 = size0; fx.size1 = size1; fx.color = color;
        fx.spin = spin; fx.flat = flat; fx.angle = angle; fx.stretchY = stretchY; fx.vel = vel;
        fx.Tick(0f);
        return fx;
    }

    /// <summary>Oriente un arc pour que sa « bosse » pointe vers 'toward' (écran, y vers le haut) : rotation pure, pas de flip.</summary>
    public static FxSprite SpawnDirected(string sprite, Vector3 pos, Vector2 toward, float size0, float size1,
                                         Color color, float life, int order = 60, float stretchY = 1f, float spin = 0f)
    {
        Vector2 b;
        if (!bulge.TryGetValue(sprite, out b)) b = new Vector2(1f, 0f);
        float ang = toward.sqrMagnitude < 0.0001f ? 0f : Vector2.SignedAngle(b, toward.normalized);
        return Spawn(sprite, pos, size0, size1, color, life, spin: spin, angle: ang, stretchY: stretchY, order: order);
    }

    public static void Hit(Vector3 pos)
    {
        Spawn("star_08", pos, 0.5f, 1.8f, new Color(1f, 0.92f, 0.6f), 0.18f, spin: 90f, order: 80);
    }

    /// <summary>Petit nuage de poussière aux pieds (pas, atterrissage). side = +1 si le personnage regarde à droite.</summary>
    public static void Dust(Vector3 feet, float side, float size)
    {
        Spawn("circle_05", new Vector3(feet.x - side * 0.3f, 0.12f, feet.z), 0.25f * size, 0.8f * size,
              new Color(0.85f, 0.78f, 0.66f, 0.7f), 0.3f, vel: new Vector3(-side * 0.7f, 0.6f, 0f), order: -60);
    }

    /// <summary>Image rémanente (traînée) : copie du sprite qui s'estompe.</summary>
    public static void Ghost(Sprite sp, Vector3 pos, Quaternion rot, Vector3 scale, bool flipX, Color c, float life)
    {
        if (sp == null) return;
        var go = new GameObject("Ghost");
        go.transform.position = pos; go.transform.rotation = rot; go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.flipX = flipX; sr.color = c; sr.sortingOrder = -1;
        var g = go.AddComponent<GhostFade>();
        g.sr = sr; g.life = life; g.a0 = c.a;
    }
}

public class GhostFade : MonoBehaviour
{
    public SpriteRenderer sr; public float life = 0.25f, a0 = 0.5f;
    float t;
    void Update()
    {
        t += Time.deltaTime;
        if (t >= life) { Destroy(gameObject); return; }
        var c = sr.color; c.a = a0 * (1f - t / life); sr.color = c;
    }
}

public class FxSprite : MonoBehaviour
{
    public SpriteRenderer sr;
    public float life, size0, size1, spin, angle, stretchY = 1f;
    public bool flat;
    public Color color = Color.white;
    public Vector3 vel;
    float t;

    public bool FlipX { set { if (sr) sr.flipX = value; } }

    void Update() { Tick(Time.deltaTime); }

    public void Tick(float dt)
    {
        t += dt;
        bool forever = float.IsPositiveInfinity(life);
        float k = forever ? 0f : t / life;
        if (k >= 1f) { Destroy(gameObject); return; }

        transform.position += vel * dt;
        angle += spin * dt;
        float s = Mathf.Lerp(size0, size1, k);
        transform.localScale = new Vector3(s, s * stretchY, 1f);

        if (flat) transform.rotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, angle);
        else transform.rotation = PixelView.Facing() * Quaternion.Euler(0f, 0f, angle);

        var c = color; c.a = color.a * (forever ? 1f : 1f - k * k);
        sr.color = c;
    }
}
