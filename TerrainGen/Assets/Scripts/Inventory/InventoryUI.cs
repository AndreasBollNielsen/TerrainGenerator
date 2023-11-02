using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryUI : MonoBehaviour
{
    public UIDocument doc;
    public VisualTreeAsset buttonPrefab;
    bool toggleInventory;
    private void OnEnable()
    {
       
        
    }

    // Start is called before the first frame update
    void Start()
    {
        Inventory.Instance.UIUpdateEvent += OnInventoryUpdate;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Tab))
        {
            toggleInventory= !toggleInventory;
            doc.rootVisualElement.style.display = toggleInventory ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    void OnInventoryUpdate(List<BaseItem> items)
    {
        doc.rootVisualElement.Q("ItemsContainer").Clear();
        foreach (var item in items)
        {
            SlotItem slot = new SlotItem(item, buttonPrefab);
            doc.rootVisualElement.Q("ItemsContainer").Add(slot.btn);
        }
    }
}

public class SlotItem
{

    public Button btn;
    public BaseItem item;
    public SlotItem(BaseItem item, VisualTreeAsset element)
    {
        TemplateContainer buttoncontainer = element.Instantiate();
        btn = buttoncontainer.Q<Button>();
        btn.RegisterCallback<ClickEvent>(OnClick);
        this.item = item;

        //set items data
        btn.style.backgroundImage = new StyleBackground(this.item.Icon);
       var label = btn.Q<Label>("ItemsCount");
        label.text = item.Amount.ToString();
    }

    public void OnClick(ClickEvent evt)
    {
        Debug.Log("this item is: " + this.item.Name);
    }
}
