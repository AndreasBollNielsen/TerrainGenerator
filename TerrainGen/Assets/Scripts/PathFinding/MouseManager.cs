using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Tilemaps;

public class MouseManager : MonoBehaviour
{

    public float maxRaycastDistance = 100f;
    private bool startTilePlaced = false;

    Tile startTile;
    Tile endTile;
    private TileGridGenerator tileMap;
    private Tile currentTile;

    private void Awake()
    {
        tileMap = FindObjectOfType<TileGridGenerator>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            //set calculated path
            if(startTilePlaced)
            {
                tileMap.SetPath();
                tileMap.ClearVisuals();
                startTilePlaced = false;
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
            {
                Tile tile = tileMap.GetTileAtPosition(hit.point);

                if (tile != null)
                {
                    currentTile = tile;

                    if (tile.type == TD.tiltypes.blank)
                    {
                        //set start
                        if (!startTilePlaced)
                        {
                            // Debug.Log("start set");
                            startTilePlaced = true;
                            startTile = tile;
                        }

                    }
                }


            }
        }

        //cancel pathfinding
        if (Input.GetMouseButton(1))
        {
            startTilePlaced = false;
            tileMap.ClearVisuals();
        }

        //calculate path if start tile is set
        if (startTilePlaced)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
            {
                Tile tile = tileMap.GetTileAtPosition(hit.point);
                
                if (tile != null)
                {
                    if (currentTile != tile && tile.type == TD.tiltypes.blank)
                    {
                        endTile = tile;
                       
                        tileMap.CalculateHeuristicCost(tile);
                        tileMap.CalculatePath(startTile, endTile);
                       
                        
                    }

                    currentTile = tile;
                }
            }
        }
    }
}
