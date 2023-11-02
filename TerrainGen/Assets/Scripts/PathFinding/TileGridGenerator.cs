using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using Unity.Mathematics;
using Unity.PlasticSCM.Editor.WebApi;
using UnityEngine;
using UnityEngine.Profiling;
using static TileGridGenerator;

public class TileGridGenerator : MonoBehaviour
{
    public int gridWidth = 10; // number of tiles in the horizontal direction
    public int gridHeight = 10; // number of tiles in the vertical direction
    public float tileSize = 10;
    public GameObject prefab;
    private Tile[,] tiles; // 2D array to store the generated tiles
    public Material[] materials;
    List<Tile> path = new List<Tile>();
    
    List<GameObject> visuals = new List<GameObject>();
    Vector2Int minBounds;
    Vector2Int maxBounds;


    void Start()
    {
        GenerateTileGrid();
    }

    private void Update()
    {
        DebugBounds();
    }

    void GenerateTileGrid()
    {
        tiles = new Tile[gridWidth, gridHeight];

        // Loop through the grid and create a new tile with a randomly chosen type for each position
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                TD.tiltypes tileType = GetRandomType();
                Tile tile = new Tile();
                tile.type = tileType;
                tile.x = x;
                tile.y = y;
                tiles[x, y] = tile;
                var tileobject = Instantiate(prefab, new Vector3(x * tileSize, 0, y * tileSize), Quaternion.identity);
                tileobject.transform.eulerAngles = new Vector3(90, 0, 0);
                tileobject.transform.parent = this.transform;
                tile.obj = tileobject;
                tile.ChangeMaterial(materials);

            }
        }
    }

    TD.tiltypes GetRandomType()
    {
        int randNum = UnityEngine.Random.Range(0, 2);
        return (TD.tiltypes)randNum;
    }

    public Tile GetTileAtPosition(Vector3 position)
    {
        int x = Mathf.RoundToInt(position.x / tileSize);
        int z = Mathf.RoundToInt(position.z / tileSize);

        if (x < 0 || x >= tiles.GetLength(0) || z < 0 || z >= tiles.GetLength(1))
        {
            return null;
        }

        return tiles[x, z];
    }

    public void CalculateHeuristicCost(Tile endTile)
    {
        for (int x = minBounds.x; x < maxBounds.x; x++)
        {
            for (int y = minBounds.y; y < maxBounds.y; y++)
            {
                var tile = tiles[x, y];
                if (tile != endTile)
                {
                    tile.hcost = Vector2.Distance(new Vector2Int(tile.x, tile.y), new Vector2Int(endTile.x, endTile.y));
                }
            }
        }
    }

    Tile[] GetNeighbors(Tile tile)
    {
        Vector2Int[] directions = new Vector2Int[] {
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
        };
        List<Tile> neighbors = new List<Tile>();

        for (int i = 0; i < directions.Length; i++)
        {
            int x = tile.x + directions[i].x;
            int y = tile.y + directions[i].y;
            if (x >= minBounds.x && x <= maxBounds.x && y >= minBounds.y && y <= maxBounds.y)
            {
                var node = tiles[x, y];
                if (node.type == TD.tiltypes.blank || node.type == TD.tiltypes.road)
                {
                    neighbors.Add(node);
                }

            }


        }

        return neighbors.ToArray();
    }

    public void ClearVisuals()
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            DestroyImmediate(visuals[i]);
        }
        visuals.Clear();
    }

    private void SetBoundings(Tile startTile, Tile endTile)
    {
        int minX = startTile.x;
        int minY = startTile.y;
        int maxX = endTile.x;
        int maxY = endTile.y;
        int padding = 3;

        //flip x values
        if (minX > maxX)
        {
            int temp = minX;
            minX = maxX;
            maxX = temp;
        }

        //flip y values
        if (minY > maxY)
        {
            int temp = minY;
            minY = maxY;
            maxY = temp;
        }

        //clamp overflow
        if (minY - padding < 0)
        {
            minY = padding;
        }

        if (maxY + padding > gridHeight)
        {
            maxY = 0 - padding;
        }

        if (minX - padding < 0)
        {
            minX = padding;
        }

        if (maxX + padding > gridWidth)
        {
            maxX = 0 - padding;
        }

        //set bounds
        minBounds = new Vector2Int(minX - padding, minY - padding);
        maxBounds = new Vector2Int(maxX + padding, maxY + padding);

        // Debug.Log($"min bounds: {minBounds} max bounds: {maxBounds}");
    }

    List<Tile> TracePath(Tile startTile, Tile endTile)
    {
        List<Tile> path = new List<Tile>();
        Tile currentNode = endTile;

        //trace back parent tile at each node
        while (currentNode != startTile)
        {
            path.Add(currentNode);
            currentNode = currentNode.Parent;
        }
        path.Add(startTile);
        path.Reverse();
        return path;
    }

    public void CalculatePath(Tile startTile, Tile endTile)
    {
        List<Tile> openList = new List<Tile>();
        HashSet<Tile> visited = new HashSet<Tile>();

        ClearVisuals();

        SetBoundings(startTile, endTile);


        //add start node to the openlist
        startTile.fcost = 0;
        openList.Add(startTile);

        //run this recursively
        while (openList.Count > 0)
        {
            Tile currentNode = openList[0];

            //set current node to lowest cost node
            for (int i = 0; i < openList.Count; i++)
            {
                if (openList[i].fcost < currentNode.fcost ||
                    openList[i].fcost == currentNode.fcost &&
                    openList[i].hcost < currentNode.hcost)
                {
                    currentNode = openList[i];
                }
            }

            //move current node to visited
            openList.Remove(currentNode);
            visited.Add(currentNode);

            //current node is end node, return path
            if (currentNode == endTile)
            {
                // Debug.Log("path found!");
                path = TracePath(startTile, endTile);
            }

            //check neighbor node for lowest cost
            var neighbors = GetNeighbors(currentNode);
            foreach (Tile neighbor in neighbors)
            {
                //skip neighbor if it already exists in visited list
                if (visited.Contains(neighbor))
                {
                    continue;
                }

                int neighborCost = currentNode.fcost + 1;

                //add neighbor if has not been visited or calculated cost is lower than previously calculated
                if (neighborCost < neighbor.fcost || !openList.Contains(neighbor))
                {
                    neighbor.fcost = neighborCost;
                    neighbor.Parent = currentNode;

                    openList.Add(neighbor);
                }
            }


        }

        //  Debug.Log("done pathfinding");


        //show visual
        ShowVisual();

       
    }


    void ShowVisual()
    {
        if (visuals.Count != path.Count)
        {
            ClearVisuals();

            foreach (Tile pathNode in path)
            {
                var visual = Instantiate(prefab, new Vector3(pathNode.x * tileSize, 0.1f, pathNode.y * tileSize), Quaternion.identity);
                visual.transform.eulerAngles = new Vector3(90, 0, 0);
                visual.GetComponent<Renderer>().material = materials[3];
                visuals.Add(visual);
            }
        }


    }

    void CalculateForcefield()
    {
        Dictionary<Vector2Int, Vector2> flowfield = new Dictionary<Vector2Int, Vector2>();
        List<Vector2> waypoints = new List<Vector2>();
        Field field = new Field();
        for (int i = 0; i < path.Count - 1; i++)
        {
            Tile currentTile = path[i];
            Tile nextTile = path[i + 1];

            Vector2 force = new Vector2(nextTile.x, nextTile.y) - new Vector2(currentTile.x, currentTile.y);
            force.Normalize();
            flowfield.Add(new Vector2Int(currentTile.x, currentTile.y), force);
            Vector2 waypoint = new Vector2(currentTile.x * tileSize,currentTile.y * tileSize);
            waypoints.Add(waypoint);
            field.flowfields = flowfield;
           
            if(i == path.Count - 1)
            {
                Debug.Log("last tile: " + waypoint);
            }

        }
        Vector3 starttile = new Vector3(path[0].x * tileSize, 0, path[0].y * tileSize);
      //  Debug.Log("starttile: " + starttile);
        Vector3 endtile = new Vector3(path[path.Count-1].x * tileSize, 0, path[path.Count - 1].y * tileSize);
        Debug.Log("endtile: " + endtile);
        field.waypoints= waypoints;
        field.startPos= starttile;
        field.endPos= endtile;

        GetComponent<MoveBlocks>().fields.Add(field);
       // flowfield.Clear();

    }

    private void DebugBounds()
    {
        if (visuals.Count > 0)
        {

            Debug.DrawLine(new Vector3(minBounds.x * tileSize, 0, minBounds.y * tileSize), new Vector3(maxBounds.x * tileSize, 0, minBounds.y * tileSize), Color.red);
            Debug.DrawLine(new Vector3(maxBounds.x * tileSize, 0, minBounds.y * tileSize), new Vector3(maxBounds.x * tileSize, 0, maxBounds.y * tileSize), Color.red);
            Debug.DrawLine(new Vector3(maxBounds.x * tileSize, 0, maxBounds.y * tileSize), new Vector3(maxBounds.x * tileSize, 0, minBounds.y * tileSize), Color.red);
            Debug.DrawLine(new Vector3(minBounds.x * tileSize, 0, maxBounds.y * tileSize), new Vector3(minBounds.x * tileSize, 0, minBounds.y * tileSize), Color.red);
        }
    }

    public void SetPath()
    {
        if (path.Count > 0)
        {
            foreach (Tile pathNode in path)
            {
                pathNode.type = TD.tiltypes.road;
                pathNode.ChangeMaterial(materials);
            }


            CalculateForcefield();
        }
    }
}
