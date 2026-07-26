using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public struct SegmentAddedData
{
    public float Percentage;
    public int SegmentCount;
    public int MaxBodyLength;

    public SegmentAddedData(float percentage, int segmentCount, int maxBodyLength)
    {
        Percentage = percentage;
        SegmentCount = segmentCount;
        MaxBodyLength = maxBodyLength;
    }
}

public struct LevelUpData
{
    public float LevelAmount;
    public int Level;
    public bool IsSnakeBig;

    public LevelUpData(float levelAmount, int level, bool isSnakeBig)
    {
        LevelAmount = levelAmount;
        Level = level;
        IsSnakeBig = isSnakeBig;
    }
}

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

    [Header("Movement")]
    [SerializeField] private float followSpeed = 2f;
    [SerializeField] private float recordDistance = 0.2f;
    [SerializeField] private int pointsPerSegment = 5;
    
    [Header("Food Data")]
    int foodCount = 0;
    private int maxFood = 10;
    private int score = 0;
    
    [Header("Snake Stats")]
    private int level = 1;
    [SerializeField] private int maxBodyLength = 50;
    public int MaxBodyLength => maxBodyLength;
    
    public FoodType TargetFood
    {
        get
        {
            if (segments.Count == 0)
                return FoodType.Apple;

            return segments[0].FoodType;
        }
    }
    
    [Header("Bool")]
    private bool gameOver;

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

    #region Initialization
    
    private void OnEnable()
    {
       GameManager.events.AddEvent(GameEvents.EventType.OnGameStart, Initialize);
       GameManager.events.AddEvent<SnakeSegment>(GameEvents.EventType.OnAteFood, OnNewFoodAte);
       GameManager.events.AddEvent(GameEvents.EventType.OnGameOver, TriggerGameOver);
       GameManager.events.AddEvent<ChaosType>(GameEvents.EventType.OnPowerUpSelected, OnPowerUpSelected);
    }

    private void Initialize()
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

        Debug.Log(segments[0].FoodType.ToString());
        UpdateBody();
        
        GameManager.events.TriggerEvent(GameEvents.EventType.OnSnakeInitialized, this);
    }

    #endregion

    #region Update

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

    #endregion

    #region Segment Management
    
    //ADD

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
    
    //REMOVE

    public void RemoveSegments(int count)
    {
        for (int i = 0; i < count; i++)
        {
            RemoveSegment(segments.Count - 1);
        }
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
    
    public void RemoveSegment()
    {
        RemoveSegment(0);
        OnAteFood();
        
        float levelValue = foodCount / (float)maxFood;
        GameManager.events.TriggerEvent<float>(GameEvents.EventType.OnSegmentRemoved, levelValue);
    }
    
    //ANIMATE
    
    private void AnimateSegmentSpawn(Transform segmentTransform)
    {
        Vector3 targetScale = segmentTransform.localScale;

        segmentTransform.DOKill();
        segmentTransform.localScale = Vector3.zero;
        segmentTransform
            .DOScale(targetScale, segmentSpawnDuration)
            .SetEase(segmentSpawnEase);
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
    
    #endregion

    #region Actions

    private void OnNewFoodAte(SnakeSegment segment)
    {
        if (IsTheTargetFood(segment.FoodType))
        {
            RemoveSegment();
            GameManager.events.TriggerEvent(GameEvents.EventType.OnAteRightFood, this);
        }
        else
        {
            AddEatenSegment(segment);
            GameManager.events.TriggerEvent(GameEvents.EventType.OnAteWrongFood);
        }
    }

    public void OnAteFood()
    {
        foodCount++;
        score += 10;

        head.DOKill(true);
        head.localScale = Vector3.one;
        head.DOPunchScale(Vector3.one * eatPunchScale, eatPunchDuration, vibrato: 6, elasticity: 0.7f);

        if (foodCount >= maxFood)
        {
            level++;
          
            float levelValue = foodCount / (float)maxFood;
            bool isSnakeSegmentMore = false || segments.Count > 10;
            
            GameManager.events.TriggerEvent(
                GameEvents.EventType.OnLevelUp,
                new LevelUpData(levelValue, level, isSnakeSegmentMore)
            );
          
            foodCount = 0;

            // Bigger punch to sell the level-up moment
            head.DOKill(true);
            head.localScale = Vector3.one;
            head.DOPunchScale(Vector3.one * levelUpPunchScale, levelUpPunchDuration, vibrato: 8, elasticity: 0.9f);

            Debug.Log("Level Up");
        }
    }

    #endregion

    #region Power Ups

    private void OnPowerUpSelected(ChaosType type)
    {
        switch (type)
        {
            case ChaosType.Plus5:
                AddSegments(5);
                break;
            case ChaosType.Plus10:
                AddSegments(10);
                break;
            case ChaosType.Minus5:
                RemoveSegments(5);
                break;
            case ChaosType.Minus10:
                RemoveSegments(10);
                break;
        }
    }

    #endregion

    #region Helpers

    public bool IsTheTargetFood(FoodType foodType)
    {
        if(segments.Count == 0) return false;
        return foodType == segments[0].FoodType;
    }

    #endregion

    #region UI

    private void UpdateBodyLimitUI()
    {
        float percentage = segments.Count / (float)maxBodyLength;
        GameManager.events.TriggerEvent(
            GameEvents.EventType.OnNewSegmentAdded,
            new SegmentAddedData(percentage, segments.Count, maxBodyLength)
        );

        if (segments.Count >= maxBodyLength)
        {
            GameManager.events.TriggerEvent(GameEvents.EventType.OnGameOver);
        }
    }

    #endregion

    #region Game Over

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

        GameManager.events.TriggerEvent(GameEvents.EventType.OnGameOverPanelTrigger, score);
    }

    #endregion

    #region Terminate

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
    
    private void OnDisable()
    {
        GameManager.events.RemoveEvent(GameEvents.EventType.OnGameStart, Initialize);
        GameManager.events.RemoveEvent<SnakeSegment>(GameEvents.EventType.OnAteFood, OnNewFoodAte);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnGameOver, TriggerGameOver);
        GameManager.events.RemoveEvent<ChaosType>(GameEvents.EventType.OnPowerUpSelected, OnPowerUpSelected);
    }

    #endregion
    

    
}