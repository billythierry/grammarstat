using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Kereta bergerak, kamera diam. Arah default ke kiri (masuk dari kanan); bisa dibalik ke kanan (masuk dari kiri) per level.
// Gerbong fisik dipakai ulang: gerbong yang sudah lewat jauh di belakang layar dipindah ke ujung belakang (tanpa Destroy/Instantiate).
public class TrainController : MonoBehaviour
{
    [Tooltip("Gerbong yang dipakai ulang. Minimal 4: yang keluar layar, sekarang, berikutnya, dan yang masuk saat transisi")]
    [SerializeField] private WagonScript[] wagons;
    [Tooltip("Jarak antar pusat gerbong (lebar gerbong + sambungan)")]
    [SerializeField] private float wagonSpacing = 22f;
    [Tooltip("Jarak awal kereta di luar layar sebelum datang")]
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
    private IList<WagonPlan> plans; // jumlah box dan label peran tiap gerbong

    // +1: kereta masuk dari kanan dan bergerak ke kiri (default). -1: masuk dari kiri dan bergerak ke kanan.
    // Juga sisi tempat gerbong berikutnya tersusun (di belakang lokomotif)
    private int side = 1;
    private Vector3 locomotiveBasePosition;
    private Vector3 locomotiveBaseScale;

    void Awake()
    {
        baseX = transform.position.x;
        if (locomotive != null)
        {
            locomotiveBasePosition = locomotive.localPosition;
            locomotiveBaseScale = locomotive.localScale;
        }
    }

    // Dipanggil sebelum Setup. fromLeft = kereta masuk dari kiri layar dan bergerak ke kanan
    public void SetDirection(bool fromLeft)
    {
        side = fromLeft ? -1 : 1;
        if (locomotive == null) return;
        // Lokomotif tetap di depan rangkaian dan menghadap ke arah gerak
        Vector3 p = locomotiveBasePosition;
        p.x *= side;
        locomotive.localPosition = p;
        Vector3 s = locomotiveBaseScale;
        s.x = Mathf.Abs(s.x) * side;
        locomotive.localScale = s;
    }

    // Siapkan kereta di luar layar dengan jumlah gerbong = jumlah langkah level (tutorial + soal).
    // wagonPlans[i] diterapkan saat gerbong ke-i dipasang (masih di luar layar)
    public void Setup(IList<WagonPlan> wagonPlans)
    {
        totalWagons = wagonPlans.Count;
        plans = wagonPlans;
        CurrentIndex = 0;
        wagonIndex = new int[wagons.Length];
        for (int i = 0; i < wagons.Length; i++) AssignWagon(i, i);
        float start = side > 0
            ? Mathf.Max(baseX + arriveDistance, OffscreenX())
            : Mathf.Min(baseX - arriveDistance, OffscreenX());
        SetX(start);
    }

    // Posisi x kereta agar seluruh bagiannya (termasuk lokomotif) berada di luar layar pada sisi kedatangan
    float OffscreenX()
    {
        Camera cam = Camera.main;
        float halfWidth = cam.orthographicSize * cam.aspect;
        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            minX = Mathf.Min(minX, r.bounds.min.x);
            maxX = Mathf.Max(maxX, r.bounds.max.x);
        }
        if (minX == float.MaxValue) return transform.position.x;
        if (side > 0) return cam.transform.position.x + halfWidth + (transform.position.x - minX) + 1f;
        return cam.transform.position.x - halfWidth - (maxX - transform.position.x) - 1f;
    }

    // Lokomotif hanya penarik di awal: hapus permanen setelah keluar dari layar di sisi tujuan
    void DestroyLocomotiveIfOffscreen()
    {
        if (locomotive == null) return;
        Camera cam = Camera.main;
        float halfWidth = cam.orthographicSize * cam.aspect;
        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (Renderer r in locomotive.GetComponentsInChildren<Renderer>())
        {
            minX = Mathf.Min(minX, r.bounds.min.x);
            maxX = Mathf.Max(maxX, r.bounds.max.x);
        }
        bool offscreen = side > 0
            ? maxX < cam.transform.position.x - halfWidth
            : minX > cam.transform.position.x + halfWidth;
        if (offscreen)
        {
            Destroy(locomotive.gameObject);
            locomotive = null;
        }
    }

    public IEnumerator Arrive()
    {
        AudioManager.Instance.PlayTrainMoveSound();
        yield return MoveTo(baseX, arriveDuration);
    }

    public IEnumerator MoveToNext()
    {
        AudioManager.Instance.PlayTrainMoveSound();

        // Sebelum kereta bergerak: gerbong yang sudah 2 posisi lewat (tidak terlihat) disambung ke ujung belakang.
        // Posisinya masih di luar layar, jadi gerbong ikut masuk layar bersama rangkaian, tidak muncul tiba-tiba.
        // Gerbong di luar jumlah soal dinonaktifkan oleh AssignWagon, jadi di soal terakhir tidak ada gerbong tambahan.
        for (int i = 0; i < wagons.Length; i++)
        {
            if (wagonIndex[i] <= CurrentIndex - 2) AssignWagon(i, LastWagonIndex() + 1);
        }

        CurrentIndex++;
        yield return MoveTo(baseX - side * CurrentIndex * wagonSpacing, moveDuration);
        DestroyLocomotiveIfOffscreen();
    }

    // Indeks gerbong paling belakang dalam rangkaian saat ini
    int LastWagonIndex()
    {
        int last = int.MinValue;
        foreach (int index in wagonIndex) last = Mathf.Max(last, index);
        return last;
    }

    // Kereta berangkat meninggalkan layar searah geraknya
    public IEnumerator Depart()
    {
        AudioManager.Instance.PlayTrainMoveSound();
        yield return MoveTo(transform.position.x - side * arriveDistance, departDuration);
    }

    void AssignWagon(int physical, int logical)
    {
        wagonIndex[physical] = logical;
        WagonScript wagon = wagons[physical];
        wagon.ReleaseBoxes();
        wagon.gameObject.SetActive(logical < totalWagons);
        if (logical < totalWagons) wagon.Configure(plans[logical]);

        Vector3 p = wagon.transform.localPosition;
        p.x = side * logical * wagonSpacing;
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
