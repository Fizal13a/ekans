using DG.Tweening;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private SnakeBodyController snake;

    [Header("Follow")]
    [SerializeField] private Vector3 offset = new Vector3(0, 15, -12);
    [SerializeField] private float followSmoothTime = 0.2f;

    [Header("Zoom")]
    [SerializeField] private float minHeight = 15f;
    [SerializeField] private float maxHeight = 30f;
    [SerializeField] private int minSnakeLength = 5;
    [SerializeField] private int maxSnakeLength = 30;
    [SerializeField] private float zoomSmoothSpeed = 5f;

    [Header("Polish / Juice - Shake")]
    [SerializeField] private float eatShakeStrength = 0.15f;
    [SerializeField] private float eatShakeDuration = 0.2f;
    [SerializeField] private float levelUpShakeStrength = 0.5f;
    [SerializeField] private float levelUpShakeDuration = 0.4f;

    [Header("Polish / Juice - Zoom Kick")]
    [SerializeField] private float zoomKickAmount = -1.5f;
    [SerializeField] private float zoomKickDuration = 0.25f;

    [Header("Polish / Juice - Bank / Tilt")]
    [SerializeField] private float maxBankAngle = 8f;
    [SerializeField] private float bankTurnSensitivity = 8f;
    [SerializeField] private float bankSmoothSpeed = 4f;

    private Vector3 velocity;
    private float currentHeight;

    private float currentShakeMagnitude;
    private Tweener shakeTween;

    private float zoomKick;
    private Sequence zoomKickSequence;

    private Vector3 lastTargetForward;
    private float currentBank;

    private void Start()
    {
        currentHeight = minHeight;
        lastTargetForward = target.forward;
    }

    private void LateUpdate()
    {
        UpdateZoom();

        Vector3 desiredPosition = target.position;
        desiredPosition += new Vector3(
            offset.x,
            currentHeight + zoomKick,
            offset.z);
        
        desiredPosition.z = Mathf.Max(desiredPosition.z, -30f);

        if (currentShakeMagnitude > 0.0001f)
            desiredPosition += Random.insideUnitSphere * currentShakeMagnitude;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            followSmoothTime);

        UpdateBank();

        //transform.LookAt(target);
    }

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

    private void UpdateBank()
    {
        // Subtle roll as the snake turns, so the camera feels like it's riding along rather than rigidly locked
        Vector3 currentForward = target.forward;
        float turnAmount = Vector3.SignedAngle(lastTargetForward, currentForward, Vector3.up);
        lastTargetForward = currentForward;

        float targetBank = Mathf.Clamp(-turnAmount * bankTurnSensitivity, -maxBankAngle, maxBankAngle);
        currentBank = Mathf.Lerp(currentBank, targetBank, bankSmoothSpeed * Time.deltaTime);

        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(euler.x, euler.y, currentBank);
    }

    /// <summary>Call this whenever food is eaten for a quick shake + zoom punch.</summary>
    public void ShakeOnEat()
    {
        Shake(eatShakeStrength, eatShakeDuration);
        ZoomKick();
    }

    /// <summary>Call this on level-up for a bigger, more dramatic shake.</summary>
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
            .Append(DOTween.To(() => zoomKick, x => zoomKick = x, zoomKickAmount, zoomKickDuration * 0.4f).SetEase(Ease.OutQuad))
            .Append(DOTween.To(() => zoomKick, x => zoomKick = x, 0f, zoomKickDuration * 0.6f).SetEase(Ease.OutElastic));
    }

    private void OnDestroy()
    {
        shakeTween?.Kill();
        zoomKickSequence?.Kill();
    }
}