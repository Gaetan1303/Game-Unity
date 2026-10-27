using UnityEngine;

/// <summary>Colore le matériau quel que soit le pipeline (URP, Built-in, HDRP).</summary>
public class TintOnStart : MonoBehaviour
{
    public Color color = Color.white;

    void Awake()
    {
        bool ok = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var m = r.material; // instance
            foreach (var prop in new[] { "_BaseColor", "_Color", "_UnlitColor" })
                if (m.HasProperty(prop)) { m.SetColor(prop, color); ok = true; }
            if (!ok) Debug.LogWarning($"TintOnStart : shader '{m.shader.name}' sans propriété de couleur connue sur {name}", this);
        }
    }
}
