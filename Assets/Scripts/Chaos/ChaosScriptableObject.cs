using UnityEngine;

[CreateAssetMenu(fileName = "ChaosScriptableObject", menuName = "Chaos/ChaosScriptableObject")]
public class ChaosScriptableObject : ScriptableObject
{
    public string chaosName;
    public Sprite chaosIcon;
    public string chaosDescription;
    public ChaosType chaosType;
}
