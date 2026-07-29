using System;
using DG.Tweening;
using UnityEngine;

public class SnakeSegment : MonoBehaviour
{
    public FoodType FoodType;
    private Collider collider;
    public GameObject arrowObject;
    
    [Header("Visual")]
    [SerializeField] Transform visual;
    [SerializeField] MeshRenderer visualMesh;
    private Material outlineMat;
    [SerializeField] private ParticleSystem spawnOnBodyParticle;
    
    private bool isAttached =  false;

    private void Awake()
    {
        collider = GetComponent<Collider>();
        outlineMat = visualMesh.materials[1];
    }

    public void OnAddedToBody()
    {
        isAttached = true;
    }

    public void EnableArrowObject()
    {
        if(arrowObject != null)
            arrowObject.SetActive(true);
    }

    public void DisableArrowObject()
    {
        if(arrowObject != null)
            arrowObject.SetActive(false);
    }

    public bool IsAttached()
    {
        return isAttached;
    }

    public void EnableOutline()
    {
        outlineMat.SetFloat("_Scale", 1.1f);
        spawnOnBodyParticle.Play();
    }

    public void DisableOutline()
    {
        outlineMat.SetFloat("_Scale", 1f);
    }
    

    public void PlayEatWave()
    {
        DOTween.Sequence()
            .SetDelay(0.1f)
            .Append(visual.DOScale(1.15f,0.08f))
            .Append(visual.DOScale(1f,0.12f));
    }

    public void PlayDestroy(System.Action callback)
    {
        DOTween.Sequence()
            .Append(visual.DOScale(1.2f,0.05f))
            .Append(visual.DOScale(0f,0.12f))
            .OnComplete(()=>callback?.Invoke());
    }
}