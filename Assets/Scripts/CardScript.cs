using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardScript : MonoBehaviour
{
    private Collider2D col;
    private Vector3 startDragPosition; 
    private SpawnAreaScript currentSpawnArea;

    // Start is called before the first frame update
    void Start()
    {
        col = GetComponent<Collider2D>();
    }

    void OnMouseDown()
    {
        Debug.Log("Card clicked");
        startDragPosition = transform.position;

        if (currentSpawnArea != null)
        {
            currentSpawnArea.RemoveCard(this);
            currentSpawnArea = null;
        }

        transform.position = GetMousePositionInWorldSpace();
    }

    void OnMouseDrag()
    {
        transform.position = GetMousePositionInWorldSpace();
    }

    void OnMouseUp()
    {
        col.enabled = false;
        Collider2D hitCollider = Physics2D.OverlapPoint(transform.position);
        col.enabled = true;
        if (hitCollider != null && hitCollider.TryGetComponent(out ICardDropArea cardDropArea))
        {
            cardDropArea.OnCardDrop(this);
        }
        else 
        {
            transform.position = startDragPosition;
        }
    }

    public void SetSpawnArea(SpawnAreaScript spawnArea)
    {
        currentSpawnArea = spawnArea;
    }

    public Vector3 GetMousePositionInWorldSpace()
    {
        Vector3 p = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        p.z = transform.position.z;
        return p;
    }

    
}
