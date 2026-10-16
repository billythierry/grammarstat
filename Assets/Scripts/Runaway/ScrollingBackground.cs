using UnityEngine;

// Layer background yang terus bergeser ke kanan sehingga kereta (menghadap kiri) terlihat melaju.
// Anak-anak objek ini adalah potongan yang berulang: potongan yang lewat batas kanan dipindah ke ujung kiri.
public class ScrollingBackground : MonoBehaviour
{
    [Tooltip("Kecepatan geser (unit/detik). Layer yang lebih jauh dibuat lebih lambat (parallax)")]
    [SerializeField] private float speed = 4f;
    [Tooltip("Panjang satu putaran pola. Potongan yang melewati batas kanan dipindah sejauh ini ke kiri")]
    [SerializeField] private float loopWidth = 24f;

    // 1 = normal, 0 = berhenti. Dipakai untuk memperlambat background saat kereta dirampok
    public float SpeedMultiplier { get; set; } = 1f;

    private float rightLimit;

    void Awake()
    {
        rightLimit = transform.position.x + loopWidth * 0.5f;
    }

    void Update()
    {
        float dx = speed * SpeedMultiplier * Time.deltaTime;
        foreach (Transform piece in transform)
        {
            Vector3 p = piece.position;
            p.x += dx;
            if (p.x > rightLimit) p.x -= loopWidth;
            piece.position = p;
        }
    }
}
