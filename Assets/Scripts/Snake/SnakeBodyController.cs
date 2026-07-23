using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class SnakeBodyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private FoodSpawner foodSpawner;
    [SerializeField] private SnakeHeadController snakeHeadController;
    [SerializeField] private UIManager uimanager;
    [SerializeField] private Transform head;
    [SerializeField] private Transform bodyRoot;

    [Header("Starting Snake")]
    [SerializeField] private List<SnakeSegment> availableFoodSegments;
    [SerializeField] private int startingLength = 5;
    
    [Header("Body Limit")]
    [SerializeField] private int maxBodyLength = 50;
    public int MaxBodyLength => maxBodyLength;

    [Header("Movement")]
    [SerializeField] private float followSpeed = 2f;
    [SerializeField] private float recordDistance = 0.2f;
    [SerializeField] private int pointsPerSegment = 5;

    [Header("Polish / Juice")]
    [SerializeField] private float segmentSpawnDuration = 0.35f;
    [SerializeField] private Ease segmentSpawnEase = Ease.OutBack;
    [SerializeField] private float segmentRemoveDuration = 0.22f;
    [SerializeField] private Ease segmentRemoveEase = Ease.InBack;
    [SerializeField] private float eatPunchScale = 0.15f;
    [SerializeField] private float eatPunchDuration = 0.2f;
    [SerializeField] private float levelUpPunchScale = 0.35f;
    [SerializeField] private float levelUpPunchDuration = 0.45f;
    
    [Header("Game Over")]
    [SerializeField] private float segmentDetachStagger = 0.05f;
    [SerializeField] private float segmentFallDuration = 0.6f;
    [SerializeField] private float segmentScatterRadius = 1.2f;
    [SerializeField] private Ease segmentFallEase = Ease.OutBounce;

    public Vector2 newSegmentSpawnRange = new Vector2(0, 0);
    private float spawnInterval;
    Coroutine CheckAndSpawnSegmentsRoutine;
    bool canAddSegments;
    
    public int Length => segments.Count;

    private readonly List<Vector3> path = new();
    private readonly List<SnakeSegment> segments = new();

    private Vector3 lastRecordedPosition;

    private void Start()
    {
        lastRecordedPosition = head.position;

        // Create an initial straight path behind the player
        for (int i = 0; i < 300; i++)
        {
            path.Add(head.position - head.forward * i * recordDistance);
        }

        canAddSegments = true;
        SpawnStartingSnake();
        UpdateBodyLimitUI();
        CheckAndSpawnSegmentsRoutine =  StartCoroutine(CheckAndSpawnSegments());
    }

    IEnumerator CheckAndSpawnSegments()
    {
        while (canAddSegments)
        {
            spawnInterval = Random.Range(newSegmentSpawnRange.x, newSegmentSpawnRange.y);

            while (spawnInterval > 0f)
            {
                spawnInterval -= Time.deltaTime;
                yield return null;
            }
        
            int randomIndex = Random.Range(0, availableFoodSegments.Count);
            AddSegment(availableFoodSegments[randomIndex]);
            FTUEController.Instance.OnFoodAdded();
        }
    }

    private void LateUpdate()
    {
        UpdatePath();
        UpdateBody();
    }

    void UpdatePath()
    {
        if (Vector3.Distance(lastRecordedPosition, head.position) < recordDistance)
            return;

        lastRecordedPosition = head.position;

        path.Insert(0, head.position);

        if (path.Count > 1000)
            path.RemoveAt(path.Count - 1);
    }

    void UpdateBody()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            int index = Mathf.Min((i + 1) * pointsPerSegment, path.Count - 1);

            segments[i].transform.position = path[index];

            Vector3 dir = path[Mathf.Max(index - 1, 0)] - path[index];

            if (dir.sqrMagnitude > 0.001f)
            {
                segments[i].transform.position = Vector3.MoveTowards(
                    segments[i].transform.position,
                    path[index],
                    followSpeed * Time.deltaTime);
                
                Quaternion targetRotation = Quaternion.LookRotation(dir);

                segments[i].transform.rotation = Quaternion.RotateTowards(
                    segments[i].transform.rotation,
                    targetRotation,
                    720f * Time.deltaTime);
            }
        }
    }

    void SpawnStartingSnake()
    {
        List<SnakeSegment> available = new List<SnakeSegment>(availableFoodSegments);

        AddSegment(available[0], animateSpawn: false);
        
        for (int i = 1; i < startingLength && available.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, available.Count);

            // Starting segments appear instantly - no pop-in on scene load
            AddSegment(available[randomIndex], animateSpawn: false);

            available.RemoveAt(randomIndex);
        }

        foodSpawner.HighlightFoodsOfType(segments[0].FoodType);
        Debug.Log(segments[0].FoodType.ToString());
        UpdateBody();
    }

    public void AddEatenSegment(SnakeSegment segment)
    {
        foreach (var food in availableFoodSegments)
        {
            if (food.FoodType == segment.FoodType)
            {
                AddSegment(food);
                return;
            }
        }
    }

    public void AddSegments(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, availableFoodSegments.Count);

            // Starting segments appear instantly - no pop-in on scene load
            AddSegment(availableFoodSegments[randomIndex], animateSpawn: false);
        }
    }

    public void RemoveSegments(int count)
    {
        for (int i = 0; i < count; i++)
        {
            RemoveSegment(segments.Count - 1);
        }
    }

    public void AddSegment(SnakeSegment food, bool animateSpawn = true)
    {
        cameraController.ShakeOnSegmentAdded();
        
        SnakeSegment segment = Instantiate(food, bodyRoot);
        segment.OnAddedToBody();
        segments.Add(segment);
        UpdateBodyLimitUI();

        if (animateSpawn)
            AnimateSegmentSpawn(segment.transform);
    }
    
    public void AddSegment(Food food)
    {
        SnakeSegment segment = Instantiate(food.bodyPrefab, bodyRoot);
        segments.Add(segment);

        AnimateSegmentSpawn(segment.transform);
    }

    private void AnimateSegmentSpawn(Transform segmentTransform)
    {
        Vector3 targetScale = segmentTransform.localScale;

        segmentTransform.DOKill();
        segmentTransform.localScale = Vector3.zero;
        segmentTransform
            .DOScale(targetScale, segmentSpawnDuration)
            .SetEase(segmentSpawnEase);
    }

    public void RemoveSegment(int index)
    {
        if (index < 0 || index >= segments.Count)
            return;

        SnakeSegment segment = segments[index];
        segments.RemoveAt(index);

        AnimateSegmentRemoval(segment);
        UpdateBodyLimitUI();
    }

    private void AnimateSegmentRemoval(SnakeSegment segment)
    {
        Transform t = segment.transform;

        t.DOKill();
        t.DOScale(Vector3.zero, segmentRemoveDuration)
            .SetEase(segmentRemoveEase)
            .OnComplete(() =>
            {
                if (segment != null)
                    Destroy(segment.gameObject);
            });
    }

    public void RemoveSegment()
    {
        RemoveSegment(0);
        OnAteFood();
        
        uimanager.PopUpDestroyedSegments(1);
    }
    
    public void RemoveAllSegments(FoodType foodType)
    {
        int destroyedSegments = 0;
        
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            if (segments[i].FoodType == foodType)
            {
                RemoveSegment(i);
                OnAteFood();
                destroyedSegments++;
            }
        }
        
        uimanager.PopUpDestroyedSegments(destroyedSegments);
    }

    int foodCount = 0;
    private int level = 1;
    private int maxFoodBeforeGolderBurger = 10;
    private int score = 0;
    
    public void OnAteFood()
    {
        foodCount++;
        score += 10;
        SFXManager.instance.PlaySegmentRemoved();

        // Small punch on the head every time food is eaten.
        // DOKill(true) completes any in-flight punch first (snapping scale back
        // to its correct end value) instead of aborting it mid-air - killing
        // mid-tween is what was leaving the head stuck at a non-1 scale.
        head.DOKill(true);
        head.localScale = Vector3.one;
        head.DOPunchScale(Vector3.one * eatPunchScale, eatPunchDuration, vibrato: 6, elasticity: 0.7f);

        if (foodCount >= maxFoodBeforeGolderBurger)
        {
            level++;
            if(segments.Count > 10)
                uimanager.OpenPowerUpPanel(false);
            else
            {
                uimanager.OpenPowerUpPanel(true);
            }
            foodCount = 0;

            // Bigger punch to sell the level-up moment
            head.DOKill(true);
            head.localScale = Vector3.one;
            head.DOPunchScale(Vector3.one * levelUpPunchScale, levelUpPunchDuration, vibrato: 8, elasticity: 0.9f);

            Debug.Log("Level Up");
        }
        
        float levelValue = foodCount / (float)maxFoodBeforeGolderBurger;
        uimanager.IncrementLevel(level, levelValue);
    }

    public bool IsTheTargetFood(FoodType foodType)
    {
        if(segments.Count == 0) return false;
        return foodType == segments[0].FoodType;
    }

    public FoodType GetTargetFood()
    {
        if (segments.Count == 0) return FoodType.Apple;
        return segments[0].FoodType;
    }
    
    public bool ContainsFood(FoodType type)
    {
        foreach(var segment in segments)
        {
            if(segment.FoodType == type)
                return true;
        }

        return false;
    }

    public void RemoveLastSegment()
    {
        if (segments.Count == 0)
            return;

        RemoveSegment(segments.Count - 1);
    }
    
    private void UpdateBodyLimitUI()
    {
        float percentage = segments.Count / (float)maxBodyLength;
        uimanager.UpdateBodyLimitBar(percentage);
        SFXManager.instance.UpdateMusicPitch(segments.Count, maxBodyLength);

        if (segments.Count >= maxBodyLength)
        {
            snakeHeadController.GameOver();
            TriggerGameOver();
        }
    }
    
    private bool gameOver;
    
    public void TriggerGameOver()
    {
        if (gameOver)
            return;
 
        gameOver = true;
 
        canAddSegments = false;
        if (CheckAndSpawnSegmentsRoutine != null)
            StopCoroutine(CheckAndSpawnSegmentsRoutine);
 
        for (int i = 0; i < segments.Count; i++)
        {
            Transform t = segments[i].transform;
 
            t.DOKill();
            // Unparent (keeping world position) so nothing else moves it once it's falling
            t.SetParent(null, true);
 
            Vector3 scatterDir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 targetPos = t.position + scatterDir * Random.Range(0.3f, segmentScatterRadius);
 
            Vector3 fallAxis = Random.value > 0.5f ? Vector3.right : Vector3.forward;
            float fallAngle = Random.value > 0.5f ? 90f : -90f;
 
            DOTween.Sequence()
                .SetDelay(i * segmentDetachStagger)
                .Append(t.DOMove(targetPos, segmentFallDuration).SetEase(Ease.OutQuad))
                .Join(t.DORotate(fallAxis * fallAngle, segmentFallDuration, RotateMode.WorldAxisAdd).SetEase(segmentFallEase));
        }

        uimanager.GameOver(score);
    }

    private void OnDestroy()
    {
        // Prevent DOTween callbacks firing on destroyed objects during scene teardown
        DOTween.Kill(head);
        foreach (var segment in segments)
        {
            if (segment != null)
                DOTween.Kill(segment.transform);
        }
    }
}