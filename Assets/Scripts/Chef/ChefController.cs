using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ChefController : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float minAttackDelay = 3f;
    [SerializeField] private float maxAttackDelay = 6f;
    private bool isAttacking = false;
    
    private List<IChefAttack> attacks = new List<IChefAttack>();

    private Coroutine attackRoutine;

    private void Awake()
    {
        IChefAttack[] chefAttacks = GetComponents<IChefAttack>();

        foreach (IChefAttack attack in chefAttacks)
        {
            attacks.Add(attack);
        }
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent(GameEvents.EventType.OnAttackFinished, OnAttackFinished);
    }

    private void Start()
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

        IChefAttack attack = attacks[Random.Range(0, attacks.Count)];

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

    private void OnDestroy()
    {
        GameManager.events.RemoveEvent(GameEvents.EventType.OnAttackFinished, OnAttackFinished);
    }
}