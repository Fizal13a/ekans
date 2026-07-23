using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChaosCardUI : MonoBehaviour
{
   public Image iconImage;
   public TextMeshProUGUI titleText;
   public TextMeshProUGUI descriptionText;
   
   private ChaosScriptableObject chaosObject;
   
   public void SetUpCard(ChaosScriptableObject chaosScriptableObject)
   {
      chaosObject =  chaosScriptableObject;
      if(chaosScriptableObject.chaosIcon != null) iconImage.sprite = chaosScriptableObject.chaosIcon;
      titleText.text = chaosScriptableObject.chaosName;
      descriptionText.text = chaosScriptableObject.chaosDescription;
   }

   public void ResetCard()
   {
      chaosObject = null;
   }
   
   public ChaosScriptableObject GetChaosObject()
   {
      return chaosObject;
   }
}
