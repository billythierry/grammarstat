using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropAreaScript : MonoBehaviour, IBoxDropArea
{
    // Box yang sedang menempati drop area ini (null bila kosong)
    public BoxScript CurrentBox { get; private set; }

    public bool CanAccept(BoxScript box)
    {
        return CurrentBox == null || CurrentBox == box;
    }

    public void OnBoxDrop(BoxScript box)
    {
        CurrentBox = box;
        box.SetCurrentArea(this);

        Vector3 newPosition = transform.position;
        newPosition.z = box.transform.position.z;

        box.transform.position = newPosition;
        Debug.Log("Box dropped here");
    }

    public void RemoveBox(BoxScript box)
    {
        if (CurrentBox == box)
        {
            CurrentBox = null;
        }
    }
}
