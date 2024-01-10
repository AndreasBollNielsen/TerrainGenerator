using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockCollector : MonoBehaviour
{
    private static BlockCollector _instance;
    public static BlockCollector Instance { get { return _instance; } }
    public List<Block> testblocks = new List<Block>();
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (testblocks.Count > 0)
        {
            foreach (Block block in testblocks)
            {
                Destroy(block.DestroyChunk());
            }
          //  testblocks.Clear();
        }
    }
}
