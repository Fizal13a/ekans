using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class FoodSpawner : MonoBehaviour
{
    [SerializeField] private SnakeBodyController snakeBodyController;
    [SerializeField] private List<SnakeSegment> foodPrefabs;

    [SerializeField] private int foodCount = 40;

    [SerializeField] private Vector2 arenaSize;
    [SerializeField] private Renderer spawnArea;
    
    [Header("Highlight Settings")]
    [SerializeField] private float highlightScaleMultiplier = 1.3f;
    [SerializeField] private float highlightDuration = 0.5f;
    [SerializeField] private Color highlightColor = Color.yellow;

    private List<GameObject> spawnedFoods = new List<GameObject>();
    private List<SnakeSegment> spawnedFoodSegments = new List<SnakeSegment>();

    // Maps each spawned food instance back to the prefab it came from
    private Dictionary<GameObject, SnakeSegment> spawnedFoodTypes = new Dictionary<GameObject, SnakeSegment>();

    // Tracks active highlight tweens + original state so we can revert cleanly
    private Dictionary<GameObject, Tween> activeHighlightTweens = new Dictionary<GameObject, Tween>();
    private Dictionary<GameObject, Vector3> originalScales = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();

    private void Awake()
    {
        SpawnInitialFoods();
    }

    private void SpawnInitialFoods()
    {
        for (int i = 0; i < foodCount; i++)
        {
            int randomIndex = Random.Range(0, foodPrefabs.Count);

            SnakeSegment food = Instantiate(
                foodPrefabs[randomIndex],
                GetRandomSpawnPosition(),
                Quaternion.identity,
                transform);
            
            spawnedFoods.Add(food.gameObject);
            spawnedFoodSegments.Add(food);
        }
    }

    public void SpawnRandomFood()
    {
        Debug.Log("Spawning food on random position");
        FoodType foodType = snakeBodyController.GetTargetFood();

        for (int i = 0; i < foodPrefabs.Count; i++)
        {
            if (foodPrefabs[i].FoodType == foodType)
            {
                SnakeSegment food = Instantiate(
                    foodPrefabs[i],
                    GetRandomSpawnPosition(),
                    Quaternion.identity,
                    transform);
            
                spawnedFoods.Add(food.gameObject);
                spawnedFoodSegments.Add(food);
                
                return;
            }
        }
     
    }

    public void RemoveFoods()
    {
        foreach (GameObject food in spawnedFoods)
        {
            Destroy(food);
        }
        spawnedFoods.Clear();
    }

    public void ResetFoods()
    {
        RemoveFoods();
        SpawnInitialFoods();
    }
    
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
     public void HighlightFoodsOfType(FoodType foodType)
    {
        StopHighlights(); // clear any previous highlight state first

        if (spawnedFoodSegments.Count == 0) return;
        Debug.Log("Highlighted food type: " + foodType);

        foreach (SnakeSegment seg in spawnedFoodSegments)
        {
            if (seg.FoodType == foodType)
            {
                hasTargetFood = true;
                seg.EnableArrowObject();
                Debug.Log("Highlighted food type: " + foodType);
                HighlightFood(seg.gameObject);
            }
        }

        if (!hasTargetFood)
        {
            SpawnRandomFood();
            HighlightFoodsOfType(snakeBodyController.GetTargetFood());
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

    private void OnDestroy()
    {
        StopHighlights();
    }
}
