using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class FoodSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private List<SnakeSegment> foodPrefabs;
    [SerializeField] private Renderer spawnArea;

    [Header("Balancing")]
    [SerializeField] private int foodPerTypeCount = 3; // how many of EACH type to spawn initially
    [SerializeField] private int minFoodPerType = 2;   // never let a type's live count drop below this

    [Header("Highlight Settings")]
    [SerializeField] private float highlightScaleMultiplier = 1.3f;
    [SerializeField] private float highlightDuration = 0.5f;
    [SerializeField] private Color highlightColor = Color.yellow;

    private readonly List<SnakeSegment> spawnedFoods = new List<SnakeSegment>();

    // Highlight state, so each food can be reverted cleanly
    private readonly Dictionary<SnakeSegment, Tween> scaleTweens = new Dictionary<SnakeSegment, Tween>();
    private readonly Dictionary<SnakeSegment, Vector3> originalScales = new Dictionary<SnakeSegment, Vector3>();
    private readonly Dictionary<SnakeSegment, Color> originalColors = new Dictionary<SnakeSegment, Color>();

    private FoodType currentTarget;
    private bool hasSpawnedInitialFoods;

    #region Initialization

    private void OnEnable()
    {
        GameManager.events.AddEvent<FoodType>(GameEvents.EventType.OnSnakeInitialized, OnSnakeInitialized);
        GameManager.events.AddEvent<FoodType>(GameEvents.EventType.OnTargetFoodChanged, OnTargetChanged);
        GameManager.events.AddEvent<SnakeSegment>(GameEvents.EventType.OnAteFood, RemoveFood);
        GameManager.events.AddEvent<ChaosType>(GameEvents.EventType.OnPowerUpSelected, OnPowerUpSelected);
        GameManager.events.AddEvent(GameEvents.EventType.OnLengthZero, OnLengthZero);
        GameManager.events.AddEvent(GameEvents.EventType.OnSpecialAttackCompleted, ResetFoods);
    }

    private void OnDisable()
    {
        GameManager.events.RemoveEvent<FoodType>(GameEvents.EventType.OnSnakeInitialized, OnSnakeInitialized);
        GameManager.events.RemoveEvent<FoodType>(GameEvents.EventType.OnTargetFoodChanged, OnTargetChanged);
        GameManager.events.RemoveEvent<SnakeSegment>(GameEvents.EventType.OnAteFood, RemoveFood);
        GameManager.events.RemoveEvent<ChaosType>(GameEvents.EventType.OnPowerUpSelected, OnPowerUpSelected);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnLengthZero, OnLengthZero);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSpecialAttackCompleted, ResetFoods);
    }

    private void OnDestroy()
    {
        StopHighlights();
    }

    #endregion

    #region Event Handlers

    private void OnSnakeInitialized(FoodType target)
    {
        currentTarget = target;

        // This event also fires after a special attack, but ResetFoods already
        // handles respawning then. Only spawn the initial set once.
        if (!hasSpawnedInitialFoods)
        {
            hasSpawnedInitialFoods = true;
            SpawnInitialFoods();
        }
        else
        {
            HighlightFoodsOfType(currentTarget);
        }
    }

    private void OnTargetChanged(FoodType target)
    {
        currentTarget = target;
        HighlightFoodsOfType(target);
    }

    private void OnPowerUpSelected(ChaosType chaosType)
    {
        if (chaosType == ChaosType.ChangeItems)
            ResetFoods();
    }

    private void OnLengthZero()
    {
        PlayerSpecialAttack attack = new PlayerSpecialAttack();
        attack.availableFoods = new List<Transform>();

        foreach (SnakeSegment food in spawnedFoods)
        {
            if (food != null)
                attack.availableFoods.Add(food.transform);
        }

        GameManager.events.TriggerEvent(GameEvents.EventType.OnSpecialAttackTrigger, attack);
    }

    #endregion

    #region Spawning

    private void SpawnInitialFoods()
    {
        foreach (SnakeSegment prefab in foodPrefabs)
            for (int i = 0; i < foodPerTypeCount; i++)
                SpawnFoodOfType(prefab);

        HighlightFoodsOfType(currentTarget);
    }

    private void SpawnRandomFood()
    {
        // Prefer the type most under the minimum, otherwise pick any type.
        SnakeSegment prefab = GetPrefabNeedingReplenish()
                              ?? foodPrefabs[Random.Range(0, foodPrefabs.Count)];

        SnakeSegment food = SpawnFoodOfType(prefab);

        // If a new food matches the current target, it should be highlighted too.
        if (food.FoodType == currentTarget)
            HighlightFood(food);
    }

    private SnakeSegment SpawnFoodOfType(SnakeSegment prefab)
    {
        SnakeSegment food = Instantiate(prefab, GetRandomSpawnPosition(), Quaternion.identity, transform);
        spawnedFoods.Add(food);
        return food;
    }

    private SnakeSegment GetPrefabNeedingReplenish()
    {
        Dictionary<FoodType, int> counts = GetFoodTypeCounts();

        SnakeSegment neediest = null;
        int lowest = int.MaxValue;

        foreach (SnakeSegment prefab in foodPrefabs)
        {
            counts.TryGetValue(prefab.FoodType, out int count);

            if (count < minFoodPerType && count < lowest)
            {
                lowest = count;
                neediest = prefab;
            }
        }

        return neediest;
    }

    private Dictionary<FoodType, int> GetFoodTypeCounts()
    {
        Dictionary<FoodType, int> counts = new Dictionary<FoodType, int>();

        foreach (SnakeSegment food in spawnedFoods)
        {
            if (food == null) continue;

            counts.TryGetValue(food.FoodType, out int count);
            counts[food.FoodType] = count + 1;
        }

        return counts;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Bounds bounds = spawnArea.bounds;

        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.center.y,
            Random.Range(bounds.min.z, bounds.max.z));
    }

    #endregion

    #region Removing

    private void RemoveFood(SnakeSegment food)
    {
        if (food == null) return;

        StopHighlightFor(food);
        spawnedFoods.Remove(food);
        Destroy(food.gameObject);

        SpawnRandomFood();
    }

    private void RemoveAllFoods()
    {
        StopHighlights(); // kill tweens before the objects are destroyed

        foreach (SnakeSegment food in spawnedFoods)
        {
            if (food != null)
                Destroy(food.gameObject);
        }

        spawnedFoods.Clear();
    }

    private void ResetFoods()
    {
        RemoveAllFoods();
        SpawnInitialFoods();
    }

    #endregion

    #region Highlight

    private void HighlightFoodsOfType(FoodType target)
    {
        StopHighlights();

        foreach (SnakeSegment food in spawnedFoods)
        {
            if (food != null && food.FoodType == target)
                HighlightFood(food);
        }
    }

    private void HighlightFood(SnakeSegment food)
    {
        if (food == null || scaleTweens.ContainsKey(food)) return;

        Transform t = food.transform;
        Vector3 baseScale = t.localScale;
        originalScales[food] = baseScale;

        food.EnableArrowObject();

        Sequence seq = DOTween.Sequence();
        seq.Append(t.DOScale(baseScale * highlightScaleMultiplier, highlightDuration).SetEase(Ease.InOutSine));
        seq.Append(t.DOScale(baseScale, highlightDuration).SetEase(Ease.InOutSine));
        seq.SetLoops(-1);
        seq.SetTarget(t);
        scaleTweens[food] = seq;

        Renderer rend = food.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            originalColors[food] = rend.material.color;
            rend.material
                .DOColor(highlightColor, highlightDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetTarget(rend.material);
        }
    }

    private void StopHighlights()
    {
        // Copy the keys because StopHighlightFor modifies the dictionaries.
        foreach (SnakeSegment food in new List<SnakeSegment>(scaleTweens.Keys))
            StopHighlightFor(food);

        scaleTweens.Clear();
        originalScales.Clear();
        originalColors.Clear();
    }

    private void StopHighlightFor(SnakeSegment food)
    {
        if (food == null) return;

        if (scaleTweens.TryGetValue(food, out Tween tween))
        {
            tween.Kill();
            scaleTweens.Remove(food);
        }

        food.DisableArrowObject();

        if (originalScales.TryGetValue(food, out Vector3 scale))
        {
            food.transform.localScale = scale;
            originalScales.Remove(food);
        }

        Renderer rend = food.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            DOTween.Kill(rend.material);

            if (originalColors.TryGetValue(food, out Color color))
            {
                rend.material.color = color;
                originalColors.Remove(food);
            }
        }
    }

    #endregion
}