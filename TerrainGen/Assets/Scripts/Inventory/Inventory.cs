using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;

    public List<BaseItem> items = new List<BaseItem>();
    public int MaxCapacity = 8;
    public event Action<List<BaseItem>> UIUpdateEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("singleton read");
    }

    public void Add_To_Inventory(BaseItem item)
    {
        //return if inventory is full
        if (items.Count == MaxCapacity)
        {
            Debug.Log("returning");
            return;
        }

        //if inventory already contains the item
        if (items.Any(x => x.Name == item.Name))
        {
            //find item with lowest amount
            var lowestItem = items.Where(_item => _item.Name == item.Name)
                     .OrderBy(_item => _item.Amount)
                     .FirstOrDefault();
            lowestItem.Amount += item.Amount;

            //split to new if above capacity
            if (lowestItem.Amount > lowestItem.MaxAmount)
            {
                float remaining = lowestItem.Amount - lowestItem.MaxAmount;
                var newItem = item;
                lowestItem.Amount = lowestItem.MaxAmount;
                newItem.Amount = remaining;

                //add new item
                items.Add(newItem);
            }
        }
        //else add new item
        else
        {
            Debug.Log("adding item");
            items.Add(item);
        }

        //update inventory UI
        UIUpdateEvent?.Invoke(items);

    }

    public void Remove_From_Inventory()
    {

    }

    public void Interact()
    {

    }
}


