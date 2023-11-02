using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceItem", menuName = "interactables/ResourceItem")]
public class ItemTemplate : ScriptableObject
{

    public string Name;
    public Sprite Icon;
    public float Amount;
    public float MaxAmount;


}
