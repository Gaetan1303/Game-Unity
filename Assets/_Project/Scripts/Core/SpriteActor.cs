using UnityEngine;

/// <summary>
/// Base des personnages 2D : enfant "Visual" (SpriteRenderer) face caméra, ombre au sol, lecteur d'animations pose-à-pose,
/// états subis communs (touché, projeté, à terre, relevé, mort), flash de dégâts, alignement sur la grille de pixels.
/// Les sous-classes choisissent le clip de locomotion dans Animate().
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(Health))]
public abstract class SpriteActor : MonoBehaviour
{
    protected SpriteRenderer sr;
    protected Transform visual;
    protected CharacterController cc;
    protected Health hp;
    protected Knockable kn;

    protected readonly AnimPlayer anim = new AnimPlayer();
    protected AnimSet set;
    protected Sprite selfSprite;                 // sprite de l'acteur, pour "$self" dans les clips
    protected Vector3 baseScale = Vector3.one;   // échelle entière uniquement (pas de mixels)
    protected Color baseTint = Color.white;      // remis à blanc chaque frame ; Animate() peut le modifier
    protected Vector3 extraOffset;               // décalage monde ajouté chaque frame (ex: vol stationnaire)
    protected bool dead;

    public bool FacingRight { get; private set; } = true;
    protected bool Forced { get; private set; }                  // un état subi (touché, à terre...) prend la main
    protected bool InAction { get { return Time.time < actionUntil; } }

    Vector3 visualBase;
    Transform shadow;
    float flash, deadT, actionUntil;
    bool wasDown;

    protected virtual void Awake()
    {
        cc = GetComponent<CharacterController>();
        hp = GetComponent<Health>();
        kn = GetComponent<Knockable>();

        float groundY = cc.center.y - cc.height * 0.5f;
        visualBase = new Vector3(0f, groundY, 0f);

        var go = new GameObject("Visual");
        visual = go.transform;
        visual.SetParent(transform, false);
        visual.localPosition = visualBase;
        sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 0;

        var shadowSprite = Fx.Get("circle_05");
        if (shadowSprite != null)
        {
            var sh = new GameObject("Shadow");
            shadow = sh.transform;
            shadow.SetParent(transform, false);
            shadow.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ssr = sh.AddComponent<SpriteRenderer>();
            ssr.sprite = shadowSprite; ssr.color = new Color(0, 0, 0, 0.45f); ssr.sortingOrder = -90;
        }

        anim.Event += e => OnAnimEvent(e);
        hp.OnDamaged += _ => flash = 0.2f;
        hp.OnDied += () => { dead = true; };
    }

    // ------------------------------------------------------------ API pour les sous-classes
    protected static Sprite Load(string path)
    {
        var s = Resources.Load<Sprite>(path);
        if (s == null) AnimLibrary.Warn("spr:" + path, $"Sprite introuvable : {path} (menu Jeu > 0 - Importer les sprites)");
        return s;
    }

    protected void UseAnims(string setName) { set = AnimLibrary.Load(setName); anim.Set = set; }

    protected void Play(string clip, float speed = 1f, bool restart = false)
    {
        if (!anim.Play(clip, speed, restart)) AnimLibrary.Warn("clip:" + (set != null ? set.actor : "?") + "/" + clip, $"Clip d'animation introuvable : {clip}");
    }

    /// <summary>Joue un clip en entier pendant 'duration' secondes (les autres clips attendent la fin).</summary>
    protected float PlayAction(string clip, float duration)
    {
        float L = anim.LengthOf(clip);
        if (L <= 0f) { Play(clip, 1f, true); return 0f; }
        Play(clip, L / Mathf.Max(0.05f, duration), true);
        actionUntil = Time.time + duration;
        return duration;
    }

