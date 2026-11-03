using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public enum SnakeMovementMode
{
    Free,   // WASD free movement (default)
    Auto    // Auto-forward, player only turns left/right (fights)
}

[RequireComponent(typeof(CharacterController))]
public class SnakeHeadController : MonoBehaviour
{
    [Header("References")]
    SnakeInputs snakeInputs;
    [SerializeField] Animator characterAnimator;
    [SerializeField] private GameObject playerHeadVisual;
    [Tooltip("Free move is relative to this. Falls back to Camera.main if empty.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Bool")]
    [SerializeField] bool isGameStarted = false;
    bool isMoving = false;
    bool canCollide = false;

    [Header("Movement Mode")]
    [SerializeField] private SnakeMovementMode startingMode = SnakeMovementMode.Free;
    public SnakeMovementMode CurrentMode { get; private set; }
    private Vector2 moveInput;

    [Header("Free Move Smoothing")]
    [SerializeField] private float acceleration = 25f;       // units/sec² when input is held
    [SerializeField] private float deceleration = 35f;       // units/sec² when input is released
    [SerializeField] private float freeRotationSpeed = 540f; // degrees/sec toward move direction

    [Header("Free Move Tilt")]
    [SerializeField] private float freeTiltAngle = 18f;          // max lean
    [SerializeField] private float maxYawRateForFullTilt = 360f; // deg/sec of turning that gives full lean
    [SerializeField] private float tiltSmoothTime = 0.12f;       // higher = floatier lean
    [SerializeField] private float animatorTurnThreshold = 30f;  // deg/sec before MoveLeft/MoveRight triggers

    private Vector3 planarVelocity;
    private float freeTilt;
    private float tiltVelocity;

    [Header("Gravity")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float gravityRampDuration = 0.6f;
    [SerializeField] private AnimationCurve gravityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private float gravityIncreaseSpeed = 10f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float airControl = 0.5f;

    private float gravityProgress;
    private float fallTime;
    private bool wasGrounded;
    private Vector3 airMomentum;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 15f; // used in Auto mode
    private bool canMove = true;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private List<TrailRenderer> dashTrailRenderers;

    private bool isDashing;
    private bool canDash = true;

    [Header("Power Up")]
    private float origionalSpeed;
    private bool canIgnoreBodySegment = false;
    private bool inverse = false;

    [Header("VFX")]
    public List<ParticleSystem> onAteParticles;
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField] private ParticleSystem levelUpParticle;

    [Header("Turn Bend / Juice")]
    [SerializeField] private float bendAngle = 18f;
    [SerializeField] private float bendDuration = 0.2f;
    [SerializeField] private Ease bendEase = Ease.OutQuad;

    [Header("Game Over")]
    private bool isGameOver;
    [SerializeField] private float headDropAmount = 0.4f;
    [SerializeField] private float headFallDuration = 0.6f;
    [SerializeField] private Ease headFallEase = Ease.OutBounce;

    private float turnInput;
    private float verticalVelocity;
    private float currentGravity;

    private CharacterController controller;

    public Vector3 CurrentVelocity { get; private set; }

    public static event Action OnAteAFood;

    private Tweener bendTween;

    #region Initialize

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        snakeInputs = new SnakeInputs();
        CurrentMode = startingMode;
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent(GameEvents.EventType.OnGameStart, InitializeHead);
        GameManager.events.AddEvent(GameEvents.EventType.OnGameOver, GameOver);
        GameManager.events.AddEvent<ChaosType>(GameEvents.EventType.OnPowerUpSelected, OnPowerUpSelected);
        GameManager.events.AddEvent(GameEvents.EventType.OnPowerUpCompleted, ResetAll);
        GameManager.events.AddEvent(GameEvents.EventType.OnLevelUp, OnLevelUp);
        GameManager.events.AddEvent(GameEvents.EventType.OnAteFood, OnAteFood);
        GameManager.events.AddEvent(GameEvents.EventType.OnAteRightFood, OnAteRightFood);
        GameManager.events.AddEvent(GameEvents.EventType.OnSpecialAttackTrigger, OnSpecialAttack);
        GameManager.events.AddEvent(GameEvents.EventType.OnCraftAttackStarted, OnSpecialAttack);
        GameManager.events.AddEvent(GameEvents.EventType.OnSpecialAttackCompleted, OnSpecialAttackFinished);
        GameManager.events.AddEvent(GameEvents.EventType.OnCraftAttackCompleted, OnSpecialAttackFinished);

