using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class FoodSpawner : MonoBehaviour
{
    [SerializeField] private SnakeBodyController snakeBodyController;
    [SerializeField] private List<SnakeSegment> foodPrefabs;

    [Header("Balancing")]
    [SerializeField] private int foodPerTypeCount = 3; // how many of EACH type to spawn initially
    [SerializeField] private int minFoodPerType = 2;    // never let a type's live count drop below this

    [SerializeField] private Vector2 arenaSize;
    [SerializeField] private Renderer spawnArea;
    
    [Header("Highlight Settings")]
    [SerializeField] private float highlightScaleMultiplier = 1.3f;
    [SerializeField] private float highlightDuration = 0.5f;
    [SerializeField] private Color highlightColor = Color.yellow;
    
    private bool canHighlight = true;

    private List<GameObject> spawnedFoods = new List<GameObject>();
    private List<SnakeSegment> spawnedFoodSegments = new List<SnakeSegment>();

    // Maps each spawned food instance back to the prefab it came from
    private Dictionary<GameObject, SnakeSegment> spawnedFoodTypes = new Dictionary<GameObject, SnakeSegment>();

    // Tracks active highlight tweens + original state so we can revert cleanly
    private Dictionary<GameObject, Tween> activeHighlightTweens = new Dictionary<GameObject, Tween>();
    private Dictionary<GameObject, Vector3> originalScales = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();

    #region Initialization

    private void Awake()
    {
        canHighlight = true;
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent<SnakeBodyController>(GameEvents.EventType.OnSnakeInitialized, SpawnInitialFoods);
        GameManager.events.AddEvent<ChaosType>(GameEvents.EventType.OnPowerUpSelected, OnPowerUpSelected);
        GameManager.events.AddEvent<SnakeSegment>(GameEvents.EventType.OnAteFood, RemoveFood);
        GameManager.events.AddEvent(GameEvents.EventType.OnLengthZero, OnLengthZero);
        GameManager.events.AddEvent(GameEvents.EventType.OnSpecialAttackCompleted, ResetFoods);
    }

    #endregion

    #region FoodSpawning

    private void SpawnInitialFoods(SnakeBodyController snake)
    {
        // Spawn a fixed number of EACH available food type instead of picking randomly.
        foreach (SnakeSegment prefab in foodPrefabs)
        {
            for (int i = 0; i < foodPerTypeCount; i++)
            {
                SpawnFoodOfType(prefab);
            }
        }
        
        if(snake != null && canHighlight) HighlightFoodsOfType(snake);
        canHighlight = false;
    }

    public void SpawnRandomFood()
    {
        // Prefer replenishing whichever type has fallen below the minimum.
        SnakeSegment prefabToSpawn = GetPrefabNeedingReplenish();

        if (prefabToSpawn == null)
        {
            // Every type already meets the minimum, so just spawn a random one.
            prefabToSpawn = foodPrefabs[Random.Range(0, foodPrefabs.Count)];
        }

        SpawnFoodOfType(prefabToSpawn);
    }

    // Instantiates a food of a given prefab/type and registers it in the tracking lists.
    private SnakeSegment SpawnFoodOfType(SnakeSegment prefab)
    {
        SnakeSegment food = Instantiate(
            prefab,
            GetRandomSpawnPosition(),
            Quaternion.identity,
            transform);

        spawnedFoods.Add(food.gameObject);
        spawnedFoodSegments.Add(food);

        return food;
    }

    // Returns the prefab for the type that is currently most under the minimum count,
    // or null if every type already meets/exceeds the minimum.
    private SnakeSegment GetPrefabNeedingReplenish()
    {
        Dictionary<FoodType, int> counts = GetFoodTypeCounts();

        SnakeSegment neediestPrefab = null;
        int lowestCount = int.MaxValue;

        foreach (SnakeSegment prefab in foodPrefabs)
        {
            counts.TryGetValue(prefab.FoodType, out int currentCount);

            if (currentCount < minFoodPerType && currentCount < lowestCount)
            {
                lowestCount = currentCount;
                neediestPrefab = prefab;
            }
        }

        return neediestPrefab;
    }

    // Counts how many of each FoodType are currently alive in the scene.
    private Dictionary<FoodType, int> GetFoodTypeCounts()
    {
        Dictionary<FoodType, int> counts = new Dictionary<FoodType, int>();

        foreach (SnakeSegment segment in spawnedFoodSegments)
        {
            if (segment == null) continue; // destroyed food, ignore

            if (counts.ContainsKey(segment.FoodType))
                counts[segment.FoodType]++;
            else
                counts[segment.FoodType] = 1;
        }

        return counts;
    }

    #endregion

    #region Remove Food

    private void OnLengthZero()
    {
        PlayerSpecialAttack playerSpecialAttack = new PlayerSpecialAttack();
        playerSpecialAttack.availableFoods = new List<Transform>();
        foreach (SnakeSegment segment in spawnedFoodSegments)
        {
            if(segment != null)
                playerSpecialAttack.availableFoods.Add(segment.transform);
        }
        GameManager.events.TriggerEvent(GameEvents.EventType.OnSpecialAttackTrigger, playerSpecialAttack);
    }

    private void RemoveFood(SnakeSegment food)
    {
        spawnedFoods.Remove(food.gameObject);
        spawnedFoodSegments.Remove(food); // keep counts accurate for balancing
        
        SpawnRandomFood();
    }

    public void RemoveFoods()
    {
        foreach (GameObject food in spawnedFoods)
        {
            Destroy(food);
        }
        spawnedFoods.Clear();
        spawnedFoodSegments.Clear();
    }

    public void ResetFoods()
    {
        RemoveFoods();
        SpawnInitialFoods(null);
    }

    #endregion

    #region Power Ups

    private void OnPowerUpSelected(ChaosType chaosType)
    {
        switch (chaosType)
        {
            case ChaosType.ChangeItems:
                ResetFoods();
                break;
        }
    }

    #endregion

    #region Helpers

     private Vector3 GetRandomSpawnPosition()
    {
        Bounds bounds = spawnArea.bounds;

        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.center.y,
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }
    
    bool hasTargetFood = false;
     public void HighlightFoodsOfType(SnakeBodyController snake)
    {
        StopHighlights(); // clear any previous highlight state first
        Debug.Log(snake.TargetFood.ToString());
        if (spawnedFoodSegments.Count == 0)
        {
            Debug.Log(spawnedFoods.Count);
            return;
        }
        Debug.Log("Highlighted food type: " + snake.TargetFood);

        foreach (SnakeSegment seg in spawnedFoodSegments)
        {
            if (seg.FoodType == snake.TargetFood)
            {
                hasTargetFood = true;
                seg.EnableArrowObject();
                Debug.Log("Highlighted food type: " + snake.TargetFood);
                HighlightFood(seg.gameObject);
            }
        }
    }

    private void HighlightFood(GameObject food)
    {
        if (food == null) return;

        Transform t = food.transform;

        originalScales[food] = t.localScale;

        Sequence seq = DOTween.Sequence();
        seq.Append(t.DOScale(t.localScale * highlightScaleMultiplier, highlightDuration).SetEase(Ease.InOutSine));
        seq.Append(t.DOScale(t.localScale, highlightDuration).SetEase(Ease.InOutSine));
        seq.SetLoops(-1);
        seq.SetTarget(t);

        activeHighlightTweens[food] = seq;

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

    public void StopHighlights()
    {
        foreach (var kvp in activeHighlightTweens)
        {
            GameObject food = kvp.Key;
            kvp.Value?.Kill();

            if (food == null) continue;
            
            SnakeSegment segment = food.GetComponent<SnakeSegment>();
            if (segment != null)
            {
                segment.DisableArrowObject();
            }

            if (originalScales.TryGetValue(food, out Vector3 scale))
                food.transform.localScale = scale;

            Renderer rend = food.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                DOTween.Kill(rend.material);
                if (originalColors.TryGetValue(food, out Color color))
                    rend.material.color = color;
            }
        }

        activeHighlightTweens.Clear();
        originalScales.Clear();
        originalColors.Clear();
    }

    #endregion

    #region Terminate

    private void OnDestroy()
    {
        StopHighlights();
    }
    
    private void OnDisable()
    {
        GameManager.events.RemoveEvent<SnakeBodyController>(GameEvents.EventType.OnSnakeInitialized, SpawnInitialFoods);
        GameManager.events.RemoveEvent<ChaosType>(GameEvents.EventType.OnPowerUpSelected, OnPowerUpSelected);
        GameManager.events.RemoveEvent<SnakeSegment>(GameEvents.EventType.OnAteFood, RemoveFood);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnLengthZero, OnLengthZero);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSpecialAttackCompleted, ResetFoods);
    }

    #endregion
}