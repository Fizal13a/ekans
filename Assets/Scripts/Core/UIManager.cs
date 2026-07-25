using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class UIManager : MonoBehaviour
{
    [SerializeField] private CameraController cameraController;
    [SerializeField] private SnakeHeadController snakeHeadController;
    [SerializeField] private SnakeBodyController snakebodyController;
    [SerializeField] private FoodSpawner foodSpawner;

    [Header("Panels")] public GameObject powerUpPanel;
    public CanvasGroup gameOverBG;
    public GameObject gameOverPanel;
    public List<Transform> gameOverStaggerElements;
    public List<RectTransform> gameOverFloatingDecor;
    public TextMeshProUGUI gameOverScoreText;

    [Header("PowerUp")] public List<ChaosScriptableObject> positiveChaosScriptableObjects;
    public List<ChaosScriptableObject> negativeChaosScriptableObjects;
    public List<ChaosCardUI> chaosCards;

    [Header("UI")] public TextMeshProUGUI levelText;
    public Image levelBarImage;
    public List<TextMeshProUGUI> segmentMultiplyPopUps;

    [Header("Timer")] public RectTransform timerBarRoot;
    public Image timerFillImage;
    [SerializeField] private float timerDuration = 10f;
    [SerializeField] private Color timerFullColor = Color.green;
    [SerializeField] private Color timerDangerColor = Color.red;
    [SerializeField] private float timerDangerThreshold = 0.3f;
    [SerializeField] private float timerPopDuration = 0.2f;
    [SerializeField] private float timerPulseScale = 1.15f;
    [SerializeField] private float timerPulseDuration = 0.25f;
    
    [Header("Countdown")] [SerializeField] private GameObject counterObject;
    [SerializeField] private TMP_Text counterText; // the TMP child on counterObject
    [SerializeField] private float countdownStepDuration = 1f;
    [SerializeField] private float countdownPunchScale = 0.4f;
    
    [Header("Body Limit Bar")] [SerializeField]
    private Image bodyLimitFill;

    [SerializeField] private Gradient bodyLimitGradient;
    [SerializeField] private RectTransform barContainer; // parent rect of the fill bar, for punch/shake
    [SerializeField] private Image flashOverlay; // thin white image over the fill, alpha 0 by default
    [SerializeField] private float dangerThreshold = 0.85f; // when the bar starts "warning" pulsing

    private Tween fillTween;
    private Tween punchTween;
    private Tween dangerPulseTween;

    [Header("Polish / Juice")] [SerializeField]
    private float levelBarFillDuration = 0.3f;

    [SerializeField] private float levelTextPunchScale = 0.4f;
    [SerializeField] private float levelTextPunchDuration = 0.35f;
    [SerializeField] private float popUpScaleDuration = 0.15f;
    [SerializeField] private float popUpHoldDuration = 0.4f;
    [SerializeField] private float popUpFadeOutDuration = 0.25f;
    [SerializeField] private float panelOpenDuration = 0.35f;
    [SerializeField] private float panelCloseDuration = 0.2f;
    [SerializeField] private float cardStaggerDelay = 0.08f;
    [SerializeField] private float gameOverBGFadeDuration = 0.8f;
    [SerializeField] private float gameOverPanelPopDuration = 0.4f;
    [SerializeField] private float gameOverStaggerDelay = 0.1f;
    [SerializeField] private float decorFloatHeight = 15f;
    [SerializeField] private float decorFloatDurationMin = 1.2f;
    [SerializeField] private float decorFloatDurationMax = 2f;
    [SerializeField] private float decorSwayAngle = 8f;

    private int lastDisplayedLevel = 1;
    private Sequence panelSequence;
    private Sequence gameOverSequence;
    private readonly Dictionary<RectTransform, Vector2> decorBasePositions = new();

    private Tweener timerFillTween;
    private Sequence timerPulseSequence;
    private bool timerActive;
    private bool timerDangerPulseStarted;
    
    #region Initialize

    private void OnEnable()
    {
        GameManager.events.AddEvent<SegmentAddedData>(GameEvents.EventType.OnNewSegmentAdded, UpdateBodyLimitBar);
        GameManager.events.AddEvent<int>(GameEvents.EventType.OnGameOverPanelTrigger, GameOver);
        GameManager.events.AddEvent(GameEvents.EventType.OnSegmentRemoved, PopUpDestroyedSegments);
    }

    #endregion

    #region Level

    public void IncrementLevel(int level, float value)
    {
        levelBarImage.DOKill();
        levelBarImage.DOFillAmount(value, levelBarFillDuration)
            .SetEase(Ease.OutQuad);

        levelText.text = level.ToString();

        if (level != lastDisplayedLevel)
        {
            lastDisplayedLevel = level;

            levelText.transform.DOKill();
            levelText.transform.DOPunchScale(Vector3.one * levelTextPunchScale, levelTextPunchDuration, vibrato: 8,
                elasticity: 0.8f);
        }
    }

    #endregion

    #region PowerUp

    public void OpenPowerUpPanel(bool canShowNegative)
    {
        Time.timeScale = 0;

        //SFXManager.instance.PlayLevelUp();
        powerUpPanel.SetActive(true);

        List<ChaosScriptableObject> availableChaos;

        if (canShowNegative)
        {
            availableChaos = new List<ChaosScriptableObject>(negativeChaosScriptableObjects);
        }
        else
        {
            availableChaos = new List<ChaosScriptableObject>(positiveChaosScriptableObjects);
        }

        panelSequence?.Kill();
        powerUpPanel.transform.DOKill();
        powerUpPanel.transform.localScale = Vector3.zero;

        panelSequence = DOTween.Sequence()
            .SetUpdate(true) // keep playing while Time.timeScale is 0
            .Append(powerUpPanel.transform.DOScale(Vector3.one, panelOpenDuration).SetEase(Ease.OutBack));

        for (int i = 0; i < chaosCards.Count; i++)
        {
            ChaosCardUI chaosCard = chaosCards[i];

            int randomIndex = Random.Range(0, availableChaos.Count);
            chaosCard.SetUpCard(availableChaos[randomIndex]);
            availableChaos.RemoveAt(randomIndex);

            chaosCard.transform.DOKill();
            chaosCard.transform.localScale = Vector3.zero;

            panelSequence.Insert(
                panelOpenDuration * 0.5f + i * cardStaggerDelay,
                chaosCard.transform.DOScale(Vector3.one, panelOpenDuration).SetEase(Ease.OutBack));
        }
    }
    
    public void ClosePowerUpPanel(ChaosType chaosType)
    {
        panelSequence?.Kill();
        powerUpPanel.transform.DOKill();

        powerUpPanel.transform.DOScale(Vector3.zero, panelCloseDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                powerUpPanel.SetActive(false);
                foreach (ChaosCardUI chaosCard in chaosCards)
                {
                    chaosCard.ResetCard();
                }
                
                PlayCountdown(3, () =>
                {
                    Time.timeScale = 1;
                    DoChaos(chaosType);
                    cameraController.ShakeOnSegmentAdded();
                });
            });
    }
    
    public void OnChaosSelected(ChaosCardUI chaosCard)
    {
        //SFXManager.instance.PlayChaosSelected();
        ChaosType type = chaosCard.GetChaosObject().chaosType;
        ClosePowerUpPanel(type);
    }

    private void DoChaos(ChaosType chaosType)
    {
        switch (chaosType)
        {
            case ChaosType.Fast:
                snakeHeadController.SpeedChange(2);
                ShowTimer();
                break;
            case ChaosType.Slow:
                snakeHeadController.SpeedChange(-2);
                ShowTimer();
                break;
            case ChaosType.Plus5:
                snakebodyController.AddSegments(5);
                break;
            case ChaosType.Plus10:
                snakebodyController.AddSegments(10);
                break;
            case ChaosType.Minus5:
                snakebodyController.RemoveSegments(5);
                break;
            case ChaosType.Minus10:
                snakebodyController.RemoveSegments(10);
                break;
            case ChaosType.ChangeItems:
                foodSpawner.ResetFoods();
                break;
            case ChaosType.PassThrough:
                snakeHeadController.PassThrough();
                ShowTimer();
                break;
            case ChaosType.Inverse:
                snakeHeadController.Inverse();
                ShowTimer();
                break;
        }
    }

    #endregion

    

    public void PopUpDestroyedSegments()
    {
        foreach (TextMeshProUGUI popUp in segmentMultiplyPopUps)
        {
            if (popUp != null && !popUp.gameObject.activeInHierarchy)
            {
                popUp.gameObject.SetActive(true);
                popUp.text = $"x{1}";

                popUp.transform.DOKill();
                popUp.DOKill();

                Color c = popUp.color;
                c.a = 1f;
                popUp.color = c;
                popUp.transform.localScale = Vector3.zero;

                DOTween.Sequence()
                    .Append(popUp.transform.DOScale(Vector3.one, popUpScaleDuration).SetEase(Ease.OutBack))
                    .AppendInterval(popUpHoldDuration)
                    .Append(popUp.DOFade(0f, popUpFadeOutDuration))
                    .OnComplete(() => { popUp.gameObject.SetActive(false); });

                return;
            }
        }
    }

    #region Count Down

     private void PlayCountdown(int from, Action onComplete)
    {
        counterObject.SetActive(true);
        counterText.transform.localScale = Vector3.one;

        Sequence countdownSequence = DOTween.Sequence().SetUpdate(true);

        for (int i = from; i >= 1; i--)
        {
            int number = i; // capture for closure

            countdownSequence.AppendCallback(() =>
            {
                counterText.text = number.ToString();
                counterText.transform.DOKill();
                counterText.transform
                    .DOPunchScale(Vector3.one * countdownPunchScale, countdownStepDuration * 0.5f, vibrato: 4,
                        elasticity: 0.6f)
                    .SetUpdate(true);
            });

            countdownSequence.AppendInterval(countdownStepDuration);
        }

        countdownSequence.OnComplete(() =>
        {
            counterObject.SetActive(false);
            onComplete?.Invoke();
        });
    }                                                                                                                   

   

    public void ShowTimer()
    {
        if (timerBarRoot == null || timerFillImage == null)
            return;

        timerBarRoot.gameObject.SetActive(true);

        timerActive = true;
        timerDangerPulseStarted = false;

        timerPulseSequence?.Kill();
        timerBarRoot.DOKill();
        timerFillImage.DOKill();

        timerBarRoot.gameObject.SetActive(true);
        timerBarRoot.localScale = Vector3.zero;
        timerBarRoot.DOScale(Vector3.one, timerPopDuration).SetEase(Ease.OutBack);

        timerFillImage.fillAmount = 1f;
        timerFillImage.color = timerFullColor;

        timerFillTween = timerFillImage
            .DOFillAmount(0f, timerDuration)
            .SetEase(Ease.Linear)
            .OnUpdate(UpdateTimerVisuals)
            .OnComplete(HideTimer);
    }

    public void CancelTimer()
    {
        if (!timerActive)
            return;

        timerFillTween?.Kill();
        HideTimer();
    }

    private void UpdateTimerVisuals()
    {
        float fill = timerFillImage.fillAmount;

        // Blend from danger color back to full color as fill rises above the threshold
        float colorT = Mathf.Clamp01(fill / timerDangerThreshold);
        timerFillImage.color = Color.Lerp(timerDangerColor, timerFullColor, colorT);

        if (fill <= timerDangerThreshold && !timerDangerPulseStarted)
        {
            timerDangerPulseStarted = true;
            StartTimerDangerPulse();
        }
    }

    private void StartTimerDangerPulse()
    {
        timerPulseSequence?.Kill();
        timerPulseSequence = DOTween.Sequence()
            .Append(timerBarRoot.DOScale(Vector3.one * timerPulseScale, timerPulseDuration).SetEase(Ease.OutQuad))
            .Append(timerBarRoot.DOScale(Vector3.one, timerPulseDuration).SetEase(Ease.InQuad))
            .SetLoops(-1);
    }

    private void HideTimer()
    {
        timerActive = false;
        timerPulseSequence?.Kill();
        timerBarRoot.DOKill();

        timerBarRoot.DOScale(Vector3.zero, timerPopDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                timerBarRoot.gameObject.SetActive(false);
                snakeHeadController.ResetAll();
            });
    }


    #endregion

    #region Game Over

    public void GameOver(int score)
    {
        gameOverSequence?.Kill();
        gameOverBG.DOKill();

        gameOverBG.alpha = 0f;
        gameOverBG.gameObject.SetActive(true);

        gameOverPanel.SetActive(false);
        gameOverPanel.transform.localScale = Vector3.zero;

        gameOverScoreText.text = $"Score: {score}";

        gameOverSequence = DOTween.Sequence()
            .SetUpdate(true) // keep playing even if something pauses Time.timeScale
            .Append(gameOverBG.DOFade(1f, gameOverBGFadeDuration))
            .AppendCallback(ShowGameOverPanel);

        StartDecorFloating();
    }

    private void StartDecorFloating()
    {
        foreach (RectTransform decor in gameOverFloatingDecor)
        {
            if (decor == null)
                continue;

            decor.DOKill();

            // Store each icon's original resting position once, so repeated
            // game-overs always float around the same base spot rather than
            // drifting from wherever the last loop happened to be killed.
            if (!decorBasePositions.TryGetValue(decor, out Vector2 basePos))
            {
                basePos = decor.anchoredPosition;
                decorBasePositions[decor] = basePos;
            }
            else
            {
                decor.anchoredPosition = basePos;
                decor.localRotation = Quaternion.identity;
            }

            float floatDuration = Random.Range(decorFloatDurationMin, decorFloatDurationMax);
            decor.DOAnchorPosY(basePos.y + decorFloatHeight, floatDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(Random.Range(0f, floatDuration))
                .SetUpdate(true);

            float swayDuration = Random.Range(decorFloatDurationMin, decorFloatDurationMax) * 1.3f;
            float swayDir = Random.value > 0.5f ? 1f : -1f;
            decor.DOLocalRotate(new Vector3(0f, 0f, decorSwayAngle * swayDir), swayDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(Random.Range(0f, swayDuration))
                .SetUpdate(true);
        }
    }

    private void ShowGameOverPanel()
    {
        gameOverPanel.SetActive(true);
        gameOverPanel.transform.DOKill();
        gameOverPanel.transform.localScale = Vector3.zero;

        Sequence panelPop = DOTween.Sequence()
            .SetUpdate(true)
            .Append(gameOverPanel.transform.DOScale(Vector3.one, gameOverPanelPopDuration).SetEase(Ease.OutBack));

        for (int i = 0; i < gameOverStaggerElements.Count; i++)
        {
            Transform element = gameOverStaggerElements[i];
            if (element == null)
                continue;

            element.DOKill();
            element.localScale = Vector3.zero;

            panelPop.Insert(
                gameOverPanelPopDuration * 0.4f + i * gameOverStaggerDelay,
                element.DOScale(Vector3.one, gameOverPanelPopDuration).SetEase(Ease.OutBack));
        }
    }
    
    public void TryAgain()
    {
        SceneManager.LoadScene(0);
    }

    #endregion

    #region Body

    public void UpdateBodyLimitBar(SegmentAddedData data)
    {
        float percentage = Mathf.Clamp01(data.Percentage);
        float previousValue = bodyLimitFill.fillAmount;
        float delta = percentage - previousValue;

        fillTween?.Kill();
        punchTween?.Kill();

        // --- Fill + color, same as before ---
        fillTween = bodyLimitFill
            .DOFillAmount(percentage, 0.25f)
            .SetEase(Ease.OutCubic)
            .OnUpdate(() => { bodyLimitFill.color = bodyLimitGradient.Evaluate(bodyLimitFill.fillAmount); });

        // --- Punch scale on the whole bar, sized by how big the change was ---
        // Growing = punch up (satisfying "gained something"), shrinking = punch down/in (a hit).
        if (barContainer != null && Mathf.Abs(delta) > 0.001f)
        {
            float punchSize = Mathf.Lerp(0.13f, 0.4f, Mathf.Clamp01(Mathf.Abs(delta) * 3f));
            Vector3 punch = delta > 0
                ? new Vector3(punchSize, punchSize, 0f)
                : new Vector3(-punchSize * 0.6f, -punchSize * 0.6f, 0f);

            barContainer.localScale = Vector3.one;
            punchTween = barContainer
                .DOPunchScale(punch, 0.3f, 8, 0.9f)
                .SetEase(Ease.OutQuad);
        }

        // --- Quick white flash on the fill itself, for a crisp "impact" moment ---
        if (flashOverlay != null && Mathf.Abs(delta) > 0.001f)
        {
            flashOverlay.DOKill();
            flashOverlay.color = Color.red;
            flashOverlay.DOFade(0f, 0.3f).SetEase(Ease.OutCubic);
        }

        // --- Warning pulse when the bar creeps into danger territory ---
        bool inDanger = percentage >= dangerThreshold;
        bool wasInDanger = dangerPulseTween != null && dangerPulseTween.IsActive();

        if (inDanger && !wasInDanger && barContainer != null)
        {
            dangerPulseTween = barContainer
                .DOScale(1.04f, 0.35f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (!inDanger && wasInDanger)
        {
            dangerPulseTween.Kill();
            dangerPulseTween = null;
            barContainer.localScale = Vector3.one;
        }
    }

    #endregion

    #region Terminate

    private void OnDestroy()
    {
        DOTween.Kill(levelBarImage);
        DOTween.Kill(levelText?.transform);
        if (powerUpPanel != null)
            DOTween.Kill(powerUpPanel.transform);
        if (gameOverBG != null)
            DOTween.Kill(gameOverBG);
        if (gameOverPanel != null)
            DOTween.Kill(gameOverPanel.transform);

        foreach (var element in gameOverStaggerElements)
        {
            if (element != null)
                DOTween.Kill(element);
        }

        foreach (var decor in gameOverFloatingDecor)
        {
            if (decor != null)
                DOTween.Kill(decor);
        }

        foreach (var card in chaosCards)
        {
            if (card != null)
                DOTween.Kill(card.transform);
        }

        foreach (var popUp in segmentMultiplyPopUps)
        {
            if (popUp != null)
            {
                DOTween.Kill(popUp.transform);
                DOTween.Kill(popUp);
            }
        }
    }
    
    private void OnDisable()
    {
        GameManager.events.RemoveEvent<SegmentAddedData>(GameEvents.EventType.OnNewSegmentAdded, UpdateBodyLimitBar);
        GameManager.events.RemoveEvent<int>(GameEvents.EventType.OnGameOverPanelTrigger, GameOver);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSegmentRemoved, PopUpDestroyedSegments);
    }

    #endregion
    
}