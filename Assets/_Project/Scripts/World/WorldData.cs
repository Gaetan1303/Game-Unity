using UnityEngine;

[CreateAssetMenu(menuName = "Jeu/Monde", fileName = "NouveauMonde")]
public class WorldData : ScriptableObject
{
    public string worldName = "Monde";
    [TextArea] public string description;
    public string sceneName;          // doit être dans Build Settings
    public int recommendedLevel = 1;
}
