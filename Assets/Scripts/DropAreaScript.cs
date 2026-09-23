using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropAreaScript : MonoBehaviour, ICardDropArea
{
    public void OnCardDrop(CardScript card)
    {
        card.transform.position = transform.position;
        Debug.Log("Card dropped here");
    }
}
