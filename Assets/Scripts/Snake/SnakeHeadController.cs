using System;
using System.Collections;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

[RequireComponent(typeof(CharacterController))]
public class SnakeHeadController : MonoBehaviour
{
    SnakeInputs snakeInputs;
    [SerializeField] Animator characterAnimator;
    [SerializeField] private GameObject playerHeadVisual;
    
    [SerializeField] private SnakeBodyController snakeBodyController;
    [SerializeField] private FoodSpawner foodSpawner;
    
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 15f;

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
    
    bool isMoving = false;

    private Tweener bendTween;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        
        snakeInputs  = new SnakeInputs();
    }

    private void OnEnable()
    {
        snakeInputs.Enable();
        snakeInputs.Snake.Turn.performed += OnTurn;
        snakeInputs.Snake.Turn.canceled += OnTurn;
    }

    private void OnDisable()
    {
        snakeInputs.Snake.Turn.performed -= OnTurn;
        snakeInputs.Snake.Turn.canceled -= OnTurn;
        snakeInputs.Disable();

        bendTween?.Kill();
    }

    private void Start()
    {
        origionalSpeed = moveSpeed;
        StartCoroutine(EnableCollision());
    }

    bool canCollide = false;
    bool isGameStarted = false;
    IEnumerator EnableCollision()
    {
        characterAnimator.speed = 0;
        yield return new WaitForSeconds(0.5f);
        characterAnimator.speed = 1;
        canCollide = true;
        isGameStarted = true;
    }

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

    private void Update()
    {
        if(isGameOver || !isGameStarted) return;
        
        Rotate();
        MoveForward();
    }
    
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
    
    public void GameOver()
    {
        if (isGameOver)
            return;
        
        Debug.Log("Game Over");
        SFXManager.instance.PlayGameOver();
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

    private void OnTriggerEnter(Collider other)
    {
        if(isGameOver || !canCollide)  return;
        
        if (other.CompareTag("Food"))
        {
            SnakeSegment segment = other.GetComponent<SnakeSegment>();
            if (segment != null)
            {
                if (segment.IsAttached())
                {
                    if(canIgnoreBodySegment)
                        return;
                    
                    GameOver();
                }
                else
                {
                    SFXManager.instance.PlayEat();

                    if (snakeBodyController.IsTheTargetFood(segment.FoodType))
                    {
                        characterAnimator.SetTrigger("Jump");
                        FTUEController.Instance.OnFoodCollected(true);
                        Debug.Log("Removing food");
                        snakeBodyController.RemoveSegment();
                        foodSpawner.SpawnRandomFood();
                        Destroy(other.gameObject);
                        //transform.DOPunchScale(Vector3.one*0.2f,0.2f);
                    }
                    else
                    {
                        FTUEController.Instance.OnFoodCollected(false);
                        Debug.Log("Adding food");
                        snakeBodyController.AddEatenSegment(segment);
                        Destroy(other.gameObject);
                        //transform.DOPunchScale(Vector3.one*0.2f,0.2f);
                    }
                }
            }
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
        {
            GameOver();
            snakeBodyController.TriggerGameOver();
            Debug.Log("Obstacle - ", other.gameObject);
        }
    }

    #region Chaos

    private float origionalSpeed;
    private bool canIgnoreBodySegment = false;
    private bool inverse = false;
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
}