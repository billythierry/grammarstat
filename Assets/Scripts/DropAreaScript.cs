using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropAreaScript : MonoBehaviour, ICardDropArea
{
    public void OnCardDrop(CardScript card)
    {
        Vector3 newPosition = transform.position;
        newPosition.z = card.transform.position.z;

        card.transform.position = transform.position;
        Debug.Log("Card dropped here");
    }
}
