using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

[RequireComponent(typeof(CharacterController))]
public class SnakeHeadController : MonoBehaviour
{
    [Header("References")]
    SnakeInputs snakeInputs;
    [SerializeField] Animator characterAnimator;
    [SerializeField] private GameObject playerHeadVisual;
    [SerializeField] private SnakeBodyController snakeBodyController;
    [SerializeField] private FoodSpawner foodSpawner;
    
    [Header("Bool")]
    bool isMoving = false;
    bool canCollide = false;
    bool isGameStarted = false;
    
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 15f;
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
    
    private CharacterController controller;

    public Vector3 CurrentVelocity { get; private set; }
    
    public static event Action OnAteAFood;
    

    private Tweener bendTween;

    #region Initialize

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        snakeInputs  = new SnakeInputs();
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent(GameEvents.EventType.OnGameStart, InitializeHead);
        GameManager.events.AddEvent(GameEvents.EventType.OnGameOver, GameOver);
        GameManager.events.AddEvent<ChaosType>(GameEvents.EventType.OnPowerUpSelected, OnPowerUpSelected);
        GameManager.events.AddEvent(GameEvents.EventType.OnPowerUpCompleted, ResetAll);
        GameManager.events.AddEvent(GameEvents.EventType.OnLevelUp, OnLevelUp);
        GameManager.events.AddEvent(GameEvents.EventType.OnAteFood, OnAteFood);
        GameManager.events.AddEvent(GameEvents.EventType.OnSpecialAttackTrigger, OnSpecialAttack);
        GameManager.events.AddEvent(GameEvents.EventType.OnCraftAttackStarted, OnSpecialAttack);
        GameManager.events.AddEvent(GameEvents.EventType.OnSpecialAttackCompleted, OnSpecialAttackFinished);
        GameManager.events.AddEvent(GameEvents.EventType.OnCraftAttackCompleted, OnSpecialAttackFinished);

        snakeInputs.Enable();
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

    #region Inputs

    private void OnTurn(InputAction.CallbackContext ctx)
    {
        if(isGameOver) return;
        
        turnInput = ctx.ReadValue<float>();
        isMoving = turnInput != 0;
        
        if (inverse)
            turnInput *= -1f;

        if (isMoving)
        {
            if (turnInput > 0)
            {
                characterAnimator.SetBool("MoveRight", true);
                characterAnimator.SetBool("MoveLeft", false);
            }
            else
            {
                characterAnimator.SetBool("MoveRight", false);
                characterAnimator.SetBool("MoveLeft", true);
            }
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
        Dash(transform.forward);
    }

    #endregion

    #region Animations

    private void AnimateHeadBend()
    {
        if (playerHeadVisual == null)
            return;

        // Negative turnInput (left) bends one way, positive (right) the other.
        // Bend returns to 0 automatically when turnInput is 0 (straight again).
        float targetBend = -turnInput * bendAngle;

        bendTween?.Kill();
        bendTween = playerHeadVisual.transform
            .DOLocalRotate(new Vector3(0f, 0f, targetBend), bendDuration)
            .SetEase(bendEase);
    }

    #endregion

    #region Update

    private void Update()
    {
        if(isGameOver || !isGameStarted || !canMove) return;
        
        Rotate();
        MoveForward();
    }

    #endregion

    #region Actions

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
        int randomParticle =  Random.Range(0, onAteParticles.Count);
        onAteParticles[randomParticle].Play();
    }
    
    private void OnWrongFoodAte()
    {
        
    }

    private void OnSpecialAttack()
    {
        canCollide = false;
        canMove  = false;
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
 
        bendTween?.Kill();
        controller.enabled = false; // stop CharacterController from resolving further movement/collisions
 
        Transform fallTarget = playerHeadVisual != null ? playerHeadVisual.transform : transform;
 
        // Topple onto a random side: pick a random world axis to rotate around
        // and a random direction, so it doesn't always fall the same way.
        Vector3 fallAxis = Vector3.forward;
        float fallAngle = Random.value > 0.5f ? 90f : -90f;
 
        DOTween.Sequence()
            .Append(fallTarget.DOMove(fallTarget.position + Vector3.down * headDropAmount, headFallDuration).SetEase(Ease.OutQuad))
            .Join(fallTarget.DORotate(fallAxis * fallAngle, headFallDuration, RotateMode.WorldAxisAdd).SetEase(headFallEase));
 
        snakeBodyController.TriggerGameOver();
    }

    #endregion

    #region Collision

    private void OnTriggerEnter(Collider other)
    {
        if(isGameOver || !canCollide)  return;
        
        if (other.CompareTag("Food"))
        {
            SnakeSegment segment = other.GetComponent<SnakeSegment>();
            if (segment != null)
            {
                if (segment.IsAttached() && segment.FoodType != snakeBodyController.TargetFood)
                {
                    if(canIgnoreBodySegment)
                        return;
                    
                    GameManager.events.TriggerEvent(GameEvents.EventType.OnGameOver);
                    Debug.Log("Collided with body segment");
                }
                else if(!segment.IsAttached())
                {
                    GameManager.events.TriggerEvent<SnakeSegment>(GameEvents.EventType.OnAteFood, segment);
                    Destroy(other.gameObject);
                }
            }
        }

        if (other.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
            return;

        if (other.CompareTag("ChefAttack"))
        {
            ChefAttackObject attackObject =
                other.GetComponent<ChefAttackObject>();

            if (attackObject == null)
            {
                GameManager.events.TriggerEvent(GameEvents.EventType.OnGameOver);
                return;
            }

            // Already rebounding → ignore collision
            if (attackObject.IsRebounding())
            {
                return;
            }

            // Attack object + dashing → rebound
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

            // Attack object + NOT dashing → Game Over
            GameManager.events.TriggerEvent(GameEvents.EventType.OnGameOver);

            Debug.Log("Attack Object → Game Over");

            return;
        }

        // Normal obstacle → Game Over
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

        if (speed > 0)
        {
            characterAnimator.speed = 1.5f;
        }
        else
        {
            characterAnimator.speed = 0.5f;
        }
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
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSpecialAttackTrigger, OnSpecialAttack);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSpecialAttackCompleted, OnSpecialAttackFinished);

        bendTween?.Kill();
    }

    #endregion
}