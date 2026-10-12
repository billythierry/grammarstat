using System.Collections;
using UnityEngine;

// Kereta bergerak ke kiri, kamera diam. Gerbong fisik dipakai ulang:
// gerbong yang sudah lewat jauh di kiri dipindah ke ujung kanan (tanpa Destroy/Instantiate).
public class TrainController : MonoBehaviour
{
    [Tooltip("Gerbong yang dipakai ulang. Minimal 3: sebelumnya, sekarang, berikutnya")]
    [SerializeField] private WagonScript[] wagons;
    [Tooltip("Jarak antar pusat gerbong (lebar gerbong + sambungan)")]
    [SerializeField] private float wagonSpacing = 22f;
    [Tooltip("Jarak awal kereta di kanan layar sebelum datang")]
    [SerializeField] private float arriveDistance = 33f;
    [SerializeField] private float arriveDuration = 2f;
    [SerializeField] private float moveDuration = 1.2f;
    [SerializeField] private float departDuration = 1.5f;

    public int CurrentIndex { get; private set; }
    public WagonScript CurrentWagon => WagonAt(CurrentIndex);
    public bool IsMoving { get; private set; }

    private int totalWagons;
    private float baseX;
    private int[] wagonIndex; // indeks gerbong (urutan soal) yang sedang dipegang tiap gerbong fisik

    void Awake()
    {
        baseX = transform.position.x;
    }

    // Siapkan kereta di luar layar sebelah kanan dengan jumlah gerbong = jumlah soal
    public void Setup(int total)
    {
        totalWagons = total;
        CurrentIndex = 0;
        wagonIndex = new int[wagons.Length];
        for (int i = 0; i < wagons.Length; i++) AssignWagon(i, i);
        SetX(baseX + arriveDistance);
    }

    public IEnumerator Arrive()
    {
        yield return MoveTo(baseX, arriveDuration);
    }

    public IEnumerator MoveToNext()
    {
        CurrentIndex++;
        yield return MoveTo(baseX - CurrentIndex * wagonSpacing, moveDuration);

        // Gerbong yang sudah 2 posisi di belakang tidak terlihat lagi: pindahkan ke depan antrean
        for (int i = 0; i < wagons.Length; i++)
        {
            if (wagonIndex[i] <= CurrentIndex - 2) AssignWagon(i, wagonIndex[i] + wagons.Length);
        }
    }

    // Kereta berangkat meninggalkan layar ke kiri
    public IEnumerator Depart()
    {
        yield return MoveTo(transform.position.x - arriveDistance, departDuration);
    }

    void AssignWagon(int physical, int logical)
    {
        wagonIndex[physical] = logical;
        WagonScript wagon = wagons[physical];
        wagon.ReleaseBoxes();
        wagon.gameObject.SetActive(logical < totalWagons);

        Vector3 p = wagon.transform.localPosition;
        p.x = logical * wagonSpacing;
        wagon.transform.localPosition = p;
    }

    WagonScript WagonAt(int logical)
    {
        for (int i = 0; i < wagons.Length; i++)
        {
            if (wagonIndex[i] == logical && wagons[i].gameObject.activeSelf) return wagons[i];
        }
        return null;
    }

    IEnumerator MoveTo(float targetX, float duration)
    {
        IsMoving = true;
        float startX = transform.position.x;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetX(Mathf.Lerp(startX, targetX, Mathf.SmoothStep(0f, 1f, t / duration)));
            yield return null;
        }
        SetX(targetX);
        IsMoving = false;
    }

    void SetX(float x)
    {
        Vector3 p = transform.position;
        p.x = x;
        transform.position = p;
    }
}
