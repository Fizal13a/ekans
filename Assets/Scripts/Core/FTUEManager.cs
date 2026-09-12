using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class FTUEManager : MonoBehaviour
{
    
    //FTUE KEYS
    private const string controlsKey = "CONTROLS";
    private const string firstFoodKey = "FIRST_FOOD";
    private const string snakeLengthKey = "SNAKE_LENGTH";
    private const string dashOnFoodKey = "DASH";
    private const string orderKey = "ORDER";
    
    
    [SerializeField] private FoodSpawner foodSpawner;
    
    public bool canShowTutorial = true;
    private GameControls gameControls;
    
    private bool firstFoodDone = false;
    private bool snakeLengthDone = false;
    private bool dashOnFoodDone = false;
    private bool specialAttackDone  = false;
    private bool craftUnlocked  = false;
    
    private bool isShowingTutorial = false;
    private bool canSkip = false;
    
    private int dashTUTTriggerCount = 2;
    private int currentDashCount = 0;
    
    public CanvasGroup controlsTut;
    public CanvasGroup firstFoodTut;
    public CanvasGroup snakeLengthTut;
    public CanvasGroup dashOnFoodTut;
    public CanvasGroup orderTut;

    public Canvas snakeLengthHUD;
    public Canvas orderHUD;

    private void OnEnable()
    {
        Initialize();
        
        if(!canShowTutorial) return;
        
        gameControls = new GameControls();
        gameControls.Enable();

        gameControls.Game.Skip.performed += OnSkipPressed;
        GameManager.events.AddEvent(GameEvents.EventType.OnGameStart, ControlsTut);
        GameManager.events.AddEvent(GameEvents.EventType.OnAteRightFood, FirstFoodTut);
        GameManager.events.AddEvent(GameEvents.EventType.OnSnakeLengthWarning, SnakeLengthTut);
        GameManager.events.AddEvent(GameEvents.EventType.OnDashTut, DashOnFoodTut);
        //GameManager.events.AddEvent(GameEvents.EventType.OnLengthZero, SpecialAttackTUT);
        GameManager.events.AddEvent(GameEvents.EventType.OnCraftItemCollected, OrderTut);
    }

    private void OnSkipPressed(InputAction.CallbackContext obj)
    {
        if(!isShowingTutorial) return;
        if(!canSkip) return;
        
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStopped, false);
        Initialize();
        canSkip = false;
    }

    private void Initialize()
    {
        controlsTut.gameObject.SetActive(false);
        firstFoodTut.gameObject.SetActive(false);
        snakeLengthTut.gameObject.SetActive(false);
        orderTut.gameObject.SetActive(false);
        dashOnFoodTut.gameObject.SetActive(false);
        
        controlsTut.transform.GetChild(2).gameObject.SetActive(false);
        firstFoodTut.transform.GetChild(2).gameObject.SetActive(false);
        snakeLengthTut.transform.GetChild(2).gameObject.SetActive(false);
        orderTut.transform.GetChild(2).gameObject.SetActive(false);
        dashOnFoodTut.transform.GetChild(2).gameObject.SetActive(false);

        snakeLengthHUD.sortingOrder = 0;
        orderHUD.sortingOrder = 0;
    }

    public void ControlsTut()
    {
        if(PlayerPrefs.HasKey(controlsKey)) return;
        
        PlayerPrefs.SetInt(controlsKey, 1);
        controlsTut.gameObject.SetActive(true);
        
        isShowingTutorial = true;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
        
        StartCoroutine(EnableContinueMessage(controlsTut.transform.GetChild(2).gameObject));
    }

    public void FirstFoodTut()
    {
        if(firstFoodDone) return;
        
        if(PlayerPrefs.HasKey(firstFoodKey)) return;
        
        PlayerPrefs.SetInt(firstFoodKey, 1);
        
        firstFoodDone = true;
        firstFoodTut.gameObject.SetActive(true);
        
        isShowingTutorial = true;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
        
        foodSpawner.StopHighlights();
        StartCoroutine(EnableContinueMessage(firstFoodTut.transform.GetChild(2).gameObject));
    }
    
    public void SnakeLengthTut()
    {
        if (firstFoodDone && !snakeLengthDone)
        {
            if(PlayerPrefs.HasKey(snakeLengthKey)) return;
        
            PlayerPrefs.SetInt(snakeLengthKey, 1);
            
            snakeLengthDone = true;
            snakeLengthTut.gameObject.SetActive(true);
            
            snakeLengthHUD.sortingOrder = 3;
            orderHUD.sortingOrder = 0;
        
            isShowingTutorial = true;
            GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
            
            StartCoroutine(EnableContinueMessage(snakeLengthTut.transform.GetChild(2).gameObject));
        }
    }

    public void DashOnFoodTut()
    {
        if(dashOnFoodDone) return;
        
        Debug.Log("Can Dash On Food");
        currentDashCount++;

        if (currentDashCount >= dashTUTTriggerCount)
        {
            if(PlayerPrefs.HasKey(dashOnFoodKey)) return;
        
            PlayerPrefs.SetInt(dashOnFoodKey, 1);
            
            dashOnFoodDone = true;
            dashOnFoodTut.gameObject.SetActive(true);
        
            isShowingTutorial = true;
            GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
        
            StartCoroutine(EnableContinueMessage(dashOnFoodTut.transform.GetChild(2).gameObject));
        }
    }

    public void OrderTut()
    {
        if(craftUnlocked) return;
        
        if(PlayerPrefs.HasKey(orderKey)) return;
        
        PlayerPrefs.SetInt(orderKey, 1);
        
        craftUnlocked = true;
        orderTut.gameObject.SetActive(true);
        
        snakeLengthHUD.sortingOrder = 0;
        orderHUD.sortingOrder = 3;
        
        isShowingTutorial = true;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
        
        StartCoroutine(EnableContinueMessage(orderTut.transform.GetChild(2).gameObject));
    }

    IEnumerator EnableContinueMessage(GameObject obj)
    {
        yield return new WaitForSecondsRealtime(2f);
        obj.SetActive(true);
        canSkip = true;
    }

    private void OnDisable()
    {
        gameControls.Game.Skip.performed -= OnSkipPressed;
        gameControls.Enable();
        
        GameManager.events.RemoveEvent(GameEvents.EventType.OnGameStart, ControlsTut);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnAteRightFood, FirstFoodTut);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSnakeLengthWarning, SnakeLengthTut);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnDashTut, DashOnFoodTut);
        //GameManager.events.RemoveEvent(GameEvents.EventType.OnLengthZero, SpecialAttackTUT);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnCraftItemCollected, OrderTut);
    }
}
