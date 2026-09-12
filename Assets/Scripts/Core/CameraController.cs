using System;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private SnakeBodyController snake;

    [Header("Follow")]
    [SerializeField] private Vector3 offset = new(0, 15, -12);
    [SerializeField] private float followSmoothTime = 0.2f;

    [Header("Zoom")]
    [SerializeField] private float minHeight = 15f;
    [SerializeField] private float maxHeight = 30f;
    [SerializeField] private int minSnakeLength = 5;
    [SerializeField] private int maxSnakeLength = 30;
    [SerializeField] private float zoomSmoothSpeed = 5f;

    [Header("Camera Bounds")]
    [SerializeField] private float minimumCameraZ = -30f;
    
    [Header("Polish - Zoom Out Sequence")]
    [SerializeField] private Transform cameraZoomOutTarget;
    [SerializeField] private float zoomOutTransitionDuration = 1f;
    [SerializeField] private float zoomOutHoldDuration = 2f;
    [SerializeField] private Ease zoomOutEase = Ease.InOutSine;

    private bool isInZoomOutSequence;
    private Sequence zoomOutSequence;

    [Header("Polish - Camera Shake")]
    [SerializeField] private float eatShakeStrength = 0.15f;
    [SerializeField] private float eatShakeDuration = 0.2f;
    [SerializeField] private float levelUpShakeStrength = 0.5f;
    [SerializeField] private float levelUpShakeDuration = 0.4f;

    [Header("Polish - Zoom Kick")]
    [SerializeField] private float zoomKickAmount = -1.5f;
    [SerializeField] private float zoomKickDuration = 0.25f;

    [Header("Polish - Camera Banking")]
    [SerializeField] private float maxBankAngle = 8f;
    [SerializeField] private float bankTurnSensitivity = 8f;
    [SerializeField] private float bankSmoothSpeed = 4f;

    private Vector3 velocity;

    private float currentHeight;
    private float currentShakeMagnitude;
    private float zoomKick;
    private float currentBank;

    private Vector3 lastTargetForward;
    private Vector3 baseRotationEuler;

    private Tweener shakeTween;
    private Sequence zoomKickSequence;

    private void Start()
    {
        if (target == null || snake == null)
        {
            Debug.LogError($"{nameof(CameraController)} is missing required references.");
            enabled = false;
            return;
        }

        currentHeight = minHeight;
        lastTargetForward = target.forward;
        baseRotationEuler = transform.eulerAngles;
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent(GameEvents.EventType.OnAteFood, ShakeOnEat);
        GameManager.events.AddEvent(GameEvents.EventType.OnLevelUp, ShakeOnLevelUp);
        GameManager.events.AddEvent(GameEvents.EventType.OnChefAttackRebound, PlayZoomOutSequence);
        GameManager.events.AddEvent(GameEvents.EventType.OnCraftAttackStarted, PlayZoomOutSequence);
    }

    private void LateUpdate()
    {
        if (isInZoomOutSequence)
            return;

        UpdateZoom();

        Vector3 desiredPosition = target.position;
        desiredPosition += new Vector3(
            offset.x,
            currentHeight + zoomKick,
            offset.z);

        // Prevent the camera from moving beyond the gameplay boundary.
        desiredPosition.z = Mathf.Max(desiredPosition.z, minimumCameraZ);

        // Add subtle procedural shake when active.
        if (currentShakeMagnitude > 0.0001f)
            desiredPosition += Random.insideUnitSphere * currentShakeMagnitude;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            followSmoothTime);

        UpdateBank();
    }

    /// <summary>
    /// Zooms out smoothly as the snake grows to keep more of the gameplay visible.
    /// </summary>
    private void UpdateZoom()
    {
        float t = Mathf.InverseLerp(
            minSnakeLength,
            maxSnakeLength,
            snake.Length);

        float targetHeight = Mathf.Lerp(
            minHeight,
            maxHeight,
            t);

        currentHeight = Mathf.Lerp(
            currentHeight,
            targetHeight,
            zoomSmoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Banks the camera opposite to the snake's turning direction
    /// to create a stronger sense of movement.
    /// </summary>
    private void UpdateBank()
    {
        Vector3 currentForward = target.forward;

        float turnAmount = Vector3.SignedAngle(
            lastTargetForward,
            currentForward,
            Vector3.up);

        lastTargetForward = currentForward;

        float targetBank = Mathf.Clamp(
            -turnAmount * bankTurnSensitivity,
            -maxBankAngle,
            maxBankAngle);

        currentBank = Mathf.Lerp(
            currentBank,
            targetBank,
            bankSmoothSpeed * Time.deltaTime);

        Vector3 rotation = transform.eulerAngles;
        rotation.z = currentBank;

        transform.rotation = Quaternion.Euler(rotation);
    }
    
    /// <summary>
    /// Moves the camera to cameraZoomOutTarget's position/rotation, holds for a
    /// moment, then resumes normal player-following behaviour.
    /// </summary>
    public void PlayZoomOutSequence()
    {
        if (cameraZoomOutTarget == null)
        {
            Debug.LogWarning($"{nameof(CameraController)}: cameraZoomOutTarget is not assigned.");
            return;
        }

        zoomOutSequence?.Kill();
        shakeTween?.Kill();
        zoomKickSequence?.Kill();

        isInZoomOutSequence = true;
        velocity = Vector3.zero;

        zoomOutSequence = DOTween.Sequence()
            .Append(transform.DOMove(cameraZoomOutTarget.position, zoomOutTransitionDuration).SetEase(zoomOutEase))
            .Join(transform.DORotateQuaternion(cameraZoomOutTarget.rotation, zoomOutTransitionDuration).SetEase(zoomOutEase))
            .AppendInterval(zoomOutHoldDuration)
            .OnComplete(ResumeFollowing);
    }

    private void ResumeFollowing()
    {
        isInZoomOutSequence = false;
        velocity = Vector3.zero;

        // Restore the camera's original pitch/yaw; only z (bank) should
        // remain dynamic, and UpdateBank will take over from here.
        currentBank = 0f;
        lastTargetForward = target.forward;
        transform.rotation = Quaternion.Euler(baseRotationEuler.x, baseRotationEuler.y, 0f);
    }

    /// <summary>
    /// Plays a small camera shake and zoom kick when food is eaten.
    /// </summary>
    public void ShakeOnEat()
    {
        Shake(eatShakeStrength, eatShakeDuration);
        ZoomKick();
    }

    /// <summary>
    /// Plays a stronger camera shake during level up.
    /// </summary>
    public void ShakeOnLevelUp()
    {
        Shake(levelUpShakeStrength, levelUpShakeDuration);
    }

    private void Shake(float strength, float duration)
    {
        shakeTween?.Kill();

        currentShakeMagnitude = strength;

        shakeTween = DOTween.To(
                () => currentShakeMagnitude,
                x => currentShakeMagnitude = x,
                0f,
                duration)
            .SetEase(Ease.OutQuad);
    }

    private void ZoomKick()
    {
        zoomKickSequence?.Kill();

        zoomKick = 0f;

        zoomKickSequence = DOTween.Sequence()
            .Append(
                DOTween.To(
                    () => zoomKick,
                    x => zoomKick = x,
                    zoomKickAmount,
                    zoomKickDuration * 0.4f)
                .SetEase(Ease.OutQuad))
            .Append(
                DOTween.To(
                    () => zoomKick,
                    x => zoomKick = x,
                    0f,
                    zoomKickDuration * 0.6f)
                .SetEase(Ease.OutElastic));
    }
    
    private void OnDisable()
    {
        GameManager.events.RemoveEvent(GameEvents.EventType.OnAteFood, ShakeOnEat);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnLevelUp, ShakeOnLevelUp);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnChefAttackRebound, PlayZoomOutSequence);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnCraftAttackStarted, PlayZoomOutSequence);
    }

    private void OnDestroy()
    {
        shakeTween?.Kill();
        zoomKickSequence?.Kill();
        zoomOutSequence?.Kill();
    }
}