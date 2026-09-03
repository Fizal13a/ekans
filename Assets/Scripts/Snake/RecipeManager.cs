using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class RecipieAttackData
{
    public List<FoodType> recipie;
}

public class RecipeManager : MonoBehaviour
{
    
    public RecipeUI recipeUI;

    public int unlockLevel;
    [Header("Recipe")]
    [SerializeField] private GameObject craftingPanel;
    [SerializeField] private int recipeLength = 4;

    private List<FoodType> currentRecipe = new();

    public IReadOnlyList<FoodType> CurrentRecipe => currentRecipe;

    private void Start()
    {
        craftingPanel.SetActive(false);
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent<LevelUpData>(GameEvents.EventType.OnLevelUp, OnLevelUp);
        GameManager.events.AddEvent(GameEvents.EventType.OnCraftAttackCompleted, GenerateRecipe);
    }

    private void OnLevelUp(LevelUpData data)
    {
        if (data.Level == unlockLevel)
        {
            GameManager.events.TriggerEvent(GameEvents.EventType.OnCraftAttackUnlocked);
            craftingPanel.SetActive(true);
            GenerateRecipe();
        }
    }

    public void GenerateRecipe()
    {
        currentRecipe.Clear();
        RecipieAttackData recipieAttackData = new RecipieAttackData();
        recipieAttackData.recipie = new List<FoodType>();

        for (int i = 0; i < recipeLength; i++)
        {
            FoodType food = GetRandomFood();
            currentRecipe.Add(food);
            recipieAttackData.recipie.Add(food);
        }

        GameManager.events.TriggerEvent(GameEvents.EventType.OnCraftAttackAdded, recipieAttackData);
        DisplayRecipe();
    }

    private FoodType GetRandomFood()
    {
        FoodType[] foods = (FoodType[])System.Enum.GetValues(typeof(FoodType));

        return foods[Random.Range(0, foods.Length)];
    }

    private void DisplayRecipe()
    {
        recipeUI.DisplayRecipe(currentRecipe);
    }
    
    private void OnDisable()
    {
        GameManager.events.RemoveEvent<LevelUpData>(GameEvents.EventType.OnLevelUp, OnLevelUp);
        GameManager.events.RemoveEvent(GameEvents.EventType.OnCraftAttackCompleted, GenerateRecipe);
    }
}