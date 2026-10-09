using UnityEngine;

public class CardSpawner : MonoBehaviour
{
    [SerializeField] public GameObject cardPrefab;
    [SerializeField] public SpawnAreaScript[] spawnAreas;

    void Start()
    {
        SpawnCards();
    }

    void SpawnCards()
    {
        foreach (SpawnAreaScript spawnArea in spawnAreas)
        {
            GameObject cardObject = Instantiate(cardPrefab);

            CardScript card = cardObject.GetComponent<CardScript>();

            spawnArea.OnCardDrop(card);
        }
    }
}