using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class RecipieData
{
    public Sprite foodImage;
    public FoodType foodType;
}

public class RecipeUI : MonoBehaviour
{
    public List<RecipieData> recipieData = new List<RecipieData>();

    public List<Image> recipieImages = new List<Image>();
    public List<Image> recipieHighlightImages = new List<Image>();

    [Header("Reveal Settings")]
    [SerializeField] private float staggerDelay = 0.12f;      // gap between each icon popping in
    [SerializeField] private float popDuration = 0.35f;
    [SerializeField] private float overshootScale = 1.25f;    // how big the pop overshoots before settling

    [Header("Highlight Settings")]
    [SerializeField] private float highlightPunchDuration = 0.3f;
    [SerializeField] private Color highlightFlashColor = Color.yellow;

    private Coroutine displayCoroutine;
    private List<Coroutine> highlightCoroutines = new List<Coroutine>();

    private void OnEnable()
    {
        GameManager.events.AddEvent<int>(GameEvents.EventType.OnCraftItemCollected, HighlightRecipe);
        GameManager.events.AddEvent(GameEvents.EventType.OnResetCraftsCollected, ResetRecipes);
    }

    private void OnDisable()
    {
        GameManager.events.RemoveEvent<int>(GameEvents.EventType.OnCraftItemCollected, HighlightRecipe);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnResetCraftsCollected, ResetRecipes);
    }

    private void ResetRecipes()
    {
        foreach (var img in recipieHighlightImages)
        {
            img.enabled = false;
        }
    }

    public void DisplayRecipe(IReadOnlyList<FoodType> recipe)
    {
        if (displayCoroutine != null) StopCoroutine(displayCoroutine);
        displayCoroutine = StartCoroutine(DisplayRecipeCoroutine(recipe));
    }

    IEnumerator DisplayRecipeCoroutine(IReadOnlyList<FoodType> recipe)
    {
        // Reset everything instantly first (hidden state)
        for (int i = 0; i < recipe.Count; i++)
        {
            recipieHighlightImages[i].enabled = false;
            recipieImages[i].transform.localScale = Vector3.zero;
            recipieImages[i].enabled = true;

            foreach (var recData in recipieData)
            {
                if (recData.foodType == recipe[i])
                    recipieImages[i].sprite = recData.foodImage;
            }
        }

        // Reveal one at a time, staggered
        for (int i = 0; i < recipe.Count; i++)
        {
            StartCoroutine(PopInCoroutine(recipieImages[i].transform));
            yield return new WaitForSeconds(staggerDelay);
        }
    }

    IEnumerator PopInCoroutine(Transform target)
    {
        float elapsed = 0f;
        float outDuration = popDuration * 0.6f;

        // Phase 1: scale up past target (overshoot)
        while (elapsed < outDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutQuad(elapsed / outDuration);
            target.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * overshootScale, t);
            yield return null;
        }

        // Phase 2: settle back down to normal scale
        elapsed = 0f;
        float settleDuration = popDuration * 0.4f;
        Vector3 from = target.localScale;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settleDuration;
            target.localScale = Vector3.Lerp(from, Vector3.one, t);
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    public void HighlightRecipe(int index)
    {
        if (index < 0 || index >= recipieHighlightImages.Count) return;

        recipieHighlightImages[index].enabled = true;

        if (index < highlightCoroutines.Count && highlightCoroutines[index] != null)
            StopCoroutine(highlightCoroutines[index]);

        Coroutine c = StartCoroutine(HighlightPunchCoroutine(index));

        while (highlightCoroutines.Count <= index) highlightCoroutines.Add(null);
        highlightCoroutines[index] = c;
    }

    IEnumerator HighlightPunchCoroutine(int index)
    {
        Image highlight = recipieHighlightImages[index];
        Transform iconTransform = recipieImages[index].transform;

        Color originalColor = highlight.color;
        highlight.color = highlightFlashColor;

        Vector3 originalScale = Vector3.one;
        Vector3 punchScale = originalScale * 1.3f;

        float elapsed = 0f;
        float outDuration = highlightPunchDuration * 0.3f;
        while (elapsed < outDuration)
        {
            elapsed += Time.deltaTime;
            iconTransform.localScale = Vector3.Lerp(originalScale, punchScale, elapsed / outDuration);
            yield return null;
        }

        elapsed = 0f;
        float backDuration = highlightPunchDuration * 0.7f;
        Vector3 from = iconTransform.localScale;
        while (elapsed < backDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutElastic(elapsed / backDuration);
            iconTransform.localScale = Vector3.LerpUnclamped(from, originalScale, t);
            yield return null;
        }

        iconTransform.localScale = originalScale;

        // fade highlight color back to its original tint (keeps it enabled/visible)
        elapsed = 0f;
        float colorFade = 0.2f;
        while (elapsed < colorFade)
        {
            elapsed += Time.deltaTime;
            highlight.color = Color.Lerp(highlightFlashColor, originalColor, elapsed / colorFade);
            yield return null;
        }
        highlight.color = originalColor;
    }

    public void UpdateProgress(int currentIndex)
    {
        // Mark collected items as completed
    }

    // --- Easing ---
    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    private float EaseOutElastic(float t)
    {
        const float c4 = (2f * Mathf.PI) / 3f;
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }
}