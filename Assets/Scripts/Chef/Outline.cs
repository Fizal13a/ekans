using UnityEngine;

public class Outline : MonoBehaviour
{
    [SerializeField] MeshRenderer visualMesh;
    private Material outlineMat;
    
    private void Awake()
    {
        outlineMat = visualMesh.materials[1];
    }
    
    public void EnableOutline()
    {
        outlineMat.SetFloat("_Scale", 1.1f);
    }

    public void DisableOutline()
    {
        outlineMat.SetFloat("_Scale", 1f);
    }
}
