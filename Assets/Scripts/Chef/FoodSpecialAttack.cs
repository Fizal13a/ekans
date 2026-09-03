using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerSpecialAttack
{
    public List<Transform> availableFoods = new List<Transform>();
}

public class FoodSpecialAttack : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private Transform foodGatherPoint;

    [SerializeField] private Transform chefTarget;
    [SerializeField] private Transform cameraTransform; // assign Camera.main's transform (or a shake rig parent above it)
    [SerializeField] private Camera targetCamera; // optional, assign Camera.main for FOV zoom. Leave empty to skip zoom.

    [Header("Timing")] [SerializeField] private float gatherDuration = 1f;
    [SerializeField] private float chargeDuration = 2f;
    [SerializeField] private float anticipationDuration = 0.15f;
    [SerializeField] private float launchDuration = 0.8f;
    [SerializeField] private float hitStopDuration = 0.06f;

    [Header("Sphere")] [SerializeField] private float sphereRadius = 4f;
    [SerializeField] private float launchDistance = 1f;

    [Header("Arc")] [SerializeField] private float arcHeight = 6f;
    [SerializeField] private int arcSpins = 2;

    [Header("Camera Shake (continuous)")]
    [SerializeField] private float shakeStartStrength = 0.05f;
    [SerializeField] private float shakeEndStrength = 0.35f;
    [SerializeField] private float shakeNoiseFrequency = 25f; // how fast the vibration jitters, higher = more jittery
    [SerializeField] private float impactShakeStrength = 0.6f;
    [SerializeField] private float impactShakeDuration = 0.25f;

    [Header("Camera Framing / Zoom")]
    [SerializeField] private float framingDuration = 0.35f; // how long it takes to move into the "watch the sphere and the chef" shot
    [SerializeField] private float framingBackDistance = 1.5f; // how far the camera pulls back to fit both the ball and the chef
    [SerializeField] private float framingHeight = 1f; // extra height added while framing
    [SerializeField] private float zoomFOV = 45f; // target field of view while charging (lower = tighter zoom). Ignored if targetCamera is unset.
    [SerializeField] private float returnDuration = 0.45f; // how long it takes to ease back to the original view at the end of the attack

    [Header("Attack")] [SerializeField] private int damage = 20;

    private bool isAttacking;
    private GameObject foodBall;
    private TrailRenderer foodBallTrail;

    // --- Camera state ---
    private bool isCameraActive;   // true while we're driving the camera at all (framing or returning)
    private bool isShaking;        // true while the vibration/noise offset should be applied
    private float currentShakeStrength;
    private Vector3 cameraFramingPosition; // the "clean" (non-shaken) local position the camera is currently easing towards/holding
    private Vector3 cameraOriginalLocalPosition;
    private Quaternion cameraOriginalLocalRotation;
    private float cameraOriginalFOV;
    private Tween shakeStrengthTween;
    private Sequence cameraFramingSequence;
    private Sequence cameraReturnSequence;

    private void OnEnable()
    {
        GameManager.events.AddEvent<PlayerSpecialAttack>(GameEvents.EventType.OnSpecialAttackTrigger,
            ExecuteSpecialAttack);
    }

    public void ExecuteSpecialAttack(PlayerSpecialAttack playerSpecialAttack)
    {
        List<Transform> foods = playerSpecialAttack.availableFoods;

        if (isAttacking || foods == null || foods.Count == 0) return;

        isAttacking = true;

        CreateFoodBall(foods);
    }

    private void CreateFoodBall(List<Transform> foods)
    {
        foodBall = new GameObject("FoodBall");
        foodBall.transform.position = foodGatherPoint.position;

        // Start the camera reacting from the very first frame of the attack: continuous vibration
        // begins now and ramps in intensity, while the camera eases into a shot that frames both
        // the gathering food and the chef.
        BeginCameraSequence();

        List<Transform> validFoods = new List<Transform>();

        foreach (Transform food in foods)
        {
            if (food == null) continue;

            validFoods.Add(food);

            food.SetParent(foodBall.transform);

            // Move all food into the gathering point.
            food.DOMove(foodGatherPoint.position, gatherDuration).SetEase(Ease.InOutQuad).SetId(foodBall);
        }

        // Wait until all food has gathered.
        DOVirtual.DelayedCall(gatherDuration, () =>
        {
            if (foodBall == null) return;

            FormSphere(validFoods);
        }).SetId(foodBall);
    }

    private void FormSphere(List<Transform> foods)
    {
        int count = foods.Count;

        for (int i = 0; i < count; i++)
        {
            Transform food = foods[i];

            if (food == null) continue;

            Vector3 spherePosition = GetSpherePosition(i, count);

            food.localPosition = spherePosition;
            food.localRotation = Random.rotation;
        }

        // Start small.
        foodBall.transform.localScale = Vector3.one * 0.2f;

        // Grow into the giant food sphere. Camera vibration/framing is already running from BeginCameraSequence().
        foodBall.transform.DOScale(sphereRadius, chargeDuration).SetEase(Ease.OutBack).SetId(foodBall)
            .OnComplete(PlayAnticipationThenLaunch);
    }

    private void PlayAnticipationThenLaunch()
    {
        if (foodBall == null)
        {
            Cleanup();
            return;
        }

        Vector3 toChef = (chefTarget.position - foodGatherPoint.position).normalized;

        // Pull the sphere back and squash it, like drawing a slingshot, before it snaps forward.
        // Vibration keeps running through this beat, right up until the throw itself.
        Sequence anticipation = DOTween.Sequence().SetId(foodBall);
        anticipation.Join(foodBall.transform.DOMove(foodBall.transform.position - toChef * 0.6f, anticipationDuration)
            .SetEase(Ease.OutQuad));
        anticipation.Join(foodBall.transform.DOScale(sphereRadius * 0.85f, anticipationDuration)
            .SetEase(Ease.OutQuad));
        anticipation.OnComplete(LaunchFoodBall);
    }

    private Vector3 GetSpherePosition(int index, int count)
    {
        // Fibonacci sphere distribution.
        float phi = Mathf.PI * (3f - Mathf.Sqrt(5f));

        float y = 1f - (index / (float)(count - 1)) * 2f;
        float radius = Mathf.Sqrt(1f - y * y);

        float theta = phi * index;

        float x = Mathf.Cos(theta) * radius;
        float z = Mathf.Sin(theta) * radius;

        return new Vector3(x, y, z) * sphereRadius;
    }

    private void LaunchFoodBall()
    {
        if (foodBall == null || chefTarget == null)
        {
            Cleanup();
            return;
        }

        // This is "the start of the throw" - stop the vibration here. The camera keeps holding
        // its framing shot (steady, no shake) while the ball is in flight.
        StopShake();

        AttachTrail();

        Vector3 startPosition = foodBall.transform.position;
        Vector3 targetPosition = chefTarget.position +
                                 (chefTarget.position - foodGatherPoint.position).normalized * launchDistance;

        // Build a simple three-point arc: start, a raised midpoint, and the target.
        Vector3 midPoint = Vector3.Lerp(startPosition, targetPosition, 0.5f) + Vector3.up * arcHeight;
        Vector3[] path = { startPosition, midPoint, targetPosition };

        // Stretch along the throw as it leaves the anticipation pose.
        foodBall.transform.DOScale(sphereRadius, 0.15f).SetEase(Ease.OutQuad).SetId(foodBall);

        foodBall.transform.DOPath(path, launchDuration, PathType.CatmullRom)
            .SetEase(Ease.InQuad)
            .SetId(foodBall)
            .OnComplete(HitChef);

        // Tumble the whole ball as it flies for extra readability on the arc.
        foodBall.transform.DOBlendableLocalRotateBy(new Vector3(0f, 360f * arcSpins, 0f), launchDuration,
            RotateMode.FastBeyond360).SetEase(Ease.Linear).SetId(foodBall);
    }

    private void AttachTrail()
    {
        foodBallTrail = foodBall.AddComponent<TrailRenderer>();
        foodBallTrail.time = 0.25f;
        foodBallTrail.startWidth = sphereRadius * 0.6f;
        foodBallTrail.endWidth = 0f;
        foodBallTrail.minVertexDistance = 0.1f;
    }

    private void HitChef()
    {
        Debug.Log($"Food Special Attack hit Chef for {damage} damage!");

        // Squash the ball flat on impact for a satisfying punch, then let cleanup handle the rest.
        if (foodBall != null)
        {
            foodBall.transform.DOScale(new Vector3(sphereRadius * 1.3f, sphereRadius * 0.4f, sphereRadius * 1.3f), 0.08f)
                .SetEase(Ease.OutQuad).SetId(foodBall);
        }

        ImpactCameraShake();
        DoHitStop();

        // This is the end point of the attack: start easing the camera back to exactly where it
        // started right now, rather than waiting for the food ball to be destroyed. It runs
        // alongside the impact shake above, not after it.
        ReturnCameraToRest();

        GameManager.events.TriggerEvent(GameEvents.EventType.OnChecfGotAttacked, damage);
        StartCoroutine(RestartGame());

        // Give the squash a beat to read before the ball is destroyed.
        DOVirtual.DelayedCall(0.12f, Cleanup, false).SetId(foodBall);
    }

    private IEnumerator RestartGame()
    {
        yield return new WaitForSeconds(1f);
        GameManager.events.TriggerEvent(GameEvents.EventType.OnSpecialAttackCompleted);
    }

    private void DoHitStop()
    {
        if (hitStopDuration <= 0f) return;

        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        DOVirtual.DelayedCall(hitStopDuration, () => Time.timeScale = originalTimeScale, true);
    }

    // ---------------------------------------------------------------------
    // Camera: continuous vibration + framing/zoom
    // ---------------------------------------------------------------------

    /// <summary>
    /// Called at the very start of the attack. Captures the camera's resting pose, starts a
    /// continuous (non-stepped) vibration that ramps in intensity, and eases the camera into a
    /// shot that keeps both the food ball and the chef in frame, with a slow zoom.
    /// </summary>
    private void BeginCameraSequence()
    {
        if (cameraTransform == null) return;

        cameraOriginalLocalPosition = cameraTransform.localPosition;
        cameraOriginalLocalRotation = cameraTransform.localRotation;
        if (targetCamera != null) cameraOriginalFOV = targetCamera.fieldOfView;

        cameraFramingPosition = cameraOriginalLocalPosition;

        // Kill any leftover camera tweens from a previous attack (tagged with `this`, not `foodBall`,
        // so Cleanup()'s DOTween.Kill(foodBall) never touches these).
        DOTween.Kill(this);

        // Compute a framing shot: pull back along the camera's current facing direction and lift up
        // slightly, then look at the midpoint between the gathering food and the chef so both stay in view.
        Vector3 midPoint = Vector3.Lerp(foodGatherPoint.position, chefTarget.position, 0.5f);
        Vector3 targetWorldPosition = cameraTransform.position
                                       - cameraTransform.forward * framingBackDistance
                                       + Vector3.up * framingHeight;
        Vector3 targetLocalPosition = cameraTransform.parent != null
            ? cameraTransform.parent.InverseTransformPoint(targetWorldPosition)
            : targetWorldPosition;

        cameraFramingSequence = DOTween.Sequence().SetId(this);
        cameraFramingSequence.Join(DOTween.To(() => cameraFramingPosition, x => cameraFramingPosition = x,
            targetLocalPosition, framingDuration).SetEase(Ease.OutSine));
        cameraFramingSequence.Join(cameraTransform.DOLookAt(midPoint, framingDuration).SetEase(Ease.OutSine));

        if (targetCamera != null)
        {
            // Slow zoom that continues through the charge, not just the initial framing snap.
            targetCamera.DOFieldOfView(zoomFOV, framingDuration + chargeDuration)
                .SetEase(Ease.InOutSine).SetId(this);
        }

        // Continuous vibration: a single smooth ramp from start to end strength across the
        // gather + charge window, sampled every frame in Update() via Perlin noise (no discrete steps/pulses).
        isCameraActive = true;
        isShaking = true;
        currentShakeStrength = shakeStartStrength;

        float rampDuration = gatherDuration + chargeDuration;
        shakeStrengthTween = DOTween.To(() => currentShakeStrength, x => currentShakeStrength = x,
            shakeEndStrength, rampDuration).SetEase(Ease.InSine).SetId(this);
    }

    /// <summary>Stops the continuous vibration. Called right as the throw begins.</summary>
    private void StopShake()
    {
        isShaking = false;
        shakeStrengthTween?.Kill();
        currentShakeStrength = 0f;
    }

    /// <summary>A short, separate burst of vibration on impact, using the same continuous system.</summary>
    private void ImpactCameraShake()
    {
        if (cameraTransform == null) return;

        isShaking = true;
        currentShakeStrength = impactShakeStrength;

        shakeStrengthTween?.Kill();
        shakeStrengthTween = DOTween.To(() => currentShakeStrength, x => currentShakeStrength = x, 0f,
                impactShakeDuration)
            .SetEase(Ease.OutSine).SetId(this)
            .OnComplete(() => isShaking = false);
    }

    /// <summary>Eases the camera back to exactly where it started, ending the sequence.</summary>
    private void ReturnCameraToRest()
    {
        if (cameraTransform == null)
        {
            isCameraActive = false;
            return;
        }

        // Note: deliberately NOT calling StopShake() here. If an impact shake is currently
        // playing, let it keep running on top of the return motion instead of cutting it off -
        // the camera should glide back while still jolting from the hit.
        cameraFramingSequence?.Kill();
        cameraReturnSequence?.Kill();

        cameraReturnSequence = DOTween.Sequence().SetId(this);
        cameraReturnSequence.Join(DOTween.To(() => cameraFramingPosition, x => cameraFramingPosition = x,
            cameraOriginalLocalPosition, returnDuration).SetEase(Ease.InOutSine));
        cameraReturnSequence.Join(cameraTransform.DOLocalRotateQuaternion(cameraOriginalLocalRotation, returnDuration)
            .SetEase(Ease.InOutSine));

        if (targetCamera != null)
        {
            cameraReturnSequence.Join(targetCamera.DOFieldOfView(cameraOriginalFOV, returnDuration)
                .SetEase(Ease.InOutSine));
        }

        cameraReturnSequence.OnComplete(() => isCameraActive = false);
    }

    private void Update()
    {
        if (!isCameraActive || cameraTransform == null) return;

        Vector3 shakeOffset = Vector3.zero;

        if (isShaking)
        {
            // Perlin noise sampled continuously (rather than discrete DOShakePosition pulses) so the
            // vibration reads as one unbroken tremor whose amplitude just happens to be ramping.
            float t = Time.time * shakeNoiseFrequency;
            float noiseX = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f;
            float noiseZ = (Mathf.PerlinNoise(t, t) - 0.5f) * 2f;

            shakeOffset = new Vector3(noiseX, noiseY, noiseZ) * currentShakeStrength;
        }

        cameraTransform.localPosition = cameraFramingPosition + shakeOffset;
    }

    private void Cleanup()
    {
        if (foodBall != null)
        {
            DOTween.Kill(foodBall);
            Destroy(foodBall);
        }

        foodBallTrail = null;
        foodBall = null;
        isAttacking = false;

        // Camera return was already started in HitChef(), at the actual end point of the attack -
        // nothing camera-related to do here.
    }

    private void OnDisable()
    {
        GameManager.events.RemoveEvent<PlayerSpecialAttack>(GameEvents.EventType.OnSpecialAttackTrigger,
            ExecuteSpecialAttack);
    }

    private void OnDestroy()
    {
        if (foodBall != null)
        {
            DOTween.Kill(foodBall);
        }

        DOTween.Kill(this);
    }
}