    /// <summary>
    /// Joue un clip en calant son évènement (ex: "hit") sur 'evTime' secondes après le début, sans dépasser trop 'maxTotal'.
    /// Permet de synchroniser l'animation avec le moment réel des dégâts.
    /// </summary>
    protected float PlayAligned(string clip, string ev, float evTime, float maxTotal)
    {
        float L = anim.LengthOf(clip);
        if (L <= 0f) { Play(clip, 1f, true); return 0f; }
        float te = anim.TimeOf(clip, ev);
        float speed = te > 0.001f ? te / Mathf.Max(0.03f, evTime) : L / Mathf.Max(0.1f, maxTotal);
        speed = Mathf.Clamp(Mathf.Max(speed, L / (maxTotal * 1.15f)), 0.3f, 4f);
        Play(clip, speed, true);
        float dur = L / speed;
        actionUntil = Time.time + dur;
        return dur;
    }

    protected void Dust(float size) { Fx.Dust(transform.position, FacingRight ? 1f : -1f, size); }

    protected void Ghost(Color c, float life = 0.25f)
    {
        Fx.Ghost(sr.sprite, visual.position, visual.rotation, visual.localScale, sr.flipX, c, life);
    }

    protected virtual void OnAnimEvent(string ev) { }

    /// <summary>Appelé chaque frame. Faire les effets continus, puis choisir le clip si !Forced &amp;&amp; !InAction.</summary>
    protected abstract void Animate();

    string ForcedClip()
    {
        if (dead) return "death";
        if (kn != null)
        {
            if (kn.Airborne) return "tumble";
            if (kn.Down) return "down";
            if (kn.Stunned) return "hurt";
        }
        return null;
    }

    // ------------------------------------------------------------ boucle
    void LateUpdate()
    {
        Quaternion facing = PixelView.Facing();
        visual.rotation = facing;

        float screenFacing = CombatUtil.ScreenFacingDot(transform);      // direction regardée projetée sur l'écran
        if (screenFacing > 0.01f) FacingRight = true; else if (screenFacing < -0.01f) FacingRight = false;

        if (kn != null)
        {
            bool down = kn.Down;
            if (wasDown && !down && !dead) PlayAction("getup", 0.3f);
            wasDown = down;
        }

        extraOffset = Vector3.zero;
        baseTint = Color.white;
        string forced = ForcedClip();
        Forced = forced != null;
        if (Forced) { actionUntil = 0f; Play(forced); }
        Animate();

        anim.Update(Time.deltaTime);
        ApplyPose(facing);
    }

    void ApplyPose(Quaternion facing)
    {
        var p = anim.Pose;
        float side = FacingRight ? 1f : -1f;

        Sprite sp = set != null ? AnimLibrary.GetSprite(set, p.sprite, selfSprite) : selfSprite;
        if (sp != null) sr.sprite = sp;
        sr.flipX = set != null && FacingRight != set.facesRight;

        visual.localScale = new Vector3(baseScale.x * p.sx, baseScale.y * p.sy, 1f);
        visual.rotation = facing * Quaternion.Euler(0f, 0f, -side * p.rot);
        Vector3 pos = transform.TransformPoint(visualBase) + Vector3.right * (side * p.x) + Vector3.up * p.y + extraOffset;
        visual.position = PixelView.Snap(pos);

        Color c = baseTint * new Color(p.tr, p.tg, p.tb, 1f);
        if (dead)
        {
            deadT += Time.deltaTime;
            c.a = Mathf.Clamp01(1f - deadT / 0.7f);
        }
        else if (flash > 0f)
        {
            flash -= Time.deltaTime;
            c = Color.Lerp(c, new Color(1f, 0.25f, 0.25f, 1f), Mathf.Clamp01(flash / 0.2f));
        }
        sr.color = c;

        if (shadow != null)
        {
            float h = Mathf.Max(0f, transform.position.y - cc.height * 0.5f);
            shadow.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
            shadow.localScale = Vector3.one * Mathf.Max(0.6f, Mathf.Max(1.2f, cc.radius * 3f) - h * 0.2f);
        }
    }
}
