using System;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class generator_editorTool : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    Button GenButton;
    Button ClearButton;
    Button Cancel_button;
    ProgressBar _ProgressBar;
    bool[,] holeData;
    int size;
    
    float scaleFactor;
    TerrainData td;
    Ray ray;
    bool RunGenerating = false;

    [MenuItem("Tools/generator_editorTool")]
    public static void ShowExample()
    {
        generator_editorTool wnd = GetWindow<generator_editorTool>();
        wnd.titleContent = new GUIContent("generator_editorTool");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        //// VisualElements objects can contain other VisualElement following a tree hierarchy.
        //VisualElement label = new Label("Hello World! From C#");
        //root.Add(label);

        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);

        GenButton = root.Q<Button>("Generate_Button");
        ClearButton = root.Q<Button>("Clear_Button");
        _ProgressBar = root.Q<ProgressBar>("Progress");
        Cancel_button = root.Q<Button>("Cancel_Button");
        GenButton.clicked += () => Generate();
        ClearButton.clicked += () => ClearTerrain();
        Cancel_button.clicked += () => StopGenerating();

        //initialize tool
        td = Terrain.activeTerrain.terrainData;
        size = td.heightmapResolution - 1;
        scaleFactor = td.size.x / size;
        holeData = new bool[size, size];

       

        ClearTerrain();
    }

    private void StopGenerating()
    {
        RunGenerating = false;
        _ProgressBar.title = "Aborted";
    }

    private void ClearTerrain()
    {
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                holeData[i, j] = true;
            }
        }

        SetTerrain();
        _ProgressBar.value = 0;
        _ProgressBar.style.display = DisplayStyle.None;
        Cancel_button.style.display = DisplayStyle.None;
    }

    private void Generate()
    {
        Cancel_button.style.display = DisplayStyle.Flex;
        _ProgressBar.style.display = DisplayStyle.Flex;
        _ProgressBar.title = "Generating...";
        RunGenerating = true;
        EditorCoroutineUtility.StartCoroutineOwnerless(GenerateIncremental());
       
    }

   

    IEnumerator GenerateIncremental()
    {
        LayerMask mask = LayerMask.GetMask("hole");
        float totalProgress = td.size.x * td.size.y;
        float currentProgress = 0;
        _ProgressBar.highValue = totalProgress;
        _ProgressBar.value = currentProgress;

        

        for (int x = 0; x < td.size.x; x++)
        {
            
            yield return new WaitForEndOfFrame();
            for (int y = 0; y < td.size.x; y++)
            {
                

                ray = new Ray(new Vector3(x, 1000, y), Vector3.down * 1000);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, mask))
                {
                    int xScaled = Mathf.RoundToInt(hit.point.x / scaleFactor);
                    int yScaled = Mathf.RoundToInt(hit.point.z / scaleFactor);
                    holeData[yScaled, xScaled] = false;
                   
                }

                //increase progression
                currentProgress++;
                _ProgressBar.value = currentProgress;

                if(!RunGenerating)
                {
                    yield break;
                }
            }
           
        }

        Cancel_button.style.display = DisplayStyle.None;
        _ProgressBar.title = "Finished";
        SetTerrain();

    }

    

   

    private void SetTerrain()
    {

        td.SetHoles(0, 0, holeData);
    }
}
