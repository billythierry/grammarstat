using System.Collections;
using UnityEngine;

// Kereta bergerak ke kiri, kamera diam. Gerbong fisik dipakai ulang:
// gerbong yang sudah lewat jauh di kiri dipindah ke ujung belakang (tanpa Destroy/Instantiate).
public class TrainController : MonoBehaviour
{
    [Tooltip("Gerbong yang dipakai ulang. Minimal 4: yang keluar di kiri, sekarang, berikutnya, dan yang masuk dari kanan saat transisi")]
    [SerializeField] private WagonScript[] wagons;
    [Tooltip("Jarak antar pusat gerbong (lebar gerbong + sambungan)")]
    [SerializeField] private float wagonSpacing = 22f;
    [Tooltip("Jarak awal kereta di kanan layar sebelum datang")]
    [SerializeField] private float arriveDistance = 33f;
    [SerializeField] private float arriveDuration = 2f;
    [Tooltip("Lama transisi ke gerbong berikutnya. Notifikasi benar/salah tampil selama transisi ini")]
    [SerializeField] private float moveDuration = 2f;
    [SerializeField] private float departDuration = 1.5f;
    [Tooltip("Lokomotif di depan gerbong pertama. Hanya muncul di awal, di-Destroy setelah keluar layar")]
    [SerializeField] private Transform locomotive;

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
        SetX(Mathf.Max(baseX + arriveDistance, OffscreenRightX()));
    }

    // Posisi x kereta agar seluruh bagiannya (termasuk lokomotif) berada di luar layar kanan
    float OffscreenRightX()
    {
        Camera cam = Camera.main;
        float cameraRight = cam.transform.position.x + cam.orthographicSize * cam.aspect;
        float minX = float.MaxValue;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) minX = Mathf.Min(minX, r.bounds.min.x);
        if (minX == float.MaxValue) return transform.position.x;
        return cameraRight + (transform.position.x - minX) + 1f;
    }

    // Lokomotif hanya penarik di awal: hapus permanen setelah keluar dari layar kiri
    void DestroyLocomotiveIfOffscreen()
    {
        if (locomotive == null) return;
        Camera cam = Camera.main;
        float cameraLeft = cam.transform.position.x - cam.orthographicSize * cam.aspect;
        float maxX = float.MinValue;
        foreach (Renderer r in locomotive.GetComponentsInChildren<Renderer>()) maxX = Mathf.Max(maxX, r.bounds.max.x);
        if (maxX < cameraLeft)
        {
            Destroy(locomotive.gameObject);
            locomotive = null;
        }
    }

    public IEnumerator Arrive()
    {
        yield return MoveTo(baseX, arriveDuration);
    }

    public IEnumerator MoveToNext()
    {
        // Sebelum kereta bergerak: gerbong yang sudah 2 posisi di kiri (tidak terlihat) disambung ke ujung belakang.
        // Posisinya masih di luar layar kanan, jadi gerbong ikut masuk layar bersama rangkaian, tidak muncul tiba-tiba.
        // Gerbong di luar jumlah soal dinonaktifkan oleh AssignWagon, jadi di soal terakhir tidak ada gerbong tambahan.
        for (int i = 0; i < wagons.Length; i++)
        {
            if (wagonIndex[i] <= CurrentIndex - 2) AssignWagon(i, LastWagonIndex() + 1);
        }

        CurrentIndex++;
        yield return MoveTo(baseX - CurrentIndex * wagonSpacing, moveDuration);
        DestroyLocomotiveIfOffscreen();
    }

    // Indeks gerbong paling belakang dalam rangkaian saat ini
    int LastWagonIndex()
    {
        int last = int.MinValue;
        foreach (int index in wagonIndex) last = Mathf.Max(last, index);
        return last;
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
