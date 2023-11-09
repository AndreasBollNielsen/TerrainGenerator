using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MoveBlocks : MonoBehaviour
{
    public GameObject prefab;
    public List<Field> fields = new List<Field>();
    List<MoveCubeData> cubes = new List<MoveCubeData>();
    float tileSize;
    private void Awake()
    {
        tileSize = GetComponent<TileGridGenerator>().tileSize;
    }
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (fields.Count > 0)
        {
            if (cubes.Count != fields.Count)
            {
                var cube = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                var movingCube = new MoveCubeData();
                movingCube.Graphics = cube;
                cubes.Add(movingCube);
            }
            else
            {
                for (int i = 0; i < cubes.Count; i++)
                {
                    var cube = cubes[i];

                    //add start position if new
                    if (cube.Field < 0)
                    {
                        cube.Field = i;
                        cube.Speed = 5;
                        cube.Graphics.transform.position = fields[cube.Field].startPos + new Vector3(0, 2.5f, 0);
                        cube.currentPos = cube.Graphics.transform.position;
                        cube.lerpduration = 0.3f;
                    }


                    if (cube.doneMoving)
                    {
                        return;
                    }

                    //follow path with waypoints
                    Vector2 currentwaypoint = new Vector2();
                    Vector2 nextwaypoint = new Vector2();
                    int iterator = cube.iterator + 1;
                    if (!cube.forwardMovement)
                    {
                        if (iterator > 0)
                        {
                            iterator = cube.iterator - 1;
                        }
                        else
                        {
                            iterator = 0;
                        }
                    }

                    currentwaypoint = fields[cube.Field].waypoints[cube.iterator];

                    //set last waypoint
                    if (iterator >= fields[cube.Field].waypoints.Count)
                    {
                        var endpos = fields[cube.Field].endPos;

                        nextwaypoint = new Vector2(endpos.x, endpos.z);
                        // Debug.Log("endpos found:" + endpos);

                    }
                    else
                    {

                        nextwaypoint = fields[cube.Field].waypoints[iterator];

                    }

                    if (cube.timeElapsed < cube.lerpduration)
                    {
                       // Debug.Log($" current pos: {currentwaypoint} +next pos: {nextwaypoint}");

                        Vector3 pos = new Vector3();
                        Vector2 p = Vector2.Lerp(currentwaypoint, nextwaypoint, cube.timeElapsed / cube.lerpduration);
                        pos = new Vector3((float)p.x, 0, (float)p.y);
                        cube.currentPos = pos;
                        cube.Graphics.transform.position = pos;
                        cube.timeElapsed += Time.deltaTime;

                    }
                    else
                    {
                        cube.timeElapsed = 0;

                        //iterate forward 
                        if (cube.forwardMovement)
                        {
                            Debug.Log($"iterator {iterator} max length {fields[cube.Field].waypoints.Count - 1}");

                            if (cube.iterator < fields[cube.Field].waypoints.Count - 1)
                            {
                                cube.iterator += 1;

                            }
                            else
                            {
                                cube.forwardMovement = false;
                                cube.iterator = fields[cube.Field].waypoints.Count - 1;
                                Debug.Log("done forward: " + fields[cube.Field].waypoints[fields[cube.Field].waypoints.Count - 1]);
                            }
                        }
                        else
                        {
                            //iterate backwards
                            if (iterator > 0)
                            {
                                cube.iterator -= 1;
                            }
                            else
                            {
                                Debug.Log("finished " + cube.iterator);
                                cube.forwardMovement = true;
                                cube.iterator = 0;
                            }
                        }

                    }
                }

            }


        }


        showDebug();
    }



    void showDebug()
    {
        foreach (var field in fields)
        {
            foreach (KeyValuePair<Vector2Int, Vector2> force in field.flowfields)
            {

                Vector3 fieldOrigin = new Vector3(force.Key.x * tileSize, 2, force.Key.y * tileSize);
                Vector3 direction = new Vector3(force.Value.x, 0, force.Value.y);

                Debug.DrawRay(fieldOrigin, direction * 10, Color.green);
                Debug.DrawRay(fieldOrigin, direction * 2, Color.red);

            }
        }
    }


}


public class Field
{
    public Vector3 startPos;
    public Vector3 endPos;
    public Dictionary<Vector2Int, Vector2> flowfields;
    public List<Vector2> waypoints;
}