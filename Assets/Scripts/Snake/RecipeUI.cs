using System;
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
    public List<RecipieData> recipieData =  new List<RecipieData>();
    
    public List<Image> recipieImages = new List<Image>();
    public List<Image> recipieHighlightImages = new List<Image>();

    private void Awake()
    {
        GameManager.events.AddEvent<int>(GameEvents.EventType.OnCraftItemCollected, HighlightRecipe);
    }

    public void DisplayRecipe(IReadOnlyList<FoodType> recipe)
    {
        for (int i = 0; i < recipe.Count; i++)
        {
            recipieHighlightImages[i].enabled = false;
            foreach (var recData in recipieData)
            {
                if(recData.foodType == recipe[i])
                    recipieImages[i].sprite = recData.foodImage;
            }
        }
    }

    public void HighlightRecipe(int index)
    {
        recipieHighlightImages[index].enabled = true;
    }

    public void UpdateProgress(int currentIndex)
    {
        // Mark collected items as completed
    }
}
