using UnityEngine;

public class ReboundEventData
{
    public ChefAttackObject chefAttackObject;
    public Transform targetTransform;

    public ReboundEventData(ChefAttackObject chefAttackObject, Transform targetTransform)
    {
        this.chefAttackObject = chefAttackObject;
        this.targetTransform = targetTransform;
    }
}
