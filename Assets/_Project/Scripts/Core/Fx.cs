using System.Collections.Generic;
using UnityEngine;

/// <summary>Spawn d'effets 2D (sprites Kenney) : fondu, zoom, rotation, face caméra.</summary>
public static class Fx
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly HashSet<string> warned = new HashSet<string>();

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

    public static void Hit(Vector3 pos)
    {
        Spawn("star_08", pos, 0.5f, 1.8f, new Color(1f, 0.92f, 0.6f), 0.18f, spin: 90f, order: 80);
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
        else
        {
            var cam = Camera.main;
            Quaternion yaw = Quaternion.identity;
            if (cam != null) { Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude > 0.001f) yaw = Quaternion.LookRotation(f); }
            transform.rotation = yaw * Quaternion.Euler(0f, 0f, angle);
        }

        var c = color; c.a = color.a * (forever ? 1f : 1f - k * k);
        sr.color = c;
    }
}
