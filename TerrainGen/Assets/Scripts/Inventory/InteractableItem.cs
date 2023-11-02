using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractableItem : MonoBehaviour
{
    public ItemTemplate template;
   [HideInInspector] public BaseItem Iinteractable;

    private void Awake()
    {
        Iinteractable = new BaseItem();

        if (template != null)
        {
            Iinteractable.Name = template.Name;
            Iinteractable.Icon = template.Icon;
            Iinteractable.Amount = template.Amount;
            Iinteractable.MaxAmount = template.MaxAmount;
        }
    }

    public void Destroy()
    {
        Destroy(this.gameObject);
    }
}
