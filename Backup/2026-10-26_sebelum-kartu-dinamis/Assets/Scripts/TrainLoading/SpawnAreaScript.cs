using UnityEngine;

public class SpawnAreaScript : MonoBehaviour, IBoxDropArea
{
    private BoxScript currentBox;

    public bool CanAccept(BoxScript box)
    {
        return currentBox == null || currentBox == box;
    }

    public void OnBoxDrop(BoxScript box)
    {
        currentBox = box;

        box.SetCurrentArea(this);

        Vector3 newPosition = transform.position;
        newPosition.z = box.transform.position.z;

        box.transform.position = newPosition;
    }

    public void RemoveBox(BoxScript box)
    {
        if (currentBox == box)
        {
            currentBox = null;
        }
    }
}