        snakeInputs.Enable();
        snakeInputs.Snake.Move.performed += OnMovement;
        snakeInputs.Snake.Move.canceled += OnMovement;
        snakeInputs.Snake.Turn.performed += OnTurn;
        snakeInputs.Snake.Turn.canceled += OnTurn;
        snakeInputs.Snake.Dash.performed += OnDash;
    }

    private void InitializeHead()
    {
        origionalSpeed = moveSpeed;
        canMove = true;
        StartCoroutine(EnableCollision());
    }

    IEnumerator EnableCollision()
    {
        characterAnimator.speed = 0;
        yield return new WaitForSeconds(0.5f);
        characterAnimator.speed = 1;
        canCollide = true;
        isGameStarted = true;
    }

    #endregion

    #region Movement Mode

    /// <summary>Call this when a fight starts/ends (e.g. SetMovementMode(SnakeMovementMode.Auto)).</summary>
    public void SetMovementMode(SnakeMovementMode mode)
    {
        if (CurrentMode == mode)
            return;

        CurrentMode = mode;

        turnInput = 0f;
        isMoving = false;
        characterAnimator.SetBool("MoveRight", false);
        characterAnimator.SetBool("MoveLeft", false);

        if (mode == SnakeMovementMode.Free)
        {
            // Hand the head visual over from the DOTween bend to the per-frame tilt
            bendTween?.Kill();
            if (playerHeadVisual != null)
                freeTilt = Mathf.DeltaAngle(0f, playerHeadVisual.transform.localEulerAngles.z);
            tiltVelocity = 0f;
        }
        else
        {
            AnimateHeadBend();

            if (!isGameOver)
                ApplyTurnInput(snakeInputs.Snake.Turn.ReadValue<float>());
        }
    }

    public void SetAutoMove()  => SetMovementMode(SnakeMovementMode.Auto);
    public void SetFreeMove()  => SetMovementMode(SnakeMovementMode.Free);

    #endregion

    #region Inputs

    // WASD (Vector2) - only used in Free mode
    private void OnMovement(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    // Left/Right turn - only used in Auto mode
    private void OnTurn(InputAction.CallbackContext ctx)
    {
        if (isGameOver || CurrentMode != SnakeMovementMode.Auto)
            return;

        ApplyTurnInput(ctx.ReadValue<float>());
    }

    private void ApplyTurnInput(float rawTurn)
    {
        turnInput = rawTurn;
        isMoving = turnInput != 0;

        if (inverse)
            turnInput *= -1f;

        if (isMoving)
        {
            characterAnimator.SetBool("MoveRight", turnInput > 0);
            characterAnimator.SetBool("MoveLeft", turnInput < 0);
        }
        else
        {
            characterAnimator.SetBool("MoveRight", false);
            characterAnimator.SetBool("MoveLeft", false);
        }

        AnimateHeadBend();
    }

    private void OnDash(InputAction.CallbackContext obj)
    {
        Vector3 dir = transform.forward;

        // In free mode, dash where the player is pointing, not where the snake is still turning
        if (CurrentMode == SnakeMovementMode.Free)
        {
            Vector3 inputDir = GetFreeMoveDirection();
            if (inputDir.sqrMagnitude > 0.01f)
                dir = inputDir;
        }

        Dash(dir);
    }

    #endregion

    #region Animations

    private void AnimateHeadBend()
    {
        if (playerHeadVisual == null)
            return;

        float targetBend = -turnInput * bendAngle;

        bendTween?.Kill();
        bendTween = playerHeadVisual.transform
            .DOLocalRotate(new Vector3(0f, 0f, targetBend), bendDuration)
            .SetEase(bendEase);
    }

    private void OnAteRightFood()
    {
        playerHeadVisual.transform.DOKill(true);
        playerHeadVisual.transform.localScale = Vector3.one;
        playerHeadVisual.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 6, 0.7f);
    }

    #endregion

    #region Update

    private void Update()
    {
        if (isGameOver || !isGameStarted || !canMove) return;

        HandleGravity();

        if (CurrentMode == SnakeMovementMode.Auto)
        {
            Rotate();
            MoveForward();
            planarVelocity = transform.forward * moveSpeed; // keeps Auto -> Free handoff smooth
        }
        else
        {
            FreeMove();
        }
    }

    #endregion

    #region Actions

    private void HandleGravity()
    {
        bool isGrounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            groundCheckDistance,
            groundLayer
        );

        if (isGrounded)
        {
            gravityProgress = 0f;
            currentGravity = 0f;

            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
        }
        else
        {
            float rampDuration = Mathf.Abs(gravity) / gravityIncreaseSpeed;
            gravityProgress = Mathf.MoveTowards(gravityProgress, 1f, Time.deltaTime / rampDuration);

            currentGravity = gravity * gravityCurve.Evaluate(gravityProgress);

            verticalVelocity += currentGravity * Time.deltaTime;
        }

        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    // ---- Auto mode ----
    private void Rotate()
    {
        transform.Rotate(
            Vector3.up,
            turnInput * rotationSpeed * Time.deltaTime);
    }

    private void MoveForward()
    {
        controller.Move(
            transform.forward *
            moveSpeed *
            Time.deltaTime);
    }

    // ---- Free mode ----
    private Vector3 GetFreeMoveDirection()
    {
        Vector2 input = moveInput;
        if (inverse)
            input *= -1f;

        if (input.sqrMagnitude < 0.01f)
            return Vector3.zero;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right   = cameraTransform != null ? cameraTransform.right   : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 dir = forward * input.y + right * input.x;
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    private void FreeMove()
    {
        Vector3 dir = GetFreeMoveDirection();
        bool hasInput = dir.sqrMagnitude > 0.01f;
        isMoving = hasInput;

        // Smooth accelerate / decelerate toward the target velocity
        Vector3 targetVelocity = dir * moveSpeed;
        float rate = hasInput ? acceleration : deceleration;
        planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, rate * Time.deltaTime);

        // Rotate toward input; keep the last facing when input is released
        float yawBefore = transform.eulerAngles.y;
        if (hasInput)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, freeRotationSpeed * Time.deltaTime);
        }
        float yawRate = Mathf.DeltaAngle(yawBefore, transform.eulerAngles.y) / Time.deltaTime;

        if (planarVelocity.sqrMagnitude > 0.0001f)
            controller.Move(planarVelocity * Time.deltaTime);

        CurrentVelocity = planarVelocity;
        UpdateFreeTilt(yawRate);
    }

    private void UpdateFreeTilt(float yawRate)
    {
        // No lean when standing still
        float speed01 = moveSpeed > 0f ? Mathf.Clamp01(planarVelocity.magnitude / moveSpeed) : 0f;

        // Same sign convention as AnimateHeadBend: turning right leans the opposite way
        float targetTilt = -Mathf.Clamp(yawRate / maxYawRateForFullTilt, -1f, 1f)
                           * freeTiltAngle * speed01;

        freeTilt = Mathf.SmoothDamp(freeTilt, targetTilt, ref tiltVelocity, tiltSmoothTime);

        if (playerHeadVisual != null)
            playerHeadVisual.transform.localRotation = Quaternion.Euler(0f, 0f, freeTilt);

        // Reuse the same turn animations as Auto mode
        bool turningRight = speed01 > 0.1f && yawRate > animatorTurnThreshold;
        bool turningLeft  = speed01 > 0.1f && yawRate < -animatorTurnThreshold;
        characterAnimator.SetBool("MoveRight", turningRight);
        characterAnimator.SetBool("MoveLeft", turningLeft);
    }

    private void OnCorrectFoodAte()
    {
        characterAnimator.SetTrigger("Jump");
    }

    private void OnLevelUp()
    {
        levelUpParticle.Play();
    }

    private void OnAteFood()
    {
        int randomParticle = Random.Range(0, onAteParticles.Count);
        onAteParticles[randomParticle].Play();
    }

    private void OnWrongFoodAte()
    {

    }

    private void OnSpecialAttack()
    {
        canCollide = false;
        canMove = false;
        planarVelocity = Vector3.zero;
    }

    private void OnSpecialAttackFinished()
    {
        canCollide = true;
        canMove = true;
    }

    public void Dash(Vector3 direction)
    {
        if (isDashing || !canDash)
            return;

        if (direction.sqrMagnitude <= 0.01f)
            direction = transform.forward;

        GameManager.events.TriggerEvent(GameEvents.EventType.OnDash);
        StartCoroutine(DashRoutine(direction.normalized));
    }

    private IEnumerator DashRoutine(Vector3 direction)
    {
        isDashing = true;
        canDash = false;

        foreach (var tr in dashTrailRenderers)
        {
            tr.emitting = true;
        }

        float elapsed = 0f;
        float dashSpeed = dashDistance / dashDuration;

        while (elapsed < dashDuration)
        {
            controller.Move(direction * dashSpeed * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        isDashing = false;

        foreach (var tr in dashTrailRenderers)
        {
            tr.emitting = false;
        }

        yield return new WaitForSeconds(dashCooldown);

        canDash = true;
    }

    #endregion

    #region GameOver

    public void GameOver()
    {
        if (isGameOver)
            return;

        hitParticles.Play();
        canCollide = false;
        characterAnimator.SetBool("GameOver", true);

        isGameOver = true;
        isMoving = false;
        turnInput = 0f;
        moveInput = Vector2.zero;
        planarVelocity = Vector3.zero;

        bendTween?.Kill();
        controller.enabled = false;

        Transform fallTarget = playerHeadVisual != null ? playerHeadVisual.transform : transform;

        Vector3 fallAxis = Vector3.forward;
        float fallAngle = Random.value > 0.5f ? 90f : -90f;

        DOTween.Sequence()
            .Append(fallTarget.DOMove(fallTarget.position + Vector3.down * headDropAmount, headFallDuration).SetEase(Ease.OutQuad))
            .Join(fallTarget.DORotate(fallAxis * fallAngle, headFallDuration, RotateMode.WorldAxisAdd).SetEase(headFallEase));
    }

    #endregion

    #region Collision

    private void OnTriggerEnter(Collider other)
    {
        if (isGameOver || !canCollide) return;

        if (other.CompareTag("Food"))
        {
            if (other.TryGetComponent(out SnakeSegment segment))
                GameManager.events.TriggerEvent<SnakeSegment>(GameEvents.EventType.OnHeadTouchedFood, segment);
            return;
        }

        if (other.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
            return;

        if (other.CompareTag("ChefAttack"))
        {
            ChefAttackObject attackObject = other.GetComponent<ChefAttackObject>();

            if (attackObject == null)
            {
                GameManager.events.TriggerEvent(GameEvents.EventType.OnGameOver);
                return;
            }

            if (attackObject.IsRebounding())
                return;

            if (isDashing)
            {
                attackObject.SetRebounding(true);

                GameManager.events.TriggerEvent(
                    GameEvents.EventType.OnChefAttackRebound,
                    new ReboundEventData(
                        attackObject,
                        GameManager.Instance.GetBossTransform()
                    )
                );

                Debug.Log("Rebound - " + other.gameObject.name);
                return;
            }

            GameManager.events.TriggerEvent(GameEvents.EventType.OnGameOver);
            Debug.Log("Attack Object → Game Over");
            return;
        }

        GameManager.events.TriggerEvent(GameEvents.EventType.OnGameOver);
        Debug.Log("Obstacle → Game Over");
    }

    #endregion

    #region Power Ups

    private void OnPowerUpSelected(ChaosType type)
    {
        switch (type)
        {
            case ChaosType.Fast:
                SpeedChange(2);
                break;
            case ChaosType.Slow:
                SpeedChange(-2);
                break;
            case ChaosType.PassThrough:
                PassThrough();
                break;
            case ChaosType.Inverse:
                Inverse();
                break;
        }
    }

    public void SpeedChange(float speed)
    {
        moveSpeed += speed;

        characterAnimator.speed = speed > 0 ? 1.5f : 0.5f;
    }

    public void ResetAll()
    {
        characterAnimator.speed = 1f;
        moveSpeed = origionalSpeed;
        canIgnoreBodySegment = false;
        inverse = false;
    }

    public void PassThrough()
    {
        canIgnoreBodySegment = true;
    }

    public void Inverse()
    {
        inverse = true;
    }

    #endregion

    #region Terminate

    private void OnDisable()
    {
        snakeInputs.Snake.Move.performed -= OnMovement;
        snakeInputs.Snake.Move.canceled -= OnMovement;
        snakeInputs.Snake.Turn.performed -= OnTurn;
        snakeInputs.Snake.Turn.canceled -= OnTurn;
        snakeInputs.Snake.Dash.performed -= OnDash;

        snakeInputs.Disable();

        GameManager.events.RemoveEvent(GameEvents.EventType.OnGameStart, InitializeHead);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnGameOver, GameOver);
        GameManager.events.RemoveEvent<ChaosType>(GameEvents.EventType.OnPowerUpSelected, OnPowerUpSelected);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnPowerUpCompleted, ResetAll);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnLevelUp, OnLevelUp);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnAteFood, OnAteFood);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnAteRightFood, OnAteRightFood);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSpecialAttackTrigger, OnSpecialAttack);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnCraftAttackStarted, OnSpecialAttack);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSpecialAttackCompleted, OnSpecialAttackFinished);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnCraftAttackCompleted, OnSpecialAttackFinished);

        bendTween?.Kill();
    }

    #endregion
}