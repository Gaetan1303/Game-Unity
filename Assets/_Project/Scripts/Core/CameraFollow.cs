using UnityEngine;

/// <summary>Suit le héros ; en combat, se place entre le héros et la cible, recule selon la distance, tremble sur les gros coups.</summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 4f, -9f);
    public float smooth = 5f;

    static float shake;
    public static void Shake(float amount) { shake = Mathf.Max(shake, amount); }

    TargetSelector sel;

    void LateUpdate()
    {
        if (target == null) return;
        if (sel == null) sel = target.GetComponent<TargetSelector>();

        Vector3 focus = target.position;
        float extra = 0f;
        if (sel != null && sel.Current != null)
        {
            Vector3 t = sel.Current.position;
            focus = (target.position + t) * 0.5f;
            extra = Mathf.Clamp(Vector3.Distance(target.position, t) * 0.3f, 0f, 5f);
        }

        Vector3 want = focus + offset + new Vector3(0f, extra * 0.4f, -extra);
        transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
        transform.LookAt(focus + Vector3.up * 1f);

        if (shake > 0.001f)
        {
            transform.position += Random.insideUnitSphere * shake;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
        }
    }
}
