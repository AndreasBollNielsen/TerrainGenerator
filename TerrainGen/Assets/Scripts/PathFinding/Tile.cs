using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Tile
{
    public int x;
    public int y;
    public int fcost;
    public float hcost;
    private TD.tiltypes _type;
    public GameObject obj;
    public Tile Parent;


    public TD.tiltypes type
    {
        get { return _type; }
        set
        {
            if (_type != value)
            {
                _type = value;

                // Call the ChangeTileType method
                // ChangeTileType(this, _type);

            }
        }
    }

    public void ChangeMaterial(Material[] materials)
    {
       // Debug.Log(materials);
        // Update the game object with the new type
        if (_type == TD.tiltypes.road)
        {
            obj.GetComponent<Renderer>().sharedMaterial = materials[1];
        }
        else if(_type== TD.tiltypes.blank)
        {
            obj.GetComponent<Renderer>().sharedMaterial = materials[0];
        }
        else if(_type == TD.tiltypes.blocked)
        {
            obj.GetComponent<Renderer>().sharedMaterial = materials[2];
        }
    }
}
