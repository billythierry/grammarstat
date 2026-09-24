using System.Collections.Generic;
using UnityEngine;

public class SpawnAreaScript : MonoBehaviour, ICardDropArea
{
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private int cardCount = 5;

    [SerializeField] private float spacing = 1.5f;
    [SerializeField] private float startOffsetX = 0f;

    private List<CardScript> cards = new List<CardScript>();

    void Start()
    {
        SpawnCards();
    }

    private void SpawnCards()
    {
        for (int i = 0; i < cardCount; i++)
        {
            Vector3 spawnPosition = transform.position;
            spawnPosition.x += startOffsetX + (i * spacing);

            GameObject newCardObject = Instantiate(
                cardPrefab,
                spawnPosition,
                Quaternion.identity
            );

            CardScript newCard = newCardObject.GetComponent<CardScript>();

            if (newCard != null)
            {
                cards.Add(newCard);
                newCard.SetSpawnArea(this);
            }
        }

        ArrangeCards();
    }

    public void OnCardDrop(CardScript card)
    {
        if (!cards.Contains(card))
        {
            cards.Add(card);
        }

        card.SetSpawnArea(this);

        ArrangeCards();
    }

    public void RemoveCard(CardScript card)
    {
        if (cards.Contains(card))
        {
            cards.Remove(card);
            ArrangeCards();
        }
    }

    private void ArrangeCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Vector3 newPosition = transform.position;

            newPosition.x += startOffsetX + (i * spacing);
            newPosition.z = cards[i].transform.position.z;

            cards[i].transform.position = newPosition;
        }
    }
}