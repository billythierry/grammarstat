using UnityEngine;

public class SpawnAreaScript : MonoBehaviour, ICardDropArea
{
    private CardScript currentCard;

    public void OnCardDrop(CardScript card)
    {
        currentCard = card;

        card.SetSpawnArea(this);

        Vector3 newPosition = transform.position;
        newPosition.z = card.transform.position.z;

        card.transform.position = newPosition;
    }

    public void RemoveCard(CardScript card)
    {
        if (currentCard == card)
        {
            currentCard = null;
        }
    }
}