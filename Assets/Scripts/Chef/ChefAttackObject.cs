using System;
using System.Collections;
using UnityEngine;

public class ChefAttackObject : MonoBehaviour
{
    [SerializeField] private float duration = 0.4f;
    [SerializeField] private float arcHeight = 2f;
    
    private bool isRebounding = false;

    private void OnEnable()
    {
        GameManager.events.AddEvent<ReboundEventData>(
            GameEvents.EventType.OnChefAttackRebound,
            ReboundObject
        );
    }

    private void OnDisable()
    {
        GameManager.events.RemoveEvent<ReboundEventData>(
            GameEvents.EventType.OnChefAttackRebound,
            ReboundObject
        );
    }

    public void ReboundObject(ReboundEventData reboundEventData)
    {
        if (reboundEventData.chefAttackObject != this)
            return;

        StartCoroutine(MoveToTarget(reboundEventData));
    }

    private IEnumerator MoveToTarget(ReboundEventData data)
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = data.targetTransform.position;

        // Point above the middle of the path
        Vector3 controlPoint = (startPos + endPos) * 0.5f;
        controlPoint.y += arcHeight;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth movement
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Quadratic Bezier
            Vector3 position =
                Mathf.Pow(1 - smoothT, 2) * startPos +
                2 * (1 - smoothT) * smoothT * controlPoint +
                Mathf.Pow(smoothT, 2) * endPos;

            transform.position = position;

            yield return null;
        }

        transform.position = endPos;

        Debug.Log("Target Hit!");
    }

    public void SetRebounding(bool value)
    {
        isRebounding = value;
    }

    public bool IsRebounding()
    {
        return isRebounding;
    }
}