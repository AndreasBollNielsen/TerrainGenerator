using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class Interaction : MonoBehaviour
{
    Camera cam;
    public Vector3 rayOrigin;
    public RaycastHit hit;
    public Vector3 rayOffset;
    public GameObject wallPrefab;
    public GameObject foundationPrefab;
    GameObject ghostBuild;
    bool wallSnapping = false;
    bool foundationSnapping = false;

    private void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        rayOrigin = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));

        //simulate object building
        if (Input.GetKeyUp(KeyCode.F))
        {
            ghostBuild = Instantiate(wallPrefab);
            // ghostBuild.transform.Rotate(Vector3.up, 90f);
            wallSnapping = true;
        }
        //if (Input.GetKeyUp(KeyCode.G))
        //{
        //    ghostBuild = Instantiate(foundationPrefab);
        //    foundationSnapping = true;
        //}

        //deselect
        if (Input.GetMouseButtonDown(1))
        {
            Destroy(ghostBuild);
            ghostBuild = null;
            foundationSnapping = false;
            wallSnapping = false;
        }

        if (Physics.Raycast(rayOrigin, cam.transform.forward, out hit))
        {
            //set ghost position
            if (ghostBuild != null)
            {
                ghostBuild.transform.position = rayOrigin + cam.transform.forward * 5;

            }

            //enable snapping if building
            if (ghostBuild != null)
            {
                //enable snap wall
                if (wallSnapping)
                {

                    if (wallSnapping && hit.collider.tag == "snapping")
                    {
                        ghostBuild.transform.position = hit.collider.transform.position;
                        Vector3 lookpos = new Vector3(hit.collider.transform.parent.position.x,
                            ghostBuild.transform.position.y,
                            hit.collider.transform.parent.position.z);
                        ghostBuild.transform.LookAt(lookpos, Vector3.up);



                    }
                }

                if (foundationSnapping)
                {
                    if (hit.collider.tag == "foundation")
                    {

                        Vector3 parent = hit.collider.transform.parent.position;
                        Vector3 snappoint = hit.collider.transform.position;
                        Vector3 dir = snappoint - parent;

                        ghostBuild.transform.position = snappoint + new Vector3(dir.x, 0, dir.z);
                    }
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (hit.collider.tag == "snapping")
                {

                    var building = Instantiate(ghostBuild, ghostBuild.transform.position, ghostBuild.transform.rotation);
                    Destroy(ghostBuild);
                    ghostBuild = null;

                    //disable collisions
                    disableCollisions(building);

                    building.GetComponent<BoxCollider>().enabled = true;
                    building.transform.Find("SnappingPoints").gameObject.SetActive(true);

                    //disable bools
                    wallSnapping = false;
                    foundationSnapping = false;



                }
                else if (hit.collider.GetComponent<InteractableItem>())
                {
                    InteractableItem itemInteraction = hit.collider.GetComponent<InteractableItem>();
                    if (itemInteraction != null)
                    {
                        // Player clicked on an interactable item
                        itemInteraction.Iinteractable.Pickup();
                        itemInteraction.Destroy();

                    }
                }


            }
        }



    }

    void disableCollisions(GameObject building)
    {
        var boxsize = building.transform.localScale;
        Collider[] colliders = Physics.OverlapBox(building.transform.position, boxsize / 2f);

        foreach (Collider collider in colliders)
        {
            if (collider.tag == "foundation")
            {
                collider.gameObject.SetActive(false);
                // Debug.Log(collider.name);

            }
        }
    }
}
