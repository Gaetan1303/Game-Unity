using UnityEngine;

/// <summary>Recul, vacillement, projection en l'air (jongle) et mise à terre. Gère son propre déplacement tant que l'entité est sonnée.</summary>
[RequireComponent(typeof(CharacterController), typeof(Health))]
public class Knockable : MonoBehaviour
{
    public float drag = 9f;
    public float gravity = -22f;

    CharacterController cc;
    Health hp;
    Vector3 impulse;
    float vy, stunUntil, downUntil;
    bool airborne, wasDown;

    [Tooltip("Armure après un relevé (secondes) : empêche d'être enchaîné à terre indéfiniment")]
    public float wakeUpArmor = 1f;

    public bool Airborne => airborne;
    public bool Down => Time.time < downUntil;
    public bool Stunned => airborne || Time.time < stunUntil || Time.time < downUntil;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        hp = GetComponent<Health>();
        hp.OnDamaged += d =>
        {
            impulse += d.knockback * 0.25f;                       // petit recul à chaque coup
            if (airborne) vy = Mathf.Max(vy, 3.5f);                // jongle : chaque coup relance un peu
        };
        hp.OnStaggered += Stagger;
    }

    void Stagger(DamageInfo d)
    {
        impulse = d.knockback;
        if (d.launch > 0f)
        {
            airborne = true;
            vy = airborne && vy > 0f ? Mathf.Max(d.launch * 0.7f, vy) : d.launch;
            stunUntil = Time.time + 0.5f;
        }
        else if (d.knockdown) downUntil = Time.time + 1.1f;
        else stunUntil = Time.time + 0.45f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        // sous 5 cm/s le recul est invisible : on l'arrête (sinon cc.Move était appelé ~5 s après chaque coup,
        // ce qui remettait isGrounded à false et écrasait cc.velocity pour les contrôleurs et les animations)
        if (impulse.sqrMagnitude < 0.0025f) impulse = Vector3.zero;
        Vector3 move = impulse * dt;
        if (airborne) { vy += gravity * dt; move.y = vy * dt; }
        else if (move.sqrMagnitude > 0f) move.y = -2f * dt;     // un Move purement horizontal ferait perdre le contact avec le sol
        if (move.sqrMagnitude > 0f) cc.Move(move);
        impulse = Vector3.Lerp(impulse, Vector3.zero, drag * dt);

        if (airborne && vy < 0f && cc.isGrounded)
        {
            airborne = false; vy = 0f;
            downUntil = Time.time + 0.7f;
            Fx.Spawn("circle_02", new Vector3(transform.position.x, 0.05f, transform.position.z), 0.5f, 2.0f,
                     new Color(1f, 1f, 1f, 0.6f), 0.3f, flat: true, order: -78);
        }

        bool down = Down;
        if (wasDown && !down) hp.armorUntil = Mathf.Max(hp.armorUntil, Time.time + wakeUpArmor);
        wasDown = down;
    }
}
