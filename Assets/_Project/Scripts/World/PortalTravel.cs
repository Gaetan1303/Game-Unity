using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Portail entre les mondes. Mettre un Collider "Is Trigger" sur l'objet.</summary>
public class PortalTravel : MonoBehaviour
{
    public WorldData destination;

    void OnTriggerEnter(Collider other)
    {
        if (destination == null || other.GetComponentInParent<PlayerController>() == null) return;
        SceneManager.LoadScene(destination.sceneName);
    }
}
