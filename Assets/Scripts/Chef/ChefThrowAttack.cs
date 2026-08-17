using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ChefThrowAttack : MonoBehaviour, IChefAttack
{
    private Animator animator;

    [Header("Throw Settings")] [SerializeField]
    private int amountToThrow = 3;

    [Header("Food Objects")] [SerializeField]
    private List<GameObject> foodObjects;

    [Header("Spawn Area")] [SerializeField]
    private Transform spawnArea;

    [Header("Spawn Particles")] [SerializeField]
    private List<ParticleSystem> spawnParticles;

    [Header("Throw Animation")] [SerializeField]
    private float throwDuration = 0.5f;

    [SerializeField] private float throwHeight = 2f;
    [SerializeField] private Ease throwEase = Ease.OutQuad;

    [Header("Juice - Food Spawn")] [SerializeField]
    private float spawnPunchScale = 0.3f;

    [SerializeField] private float spawnPunchDuration = 0.3f;
    [SerializeField] private int spawnPunchVibrato = 8;
    [SerializeField] private float spawnPunchElasticity = 0.8f;

    [Header("Juice - Landing Squash")] [SerializeField]
    private float landingSquashDuration = 0.1f;

    [SerializeField] private float landingRecoverDuration = 0.25f;
    [SerializeField] private float landingSquashFlatten = 0.6f;
    [SerializeField] private float landingSquashWiden = 1.3f;

    [Header("Juice - Disable")] [SerializeField]
    private float disableScaleDuration = 0.2f;

    [Header("Juice - Particles")] [SerializeField]
    private float particlePopInDuration = 0.3f;

    [SerializeField] private float particlePopOutDuration = 0.2f;

    private Coroutine throwRoutine;

    private Dictionary<GameObject, Vector3> foodOriginalScales;
    private Dictionary<ParticleSystem, Vector3> particleOriginalScales;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        foodOriginalScales = new Dictionary<GameObject, Vector3>();
        foreach (GameObject food in foodObjects)
        {
            foodOriginalScales[food] = food.transform.localScale;
        }

        particleOriginalScales = new Dictionary<ParticleSystem, Vector3>();
        foreach (ParticleSystem particle in spawnParticles)
        {
            particleOriginalScales[particle] = particle.transform.localScale;
        }
    }

    private void Start()
    {
        DisableAllFood();
        DisableAllParticles();
    }

    public void StartAttack()
    {
        StartCoroutine(ThrowFoodRoutine());
    }

    private IEnumerator ThrowFoodRoutine()
    {
        List<GameObject> foodsToThrow = new List<GameObject>();
        List<ParticleSystem> particlesToUse = new List<ParticleSystem>();
        List<Vector3> targetPositions = new List<Vector3>();

        // -----------------------------------------
        // 1. Prepare all 3 foods and positions
        // -----------------------------------------

        foodsToThrow.Clear();
        particlesToUse.Clear();
        targetPositions.Clear();

        for (int i = 0; i < amountToThrow; i++)
        {
            GameObject food = GetRandomInactiveFood(foodsToThrow);

            if (food == null)
            {
                Debug.LogWarning("Chef: Not enough inactive food objects.");
                break;
            }

            ParticleSystem particle = GetRandomParticle(particlesToUse);

            if (particle == null)
            {
                Debug.LogWarning("Chef: Not enough inactive particle objects.");
                break;
            }

            Vector3 targetPosition = GetRandomPositionOnPlane();

            foodsToThrow.Add(food);
            particlesToUse.Add(particle);
            targetPositions.Add(targetPosition);
        }

        // -----------------------------------------
        // 2. Spawn ALL particles at the same time (pop-in juice)
        // -----------------------------------------

        for (int i = 0; i < particlesToUse.Count; i++)
        {
            ParticleSystem particle = particlesToUse[i];

            particle.transform.position = targetPositions[i];

            EnableParticleWithJuice(particle);
        }

        // -----------------------------------------
        // 3. Wait 1 second
        // -----------------------------------------

        yield return new WaitForSeconds(1f);

        // -----------------------------------------
        // 4. Throw all foods with 0.5 sec delay (spawn punch + landing squash juice)
        // -----------------------------------------

        for (int i = 0; i < foodsToThrow.Count; i++)
        {
            animator.SetTrigger("Shoot");
            ThrowObject(foodsToThrow[i].transform, targetPositions[i]);

            yield return new WaitForSeconds(0.5f);
        }

        // -----------------------------------------
        // 5. Keep everything for 3 seconds
        // -----------------------------------------

        yield return new WaitForSeconds(3f);

        // -----------------------------------------
        // 6. Disable ALL foods (shrink-out juice)
        // -----------------------------------------

        for (int i = 0; i < foodsToThrow.Count; i++)
        {
            DisableFoodWithJuice(foodsToThrow[i]);
        }

        // -----------------------------------------
        // 7. Disable ALL particles (shrink-out juice)
        // -----------------------------------------

        for (int i = 0; i < particlesToUse.Count; i++)
        {
            DisableParticleWithJuice(particlesToUse[i]);
        }

        // Loop starts again
        GameManager.events.TriggerEvent(GameEvents.EventType.OnAttackFinished);
    }

    private void ThrowObject(Transform food, Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;

        Vector3 midPoint = Vector3.Lerp(startPosition, targetPosition, 0.5f);

        midPoint.y += throwHeight;

        Vector3[] path = { startPosition, midPoint, targetPosition };

        Vector3 baseScale = foodOriginalScales.TryGetValue(food.gameObject, out Vector3 s) ? s : food.localScale;

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

    private void DisableFoodWithJuice(GameObject food)
    {
        Transform t = food.transform;
        Vector3 baseScale = foodOriginalScales.TryGetValue(food, out Vector3 s) ? s : t.localScale;

        t.DOKill();
        t.DOScale(Vector3.zero, disableScaleDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                food.SetActive(false);
                t.localScale = baseScale;
            });
    }

    private void EnableParticleWithJuice(ParticleSystem particle)
    {
        Transform t = particle.transform;
        Vector3 baseScale = particleOriginalScales.TryGetValue(particle, out Vector3 s) ? s : t.localScale;

        t.DOKill();
        t.localScale = Vector3.zero;
        particle.gameObject.SetActive(true);
        t.DOScale(baseScale, particlePopInDuration).SetEase(Ease.OutBack);
        particle.Play();
    }

    private void DisableParticleWithJuice(ParticleSystem particle)
    {
        Transform t = particle.transform;
        Vector3 baseScale = particleOriginalScales.TryGetValue(particle, out Vector3 s) ? s : t.localScale;

        t.DOKill();
        t.DOScale(Vector3.zero, particlePopOutDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                particle.Stop();
                particle.gameObject.SetActive(false);
                t.localScale = baseScale;
            });
    }

    private GameObject GetRandomInactiveFood(List<GameObject> alreadySelected)
    {
        List<GameObject> availableFoods = new List<GameObject>();

        foreach (GameObject food in foodObjects)
        {
            if (!food.activeSelf && !alreadySelected.Contains(food))
            {
                availableFoods.Add(food);
            }
        }

        if (availableFoods.Count == 0) return null;

        return availableFoods[Random.Range(0, availableFoods.Count)];
    }

    private ParticleSystem GetRandomParticle(List<ParticleSystem> alreadySelected)
    {
        List<ParticleSystem> availableParticles = new List<ParticleSystem>();

        foreach (ParticleSystem particle in spawnParticles)
        {
            if (!particle.gameObject.activeSelf && !alreadySelected.Contains(particle))
            {
                availableParticles.Add(particle);
            }
        }

        if (availableParticles.Count == 0) return null;

        return availableParticles[Random.Range(0, availableParticles.Count)];
    }

    private Vector3 GetRandomPositionOnPlane()
    {
        Renderer renderer = spawnArea.GetComponent<Renderer>();

        Bounds bounds = renderer.bounds;

        float x = Random.Range(bounds.min.x, bounds.max.x);

        float z = Random.Range(bounds.min.z, bounds.max.z);

        return new Vector3(x, bounds.max.y, z);
    }

    private void DisableAllFood()
    {
        foreach (GameObject food in foodObjects)
        {
            food.transform.DOKill();
            food.SetActive(false);
            food.transform.localPosition = Vector3.zero;

            if (foodOriginalScales.TryGetValue(food, out Vector3 s))
            {
                food.transform.localScale = s;
            }
        }
    }

    private void DisableAllParticles()
    {
        foreach (ParticleSystem particle in spawnParticles)
        {
            particle.transform.DOKill();
            particle.gameObject.SetActive(false);
            particle.transform.localPosition = Vector3.zero;

            if (particleOriginalScales.TryGetValue(particle, out Vector3 s))
            {
                particle.transform.localScale = s;
            }
        }
    }
}