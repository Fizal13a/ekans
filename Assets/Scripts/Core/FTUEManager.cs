using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class FTUEManager : MonoBehaviour
{
    public bool canShowTutorial = true;
    private GameControls gameControls;
    
    private bool firstFoodDone = false;
    private bool snakeLengthDone = false;
    private bool dashOnFoodDone = false;
    private bool specialAttackDone  = false;
    
    private bool isShowingTutorial = false;
    
    public CanvasGroup controlsTut;
    public CanvasGroup firstFoodTut;
    public CanvasGroup snakeLengthTut;
    public CanvasGroup dashOnFoodTut;
    public CanvasGroup specialAttackTut;
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
        GameManager.events.AddEvent(GameEvents.EventType.OnAteWrongFood, SnakeLengthTut);
        GameManager.events.AddEvent(GameEvents.EventType.OnDashTut, DashOnFoodTut);
        GameManager.events.AddEvent(GameEvents.EventType.OnLengthZero, SpecialAttackTUT);
        GameManager.events.AddEvent(GameEvents.EventType.OnCraftAttackUnlocked, OrderTut);
    }

    private void OnSkipPressed(InputAction.CallbackContext obj)
    {
        if(!isShowingTutorial) return;
        
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStopped, false);
        Initialize();
    }

    private void Initialize()
    {
        controlsTut.gameObject.SetActive(false);
        firstFoodTut.gameObject.SetActive(false);
        snakeLengthTut.gameObject.SetActive(false);
        orderTut.gameObject.SetActive(false);
        dashOnFoodTut.gameObject.SetActive(false);
        specialAttackTut.gameObject.SetActive(false);

        snakeLengthHUD.sortingOrder = 0;
        orderHUD.sortingOrder = 0;
    }

    public void ControlsTut()
    {
        controlsTut.gameObject.SetActive(true);
        
        isShowingTutorial = true;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
    }

    public void FirstFoodTut()
    {
        if(firstFoodDone) return;
        
        firstFoodDone = true;
        firstFoodTut.gameObject.SetActive(true);
        
        isShowingTutorial = true;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
    }

    public void SnakeLengthTut()
    {
        if (firstFoodDone && !snakeLengthDone)
        {
            snakeLengthDone = true;
            snakeLengthTut.gameObject.SetActive(true);
        
            snakeLengthHUD.sortingOrder = 1;
        
            isShowingTutorial = true;
            GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
        }
    }

    public void DashOnFoodTut()
    {
        if(dashOnFoodDone) return;
        
        dashOnFoodDone = true;
        dashOnFoodTut.gameObject.SetActive(true);
        
        isShowingTutorial = true;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
    }

    public void SpecialAttackTUT()
    {
        if(specialAttackDone)  return;
        
        specialAttackDone  = true;
        specialAttackTut.gameObject.SetActive(true);
        
        isShowingTutorial = true;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
    }

    public void OrderTut()
    {
        orderTut.gameObject.SetActive(true);
        
        orderHUD.sortingOrder = 1;
        
        isShowingTutorial = true;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
    }

    private void OnDisable()
    {
        gameControls.Game.Skip.performed -= OnSkipPressed;
        gameControls.Enable();
        
        GameManager.events.RemoveEvent(GameEvents.EventType.OnAteRightFood, FirstFoodTut);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnAteWrongFood, SnakeLengthTut);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnDashTut, DashOnFoodTut);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnLengthZero, SpecialAttackTUT);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnCraftAttackUnlocked, OrderTut);
    }
}
