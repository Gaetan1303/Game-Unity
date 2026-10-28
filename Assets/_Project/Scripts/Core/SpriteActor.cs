using UnityEngine;

/// <summary>Base des personnages 2D : enfant "Visual" (SpriteRenderer) toujours face caméra, ombre au sol, flash de dégâts, chute et mort.</summary>
[RequireComponent(typeof(CharacterController), typeof(Health))]
public abstract class SpriteActor : MonoBehaviour
{
    protected SpriteRenderer sr;
    protected Transform visual;
    protected CharacterController cc;
    protected Health hp;
    protected Knockable kn;
    protected bool baseFacesRight = true;
    protected bool FacingRight { get; private set; } = true;
    protected Vector3 extraOffset;
    protected bool dead;

    Vector3 visualBase;
    Transform shadow;
    float flash, deadT;

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

        hp.OnDamaged += _ => flash = 0.2f;
        hp.OnDied += () => { dead = true; };
    }

    protected void SetBaseFacing(bool facesRight)
    {
        baseFacesRight = facesRight;
        FacingRight = facesRight;
    }

    protected static Sprite Load(string path)
    {
        var s = Resources.Load<Sprite>(path);
        if (s == null) Debug.LogWarning($"Sprite introuvable : {path} (menu Jeu > 0 - Importer les sprites)");
        return s;
    }

    protected abstract void Animate();

    void LateUpdate()
    {
        var cam = Camera.main;
        Quaternion yaw = Quaternion.identity;
        if (cam != null) { Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude > 0.001f) yaw = Quaternion.LookRotation(f); }
        visual.rotation = yaw;

        float screenFacing = CombatUtil.ScreenFacingDot(transform);
        if (screenFacing > 0.01f) FacingRight = true;
        else if (screenFacing < -0.01f) FacingRight = false;
        sr.flipX = FacingRight != baseFacesRight;

        extraOffset = Vector3.zero;
        visual.localScale = Vector3.one;
        Animate();
        visual.position = transform.TransformPoint(visualBase) + extraOffset;

        // ombre toujours au sol, plus petite quand on est en l'air
        if (shadow != null)
        {
            float h = Mathf.Max(0f, transform.position.y - cc.height * 0.5f);
            shadow.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
            shadow.localScale = Vector3.one * Mathf.Max(0.6f, Mathf.Max(1.2f, cc.radius * 3f) - h * 0.2f);
        }

        float side = FacingRight ? -1f : 1f;
        if (dead)
        {
            deadT += Time.deltaTime;
            sr.color = new Color(1, 1, 1, Mathf.Clamp01(1f - deadT / 0.7f));
            visual.rotation *= Quaternion.Euler(0, 0, side * Mathf.Min(80f, deadT * 200f));
            return;
        }
        if (kn != null)
        {
            if (kn.Airborne) visual.rotation *= Quaternion.Euler(0, 0, side * 25f);
            else if (kn.Down) visual.rotation *= Quaternion.Euler(0, 0, side * 75f);
        }
        if (flash > 0f)
        {
            flash -= Time.deltaTime;
            sr.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.25f), Mathf.Clamp01(flash / 0.2f));
        }
        else sr.color = Color.white;
    }
}
