using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class ChefPanAttack : MonoBehaviour, IChefAttack
{
    [Header("Spawn Area")]
    [SerializeField] private Transform spawnArea;

    [Header("Pan")]
    [SerializeField] private Transform panHolder;
    [SerializeField] private Transform pan;
    [SerializeField] private Outline panOutline;

    [Header("Attack")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float spinSpeed = 1440f;
    [SerializeField] private float delayBeforePoints = 1f;

    [Header("Points")]
    [SerializeField] private int pointCount = 4;
    [SerializeField] private float minPointDistance = 2f;
    [SerializeField] private int maxPointGenerationAttempts = 100;
    
    
    [Header("Throw Animation")] [SerializeField]
    private float throwDuration = 0.5f;
    [SerializeField] private float throwHeight = 2f;
    [SerializeField] private Ease throwEase = Ease.OutQuad;
    
    [Header("Juice - pan throw")] [SerializeField]
    private float spawnPunchScale = 0.3f;
    [SerializeField] private float spawnPunchDuration = 0.3f;
    [SerializeField] private int spawnPunchVibrato = 8;
    [SerializeField] private float spawnPunchElasticity = 0.8f;
    
    [Header("Juice - Landing Squash")] [SerializeField]
    private float landingSquashDuration = 0.1f;
    [SerializeField] private float landingRecoverDuration = 0.25f;
    [SerializeField] private float landingSquashFlatten = 0.6f;
    [SerializeField] private float landingSquashWiden = 1.3f;

    
    [Header(" Particles")] [SerializeField]
    private float particlePopInDuration = 0.3f;
    [SerializeField] private ParticleSystem targetPointParticle;

    [Header("Juice")]
    [SerializeField] private Ease moveEase = Ease.InOutQuad;
    [SerializeField] private float centerImpactScale = 0.2f;
    [SerializeField] private float centerImpactDuration = 0.2f;

    private Vector3 chefPosition;

    private Coroutine attackRoutine;
    private Tween spinTween;

    public event Action OnAttackFinished;

    private void Awake()
    {
        chefPosition = panHolder.position;

        // Make sure pan starts attached to the holder.
        AttachPan();
    }

    public void StartAttack()
    {
        if (attackRoutine != null)
            return;

        attackRoutine = StartCoroutine(PanAttackRoutine());
    }

    private IEnumerator PanAttackRoutine()
    {
        // ------------------------------------------------
        // 1. Get center of spawn area
        // ------------------------------------------------
        
        Vector3 centerPosition = GetSpawnAreaCenter();
        targetPointParticle.transform.position = centerPosition;
        EnableParticleWithJuice(targetPointParticle);
        
        yield return new WaitForSeconds(1);
        
        // ------------------------------------------------
        // 2. Detach pan
        // ------------------------------------------------

        DetachPan();

        // ------------------------------------------------
        // 3. Move pan to center
        // ------------------------------------------------

        // float centerDistance =
        //     Vector3.Distance(pan.position, centerPosition);
        //
        // float centerDuration =
        //     centerDistance / moveSpeed;
        //
        // Tween centerTween = pan
        //     .DOMove(centerPosition, centerDuration)
        //     .SetEase(moveEase);
        //
        // yield return centerTween.WaitForCompletion();
        
        ThrowObject(pan,centerPosition);
        
        yield return new WaitForSeconds(2);
        
        targetPointParticle.gameObject.SetActive(false);

        // ------------------------------------------------
        // 4. Center impact
        // ------------------------------------------------

        pan.DOPunchScale(
            pan.localScale * centerImpactScale,
            centerImpactDuration,
            8,
            0.8f
        );

        // ------------------------------------------------
        // 5. Start spinning
        // ------------------------------------------------

        StartSpinning();

        // ------------------------------------------------
        // 6. Wait before selecting points
        // ------------------------------------------------

        yield return new WaitForSeconds(delayBeforePoints);

        // ------------------------------------------------
        // 7. Generate attack points
        // ------------------------------------------------

        List<Vector3> points = GeneratePoints();

        if (points.Count < pointCount)
        {
            Debug.LogWarning(
                $"ChefPanAttack: Could only generate {points.Count}/{pointCount} valid points."
            );
        }

        // ------------------------------------------------
        // 8. Move through all points
        // ------------------------------------------------

        Vector3 currentPosition = centerPosition;

        foreach (Vector3 point in points)
        {
            float distance =
                Vector3.Distance(currentPosition, point);

            float duration =
                distance / moveSpeed;

            Tween moveTween = pan
                .DOMove(point, duration)
                .SetEase(moveEase);

            yield return moveTween.WaitForCompletion();

            currentPosition = point;
        }

        // ------------------------------------------------
        // 9. Return to chef
        // ------------------------------------------------

        float returnDistance =
            Vector3.Distance(pan.position, chefPosition);

        float returnDuration =
            returnDistance / moveSpeed;

        Tween returnTween = pan
            .DOMove(chefPosition, returnDuration)
            .SetEase(Ease.InOutQuad);

        yield return returnTween.WaitForCompletion();

        // ------------------------------------------------
        // 10. Stop spinning
        // ------------------------------------------------

        StopSpinning();

        // ------------------------------------------------
        // 11. Reattach pan
        // ------------------------------------------------

        AttachPan();

        // ------------------------------------------------
        // 12. Attack finished
        // ------------------------------------------------

        GameManager.events.TriggerEvent(GameEvents.EventType.OnAttackFinished);

        attackRoutine = null;
    }

    // ====================================================
    // POINT GENERATION
    // ====================================================

    private List<Vector3> GeneratePoints()
    {
        List<Vector3> points = new List<Vector3>();

        Bounds bounds = GetSpawnAreaBounds();

        Vector3 center = GetSpawnAreaCenter();

        int attempts = 0;

        while (
            points.Count < pointCount &&
            attempts < maxPointGenerationAttempts
        )
        {
            attempts++;

            float x = Random.Range(
                bounds.min.x,
                bounds.max.x
            );

            float z = Random.Range(
                bounds.min.z,
                bounds.max.z
            );

            Vector3 candidate = new Vector3(
                x,
                bounds.max.y,
                z
            );

            // --------------------------------------------
            // Candidate must be away from center
            // --------------------------------------------

            if (Vector3.Distance(candidate, center) < minPointDistance)
                continue;

            // --------------------------------------------
            // Candidate must be away from other points
            // --------------------------------------------

            bool tooClose = false;

            foreach (Vector3 point in points)
            {
                if (Vector3.Distance(candidate, point) < minPointDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            points.Add(candidate);
        }

        return points;
    }

    // ====================================================
    // SPAWN AREA
    // ====================================================

    private Bounds GetSpawnAreaBounds()
    {
        Renderer renderer =
            spawnArea.GetComponent<Renderer>();

        return renderer.bounds;
    }

    private Vector3 GetSpawnAreaCenter()
    {
        Bounds bounds = GetSpawnAreaBounds();

        return new Vector3(
            bounds.center.x,
            bounds.max.y,
            bounds.center.z
        );
    }
    
    private void EnableParticleWithJuice(ParticleSystem particle)
    {
        Transform t = particle.transform;
        Vector3 baseScale = t.localScale;

        t.DOKill();
        t.localScale = Vector3.zero;
        particle.gameObject.SetActive(true);
        t.DOScale(baseScale, particlePopInDuration).SetEase(Ease.OutBack);
        particle.Play();
    }

    // ====================================================
    // PAN
    // ====================================================
    
    private void ThrowObject(Transform food, Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;

        Vector3 midPoint = Vector3.Lerp(startPosition, targetPosition, 0.5f);

        midPoint.y += throwHeight;

        Vector3[] path = { startPosition, midPoint, targetPosition };

        Vector3 baseScale = pan.localScale;

        // Kill any leftover tween from a previous throw/disable cycle
        food.DOKill();

        food.position = startPosition;
        food.rotation = Quaternion.identity;
        food.localScale = baseScale;
        food.gameObject.SetActive(true);

        // Juice: little punch as it's "shot" out
        food.DOPunchScale(baseScale * spawnPunchScale, spawnPunchDuration, spawnPunchVibrato, spawnPunchElasticity);

        food.DOPath(path, throwDuration, PathType.CatmullRom)
            .SetEase(throwEase)
            .OnComplete(() => PlayLandingSquash(food, baseScale));
    }
    
    private void PlayLandingSquash(Transform food, Vector3 baseScale)
    {
        if (!food.gameObject.activeSelf) return;

        Sequence landingSequence = DOTween.Sequence();

        landingSequence.Append(food
            .DOScale(
                new Vector3(baseScale.x * landingSquashWiden, baseScale.y * landingSquashFlatten,
                    baseScale.z * landingSquashWiden), landingSquashDuration)
            .SetEase(Ease.OutQuad));

        landingSequence.Append(food.DOScale(baseScale, landingRecoverDuration).SetEase(Ease.OutElastic));
    }

    private void DetachPan()
    {
        pan.SetParent(null, true);
        pan.rotation = Quaternion.identity;
        panOutline.EnableOutline();
    }

    private void AttachPan()
    {
        pan.SetParent(panHolder);

        pan.localPosition = Vector3.zero;
        pan.localRotation = Quaternion.identity;
        
        panOutline.DisableOutline();
    }

    // ====================================================
    // SPIN
    // ====================================================

    private void StartSpinning()
    {
        StopSpinning();

        spinTween = pan
            .DORotate(
                pan.eulerAngles +
                new Vector3(0f, spinSpeed, 0f),
                1f,
                RotateMode.FastBeyond360
            )
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
    }

    private void StopSpinning()
    {
        if (spinTween != null)
        {
            spinTween.Kill();
            spinTween = null;
        }
    }

    // ====================================================
    // CLEANUP
    // ====================================================

    private void OnDisable()
    {
        StopSpinning();

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        pan.DOKill();

        AttachPan();
    }

    private void OnDestroy()
    {
        StopSpinning();

        pan.DOKill();
    }
}
