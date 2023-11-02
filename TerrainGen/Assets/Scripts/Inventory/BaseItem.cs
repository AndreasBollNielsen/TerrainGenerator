using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BaseItem
{

    public string Name;
    public Sprite Icon;
    public float Amount;
    public float MaxAmount;



    public virtual void Use()
    {

    }

    public virtual void Pickup()
    {
        Debug.Log("pickup");
        Inventory.Instance.Add_To_Inventory(this);
    }
}
