using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class ChefBoomerangAttack : MonoBehaviour, IChefAttack
{
    private Animator animator;
    [SerializeField] private Transform player;
    
    [Header("Path")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float lineRevealDuration = 1f;
    [SerializeField] private List<Transform> pathList = new List<Transform>();

    [Header("Knife")]
    [SerializeField] private Transform knifeHolder;
    [SerializeField] private Transform knife;
    [SerializeField] private Outline outlineClass;
    [SerializeField] private float knifeRotateDuration = 0.8f;
    [SerializeField] private float travelDuration = 0.8f;
    [SerializeField] private float returnDuration = 0.8f;

    [Header("Juice")]
    [SerializeField] private float knifeRotationSpeed = 720f;
    [SerializeField] private float knifePunchScale = 0.2f;
    [SerializeField] private float knifePunchDuration = 0.2f;

    private Material lineMaterial;
    private Color lineColor;

    private Vector3 knifeStartPosition;
    private Vector3 targetPosition;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
        
        knifeStartPosition = knifeHolder.position;
        knifeStartPosition.y = 1f;

        lineMaterial = lineRenderer.material;
        lineColor = lineMaterial.color;

        SetLineAlpha(0f);

        lineRenderer.positionCount = 0;
    }

    public void StartAttack()
    {
        StartCoroutine(BoomerangRoutine());
    }

    private IEnumerator BoomerangRoutine()
    {
        // --------------------------------
        // 1. Choose target
        // --------------------------------

        targetPosition = GetTargetPosition();

        // --------------------------------
        // 2. Draw path
        // --------------------------------

        Draw(knifeStartPosition, targetPosition);
        
        knife.transform.localScale = new Vector3(1f, 1f, 1f);

        lineMaterial
            .DOColor(
                new Color(lineColor.r, lineColor.g, lineColor.b, 1f),
                lineRevealDuration
            )
            .SetEase(Ease.OutQuad);

        yield return new WaitForSeconds(lineRevealDuration);

        // --------------------------------
        // 3. Throw knife
        // --------------------------------

        animator.SetTrigger("Shoot");
        knife.gameObject.SetActive(true);

        knife.DOKill();

        knife.DOPunchScale(
            knife.localScale * knifePunchScale,
            knifePunchDuration,
            8,
            0.8f
        );

        DetachKnife();

        Vector3 throwDirection =
            (targetPosition - knife.position).normalized;

        Quaternion throwRotation =
            Quaternion.FromToRotation(Vector3.right, throwDirection);
        
        Sequence throwSequence = DOTween.Sequence();

        throwSequence.Join(
            knife.DOMove(targetPosition, travelDuration)
                .SetEase(Ease.OutQuad)
        );

        throwSequence.Join(
            knife.DORotateQuaternion(
                throwRotation,
                knifeRotateDuration
            )
            .SetEase(Ease.OutQuad)
        );

        yield return throwSequence.WaitForCompletion();

        // --------------------------------
        // 4. Impact
        // --------------------------------

        knife.DOPunchScale(
            knife.localScale * 0.15f,
            0.15f,
            6,
            0.7f
        );

        yield return new WaitForSeconds(0.15f);

        // --------------------------------
        // 5. Return
        // --------------------------------

        Vector3 returnDirection =
            (knifeStartPosition - knife.position).normalized;

        Quaternion returnRotation =
            Quaternion.FromToRotation(Vector3.right, returnDirection);
        
        Sequence returnSequence = DOTween.Sequence();

        returnSequence.Join(
            knife.DOMove(
                knifeStartPosition,
                returnDuration
            )
            .SetEase(Ease.InOutQuad)
        );

        returnSequence.Join(
            knife.DORotateQuaternion(
                returnRotation,
                knifeRotateDuration
            )
            .SetEase(Ease.InOutQuad)
        );

        yield return returnSequence.WaitForCompletion();

        // --------------------------------
        // 6. Reattach
        // --------------------------------

        AttachKnife();

        // --------------------------------
        // 7. Hide path
        // --------------------------------

        lineMaterial
            .DOColor(
                new Color(
                    lineColor.r,
                    lineColor.g,
                    lineColor.b,
                    0f
                ),
                0.25f
            )
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                lineRenderer.positionCount = 0;
            });

        yield return new WaitForSeconds(0.25f);

        // --------------------------------
        // Attack finished
        // --------------------------------
        knife.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        GameManager.events.TriggerEvent(GameEvents.EventType.OnAttackFinished);
    }

    public void Draw(Vector3 startPosition, Vector3 endPosition)
    {
        lineRenderer.positionCount = 2;

        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
    }

    private void DetachKnife()
    {
        knife.SetParent(null, true);

        knife.rotation = Quaternion.Euler(90f, 0f, 0f);
        outlineClass.EnableOutline();
    }

    private void AttachKnife()
    {
        knife.SetParent(knifeHolder);

        knife.localPosition = Vector3.zero;
        knife.localRotation = Quaternion.identity;
        
        outlineClass.DisableOutline();
    }

    private void SetLineAlpha(float alpha)
    {
        lineColor.a = alpha;
        lineMaterial.color = lineColor;
    }

    private Vector3 GetTargetPosition()
    {
        Vector3 playerPosition = player.position;

        Transform closestPoint = null;
        float closestDistanceSqr = Mathf.Infinity;

        foreach (Transform point in pathList)
        {
            float distanceSqr = (playerPosition - point.position).sqrMagnitude;

            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestPoint = point;
            }
        }

        Vector3 targetPos = closestPoint.position;
        targetPos.y = 1f;

        return targetPos;
    }
}