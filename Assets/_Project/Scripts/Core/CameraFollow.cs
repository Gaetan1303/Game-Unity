using UnityEngine;

/// <summary>
/// Suit le héros. Mode pixel-perfect (par défaut) : caméra orthographique dont la taille donne TOUJOURS un nombre entier de pixels
/// d'écran par pixel d'art (résolution de référence 320x180 -> x6 en 1080p), position alignée sur la grille de pixels.
/// Désactive 'pixelPerfect' pour revenir à la caméra en perspective.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 4f, -9f);
    public float smooth = 5f;

    [Header("Pixel-perfect")]
    public bool pixelPerfect = true;
    [Tooltip("Doit correspondre au PPU de Editor/SpritePack.json (24)")] public float pixelsPerUnit = 24f;
    [Tooltip("Hauteur de la résolution de référence (320x180)")] public float baseHeight = 180f;

    static float shake;
    public static void Shake(float amount) { shake = Mathf.Max(shake, amount); }

    Camera cam;
    TargetSelector sel;
    Vector3 smoothPos;
    bool init;

    void Awake() { cam = GetComponent<Camera>(); }

    void LateUpdate()
    {
        if (target == null) return;
        if (sel == null) sel = target.GetComponent<TargetSelector>();

        PixelView.Enabled = pixelPerfect;
        PixelView.PPU = pixelsPerUnit;
        if (pixelPerfect)
        {
            int k = Mathf.Max(1, Mathf.RoundToInt(Screen.height / baseHeight));        // pixels d'écran par pixel d'art : entier
            cam.orthographic = true;
            cam.orthographicSize = Screen.height / (2f * pixelsPerUnit * k);
        }
        else cam.orthographic = false;

        Vector3 focus = target.position;
        float extra = 0f;
        if (sel != null && sel.Current != null)
        {
            Vector3 t = sel.Current.position;
            focus = (target.position + t) * 0.5f;
            if (!pixelPerfect) extra = Mathf.Clamp(Vector3.Distance(target.position, t) * 0.3f, 0f, 5f);   // pas de zoom fractionnaire en pixel-perfect
        }

        Vector3 want = focus + offset + new Vector3(0f, extra * 0.4f, -extra);
        if (!init) { smoothPos = want; init = true; }
        smoothPos = Vector3.Lerp(smoothPos, want, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));

        Vector3 pos = smoothPos;
        if (shake > 0.001f)
        {
            pos += Random.insideUnitSphere * shake;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
        }
        transform.position = pos;
        transform.LookAt(focus + Vector3.up * 1f);
        if (pixelPerfect) transform.position = PixelView.Snap(transform.position);
    }
}
