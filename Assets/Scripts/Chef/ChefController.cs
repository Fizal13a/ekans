using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ChefController : MonoBehaviour
{
    Animator animator;
    
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    [SerializeField] private int middleHealth;
    [SerializeField] private int lowHealth;
    [SerializeField] private Transform chefHitPoint;
    [SerializeField] private ParticleSystem chefHitParticle;
    
    [Header("Attack Settings")]
    [SerializeField] private float minAttackDelay = 3f;
    [SerializeField] private float maxAttackDelay = 6f;
    private bool isAttacking = false;
    
    private List<IChefAttack> attacks = new List<IChefAttack>();
    private List<IChefAttack> availableAttacks = new List<IChefAttack>();

    private Coroutine attackRoutine;
    
    #region Initialization
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
        IChefAttack[] chefAttacks = GetComponents<IChefAttack>();

        foreach (IChefAttack attack in chefAttacks)
        {
            attacks.Add(attack);
        }

        foreach (var attack in attacks)
        {
            if (attack.AttackType == BossAttackType.LightAttack)
            {
                availableAttacks.Add(attack);
            }
        }
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent(GameEvents.EventType.OnAttackFinished, OnAttackFinished);
        GameManager.events.AddEvent<int>(GameEvents.EventType.OnChecfGotAttacked, ReduceHealth);
        GameManager.events.AddEvent(GameEvents.EventType.OnSpecialAttackTrigger, StopAttacking);
        GameManager.events.AddEvent(GameEvents.EventType.OnSpecialAttackCompleted, StartAttacking);
    }

    private void Start()
    {
        GameManager.Instance.SetBossTransform(chefHitPoint);
        StartAttacking();
        currentHealth = maxHealth;
    }
    
    #endregion

    #region Attack

    private void StartAttacking()
    {
        attackRoutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            float delay = Random.Range(minAttackDelay, maxAttackDelay);

            yield return new WaitForSeconds(delay);

            StartRandomAttack();

            yield return new WaitUntil(() => !isAttacking);
        }
    }

    private void StartRandomAttack()
    {
        if (attacks.Count == 0)
            return;

        isAttacking = true;

        IChefAttack attack = availableAttacks[Random.Range(0, availableAttacks.Count)];

        attack.StartAttack();
    }

    private void OnAttackFinished()
    {
        isAttacking = false;
    }

    private void StopAttacking()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine  = null;
        }
    }

    #endregion

    #region Health

    private void ReduceHealth(int amount)
    {
        chefHitParticle.Play();
        animator.SetTrigger("Hit");
        
        currentHealth -= amount;
        float healthValue = (float)currentHealth / maxHealth;
        GameManager.events.TriggerEvent(GameEvents.EventType.OnChefHealthReduced, healthValue);

        if (currentHealth <= middleHealth)
        {
            foreach (var attack in attacks)
            {
                if (attack.AttackType == BossAttackType.MidAttack)
                {
                    if(availableAttacks.Contains(attack)) break;
                    Debug.Log("Added new attack");
                    GameManager.events.TriggerEvent(GameEvents.EventType.OnChefMidHealthReduced, healthValue);
                    availableAttacks.Add(attack);
                }
            }
        }

        if (currentHealth <= lowHealth)
        {
            foreach (var attack in attacks)
            {
                if (attack.AttackType == BossAttackType.SpecialAttack)
                {
                    if(availableAttacks.Contains(attack)) break;
                    GameManager.events.TriggerEvent(GameEvents.EventType.OnChefLowHealthReduced, healthValue);
                    availableAttacks.Add(attack);
                }
            }
        }
            

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            StopAttacking();
            OnChefDead();
            GameManager.events.TriggerEvent(GameEvents.EventType.OnChefDead);
        }
    }

    private void OnChefDead()
    {
        
    }

    #endregion

    #region Collision

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("ChefAttack"))
        {
            ChefAttackObject chefAttackObject = other.gameObject.GetComponent<ChefAttackObject>();
            if (chefAttackObject != null)
            {
                if (chefAttackObject.IsRebounding())
                {
                    other.gameObject.SetActive(false);
                    Debug.Log("Chef Attacked!");
                    GameManager.events.TriggerEvent(GameEvents.EventType.OnChecfGotAttacked, 10);
                }
            }
        }
    }

    #endregion

    #region Terminate

    private void OnDestroy()
    {
        GameManager.events.RemoveEvent(GameEvents.EventType.OnAttackFinished, OnAttackFinished);
        GameManager.events.RemoveEvent<int>(GameEvents.EventType.OnChecfGotAttacked, ReduceHealth);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSpecialAttackTrigger, StopAttacking);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnSpecialAttackCompleted, StartAttacking);
    }

    #endregion
    
}