using UnityEngine;

public class MoveCubeData
{
    public Vector3 currentPos;
    Vector2 forceDir;
    float speed;
    int field = -1;
    GameObject graphics;
    public int iterator = 0;
    public float lerpduration;
    public float timeElapsed;
    public bool forwardMovement = true;
    public bool doneMoving = false;
    public Vector2 ForceDir { get => forceDir; set => forceDir = value; }
    public float Speed { get => speed; set => speed = value; }
    public int Field { get => field; set => field = value; }
    public GameObject Graphics { get => graphics; set => graphics = value; }
}
