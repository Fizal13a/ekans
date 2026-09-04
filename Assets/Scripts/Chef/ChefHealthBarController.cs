using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ChefHealthBarController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image chefHealthBarImage;      // main white/foreground fill
    [SerializeField] private Image chefHealthBarRedImage;    // trailing red fill
    [SerializeField] private RectTransform healthBarContainer; // parent to punch-scale
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private Color normalColor = Color.green;

    [Header("Timing")]
    [SerializeField] private float punchDuration = 0.25f;
    [SerializeField] private float redDelay = 0.4f;
    [SerializeField] private float redDrainDuration = 0.5f;
    [SerializeField] private float flashDuration = 0.12f;

    [Header("Punch Settings")]
    [SerializeField] private float overshootAmount = 0.08f; // how far past target it dips
    [SerializeField] private float scalePunchAmount = 0.15f; // 15% scale kick

    private Coroutine healthCoroutine;
    private Coroutine flashCoroutine;
    private Coroutine scaleCoroutine;

    private void OnEnable()
    {
        GameManager.events.AddEvent<float>(GameEvents.EventType.OnChefHealthReduced, ChefHealthBar);
    }

    private void OnDisable()
    {
        GameManager.events.RemoveEvent<float>(GameEvents.EventType.OnChefHealthReduced, ChefHealthBar);
    }

    public void ChefHealthBar(float health)
    {
        health = Mathf.Clamp01(health);

        if (healthCoroutine != null) StopCoroutine(healthCoroutine);
        healthCoroutine = StartCoroutine(ChefHealthBarCoroutine(health));

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashCoroutine());

        if (healthBarContainer != null)
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(PunchScaleCoroutine());
        }
    }

    IEnumerator ChefHealthBarCoroutine(float targetHealth)
    {
        float startFill = chefHealthBarImage.fillAmount;

        // Only punch-dip if health actually decreased (don't punch on heal)
        bool isDamage = targetHealth < startFill;
        float overshootTarget = isDamage
            ? Mathf.Max(0f, targetHealth - overshootAmount)
            : targetHealth;

        // Phase 1: quick drop past the target (punch down)
        float elapsed = 0f;
        float downDuration = punchDuration * 0.4f;
        while (elapsed < downDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutQuad(elapsed / downDuration);
            chefHealthBarImage.fillAmount = Mathf.Lerp(startFill, overshootTarget, t);
            yield return null;
        }

        // Phase 2: bounce back up to the real target
        elapsed = 0f;
        float upDuration = punchDuration * 0.6f;
        float bounceStart = chefHealthBarImage.fillAmount;
        while (elapsed < upDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutBack(elapsed / upDuration);
            chefHealthBarImage.fillAmount = Mathf.Lerp(bounceStart, targetHealth, t);
            yield return null;
        }

        chefHealthBarImage.fillAmount = targetHealth;

        // Phase 3: hold, then drain the red trailing bar
        yield return new WaitForSeconds(redDelay);

        float redStart = chefHealthBarRedImage.fillAmount;
        elapsed = 0f;
        while (elapsed < redDrainDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / redDrainDuration;
            chefHealthBarRedImage.fillAmount = Mathf.Lerp(redStart, targetHealth, t);
            yield return null;
        }

        chefHealthBarRedImage.fillAmount = targetHealth;
    }

    IEnumerator FlashCoroutine()
    {
        chefHealthBarImage.color = flashColor;
        yield return new WaitForSeconds(flashDuration);

        float elapsed = 0f;
        float fadeDuration = 0.15f;
        Color start = flashColor;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            chefHealthBarImage.color = Color.Lerp(start, normalColor, elapsed / fadeDuration);
            yield return null;
        }
        chefHealthBarImage.color = normalColor;
    }

    IEnumerator PunchScaleCoroutine()
    {
        Vector3 originalScale = Vector3.one;
        Vector3 punchScale = originalScale * (1f + scalePunchAmount);

        float elapsed = 0f;
        float outDuration = 0.08f;
        while (elapsed < outDuration)
        {
            elapsed += Time.deltaTime;
            healthBarContainer.localScale = Vector3.Lerp(originalScale, punchScale, elapsed / outDuration);
            yield return null;
        }

        elapsed = 0f;
        float backDuration = 0.25f;
        Vector3 from = healthBarContainer.localScale;
        while (elapsed < backDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutElastic(elapsed / backDuration);
            healthBarContainer.localScale = Vector3.LerpUnclamped(from, originalScale, t);
            yield return null;
        }

        healthBarContainer.localScale = originalScale;
    }

    // --- Easing functions (no external dependencies) ---

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseOutElastic(float t)
    {
        const float c4 = (2f * Mathf.PI) / 3f;
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }
}