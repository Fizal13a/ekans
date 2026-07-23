using System;
using DG.Tweening;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameEvents events;
    
    [Header("Time Scale")]
    private const float DefaultFixedDelta = 0.02f;
    private Tween timeScaleTween;
    [Tooltip("How slow time gets while a dialogue is showing (0 = fully paused, 1 = normal).")]
    [Range(0.01f, 1f)] [SerializeField] private float slowTimeScale = 0.05f;
    [Tooltip("How long the slow-down / speed-up transition takes, in real (unscaled) seconds.")]
    [SerializeField] private float timeTransitionDuration = 0.5f;
    private float ftueCloseDelay = 1f;

    private void Awake()
    {
        events = new GameEvents();
    }

    private void OnEnable()
    {
        events.AddEvent<bool>(GameEvents.EventType.OnFTUEStarted, StartTimeScaleTransition);
        events.AddEvent<bool>(GameEvents.EventType.OnFTUEStopped, StartTimeScaleTransition);
    }

    private void Start()
    {
        events.TriggerEvent(GameEvents.EventType.OnGameStart);
    }

    #region Time Scale

    private void StartTimeScaleTransition(bool isFTUEShowing)
    {
        Debug.Log("STARTNG TIME SCALE TRANSITION");
        
        timeScaleTween?.Kill();
        float target;
        if (isFTUEShowing)
            target = slowTimeScale;
        else
        {
            target = ftueCloseDelay;
        }

        // DOVirtual.Float lets us ease a plain float over unscaled time (SetUpdate(true))
        // and drive Time.timeScale ourselves each step.
        timeScaleTween = DOVirtual.Float(Time.timeScale, target, timeTransitionDuration, value =>
            {
                Time.timeScale = value;
                Time.fixedDeltaTime = DefaultFixedDelta * value;
            })
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    #endregion
}
