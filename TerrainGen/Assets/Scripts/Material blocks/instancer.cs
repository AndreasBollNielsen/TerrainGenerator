using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class instancer : MonoBehaviour
{
    public Material mat;
    public Mesh geo;
    [Range(1, 10000)]
    public int population;
    List<Matrix4x4> matrices= new List<Matrix4x4>();
    Vector4[] colors;
    float[] offsets;
    MaterialPropertyBlock block;
    float startTime = 1.0f;
    float currentTime = 0;

    // Start is called before the first frame update
    void Start()
    {
         colors = new Vector4[population];
        offsets = new float[population];
        for (int i = 0; i < population; i++)
        {
            Vector3 pos = new Vector3(Random.Range(0, 300), 0, Random.Range(0, 300));
            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetTRS(pos, Quaternion.identity, Vector3.one * Random.Range(2,8));
            matrices.Add(matrix);

            Color color = new Color(Random.Range(0.1f,1), Random.Range(0.1f, 1), Random.Range(0.1f, 1));
            float randoffset = Random.Range(1, 5);
            colors[i] = color;
            offsets[i] = randoffset;
        }
        block = new MaterialPropertyBlock();
       Debug.Log(Color.red);
        block.SetVectorArray("_color_override", colors);
        block.SetFloatArray("_offset", offsets);
    }

    // Update is called once per frame
    void Update()
    {
        currentTime += Time.deltaTime ;
      
        if(currentTime >= startTime)
        {
            currentTime = 0;
       

            //set new values
            for (int i = 0; i < population; i++)
            {
                Color color = new Color(Random.Range(0.1f, 1), Random.Range(0.1f, 1), Random.Range(0.1f, 1));
                float randoffset = Random.Range(1, 5);
                colors[i] = color;
                offsets[i] = randoffset;
            }
          //  block.SetVectorArray("_color_override", colors);
            block.SetFloatArray("_offset", offsets);
        }


        Graphics.DrawMeshInstanced(geo, 0, mat, matrices.ToArray(), matrices.Count,block);
    }
}
