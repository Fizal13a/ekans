using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCraftAttack : MonoBehaviour
{
    [SerializeField]
    private List<FoodType> currentRecipe;
    
    [SerializeField] private ParticleSystem craftedParticles;

    private bool canAttack = false;
    private int currentIndex;

    private void Awake()
    {
        canAttack = false;
    }

    private void OnEnable()
    {
        GameManager.events.AddEvent<SnakeSegment>(GameEvents.EventType.OnAteFood, CollectFood);
        GameManager.events.AddEvent<RecipieAttackData>(GameEvents.EventType.OnCraftAttackAdded,OnNewCraftAdded);
    }
    
    private void OnNewCraftAdded(RecipieAttackData  data)
    {
        currentIndex = 0;
        currentRecipe.Clear();
        
        foreach (FoodType foodType in data.recipie)
        {
            currentRecipe.Add(foodType);
        }
        
        canAttack = true;
    }

    public void CollectFood(SnakeSegment food)
    {
        Debug.Log("Collecting food");
        
        if(!canAttack) return;
        
        Debug.Log("Can craft attack");
        
        if (food.FoodType == currentRecipe[currentIndex])
        {
            Debug.Log("Craft item collected");

            GameManager.events.TriggerEvent(GameEvents.EventType.OnCraftItemCollected,currentIndex);
            currentIndex++;
            
            if (currentIndex >= currentRecipe.Count)
            {
                CompleteCombo();
            }
        }
        else
        {
            currentIndex = 0;
            GameManager.events.TriggerEvent(GameEvents.EventType.OnResetCraftsCollected);

            // Important:
            // Check if this newly collected food can
            // immediately start the recipe again.
            if (food.FoodType == currentRecipe[0])
            {
                currentIndex = 1;
            }
        }
    }

    private void CompleteCombo()
    {
        Debug.Log("COMBO COMPLETE!");

        currentIndex = 0;
        craftedParticles.Play();
        GameManager.events.TriggerEvent(GameEvents.EventType.OnCraftAttackStarted);
        Debug.Log("Chef Attacked!");
        //GameManager.events.TriggerEvent(GameEvents.EventType.OnChecfGotAttacked, 15);
        // Trigger player attack
    }

    private void OnDisable()
    {
        GameManager.events.RemoveEvent<SnakeSegment>(GameEvents.EventType.OnAteFood, CollectFood);
        GameManager.events.RemoveEvent<RecipieAttackData>(GameEvents.EventType.OnCraftAttackAdded,OnNewCraftAdded);
    }
}
