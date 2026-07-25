using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FTUEController : MonoBehaviour
{
    public static FTUEController Instance { get; private set; }
    
    [SerializeField] private FoodSpawner foodSpawner;

    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private RectTransform tapToContinueHint; 

    [Header("Juice Settings")]
    [SerializeField] private float popInDuration = 0.35f;
    [SerializeField] private float popOutDuration = 0.25f;
    [SerializeField] private Ease popInEase = Ease.OutBack;
    [SerializeField] private Ease popOutEase = Ease.InBack;
    [SerializeField] private float textPunchStrength = 0.15f;
    [SerializeField] private float hintPulseScale = 1.12f;
    [SerializeField] private float hintPulseDuration = 0.6f;

    [Header("FTUE Messages")]
    [TextArea] [SerializeField] private string startMessage =
        "Uh oh... this food is chasing ME for a change!";

    [TextArea] [SerializeField] private string eatFollowingMessage =
        "Oh! The food that's following me disappears when I eat it.";

    [TextArea] [SerializeField] private string eatNewMessage =
        "Wait — I probably shouldn't eat the ones that AREN'T following me...";
    
    [TextArea] [SerializeField] private string autoFoodAddedMessage =
        "Oh! The food gets added automatically too! its bad....";

    public bool IsInFTUE { get; private set; }

    private bool hasShownStart;
    private bool hasShownEatFollowing;
    private bool hasShownEatNew;

    private bool waitingForClick;

    private Tween timeScaleTween;
    private Sequence panelSequence;
    private Tween hintPulseTween;
    
    #region Initialize

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent(GameEvents.EventType.OnGameStart, StartingFTUE);
        GameManager.events.AddEvent<SnakeBodyController>(GameEvents.EventType.OnAteRightFood, OnRightFoodSelected);
        GameManager.events.AddEvent(GameEvents.EventType.OnAteWrongFood, OnWrongFoodSelected);
    }

    private void StartingFTUE()
    {
        //TO DO : player prefs for FTUE completion
        ShowFTUE(startMessage, ref hasShownStart, force: true);
        SFXManager.instance.PlayFTUE();
    }

    #endregion

    #region Actions

    /// <summary>
    /// Call this whenever the player collects/eats a food item.
    /// </summary>
    /// <param name="wasFollowingBody">True if the item existed on the body/tail segments.</param>

    private void OnRightFoodSelected(SnakeBodyController snake)
    {
        OnFoodCollected(true);
    }

    private void OnWrongFoodSelected()
    {
        OnFoodCollected(false);
    }
    
    public void OnFoodCollected(bool wasFollowingBody)
    {
        if (wasFollowingBody)
        {
            if (!hasShownEatFollowing)
            {
                ShowFTUE(eatFollowingMessage, ref hasShownEatFollowing);
                foodSpawner.StopHighlights();
                SFXManager.instance.PlayFTUESegmentDissappear();
            }
        }
        else
        {
            if (!hasShownEatNew)
            {
                ShowFTUE(eatNewMessage, ref hasShownEatNew);
                SFXManager.instance.PlayFTUEAte();
            }
        }
    }

    bool isAutoFoodAdded = false;
    public void OnFoodAdded()
    {
        if(isAutoFoodAdded) return;
        
        Debug.Log("Auto food added");
        ShowFTUE(autoFoodAddedMessage,ref isAutoFoodAdded);
        isAutoFoodAdded = true;
    }

    /// <summary>
    /// Hook this to the dialogue panel's Button OnClick() event.
    /// </summary>
    public void OnDialogueClicked()
    {
        if (!waitingForClick) return;
        waitingForClick = false;

        StopHintPulse();

        // Little punchy feedback on the click itself before popping out.
        if (panelRect != null)
            panelRect.DOPunchScale(Vector3.one * 0.08f, 0.15f, 6, 0.8f).SetUpdate(true);

        PlayPopOut();
        IsInFTUE = false;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStopped, false);
    }

    #endregion

    #region FTUEWindowControl

    private void ShowFTUE(string message, ref bool alreadyShownFlag, bool force = false)
    {
        if (alreadyShownFlag && !force) return;
        alreadyShownFlag = true;

        if (dialogueText != null) dialogueText.text = message;
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        waitingForClick = true;
        IsInFTUE = true;

        PlayPopIn();
        IsInFTUE = true;
        GameManager.events.TriggerEvent<bool>(GameEvents.EventType.OnFTUEStarted, true);
        StartHintPulse();
    }
    
    #endregion

    #region Animate Panel

    // ---------- DOTween juice ----------

    private void PlayPopIn()
    {
        panelSequence?.Kill();

        if (panelRect != null) panelRect.localScale = Vector3.zero;
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;

        panelSequence = DOTween.Sequence().SetUpdate(true);

        if (panelRect != null)
            panelSequence.Join(panelRect.DOScale(1f, popInDuration).SetEase(popInEase));

        if (panelCanvasGroup != null)
            panelSequence.Join(panelCanvasGroup.DOFade(1f, popInDuration * 0.7f));

        // Little extra pop/emphasis on the text once the panel lands.
        if (dialogueText != null)
        {
            panelSequence.OnComplete(() =>
                dialogueText.rectTransform.DOPunchScale(Vector3.one * textPunchStrength, 0.3f, 8, 0.9f)
                    .SetUpdate(true));
        }
    }

    private void PlayPopOut()
    {
        panelSequence?.Kill();
        panelSequence = DOTween.Sequence().SetUpdate(true);

        if (panelRect != null)
            panelSequence.Join(panelRect.DOScale(0f, popOutDuration).SetEase(popOutEase));

        if (panelCanvasGroup != null)
            panelSequence.Join(panelCanvasGroup.DOFade(0f, popOutDuration));

        panelSequence.OnComplete(() =>
        {
            if (dialoguePanel != null)
                dialoguePanel.SetActive(false);
        });
    }

    private void StartHintPulse()
    {
        if (tapToContinueHint == null) return;
        StopHintPulse();

        tapToContinueHint.localScale = Vector3.one;
        hintPulseTween = tapToContinueHint
            .DOScale(hintPulseScale, hintPulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void StopHintPulse()
    {
        hintPulseTween?.Kill();
        hintPulseTween = null;
        if (tapToContinueHint != null)
            tapToContinueHint.localScale = Vector3.one;
    }

    #endregion

    #region Terminate

    private void OnDestroy()
    {
        // Safety: never leave the game stuck in slow motion or with dangling tweens.
        timeScaleTween?.Kill();
        panelSequence?.Kill();
        hintPulseTween?.Kill();
    }

    #endregion
  
}