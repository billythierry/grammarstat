using System;
using System.Collections;
using UnityEngine;

// Kereta Runaway: selama permainan terus terdorong ke kanan (melambat, mendekati bandit).
// Jawaban benar memberi Boost: kereta meluncur ke kiri sejauh boostDistance, lalu kembali terdorong ke kanan.
public class RunawayTrain : MonoBehaviour
{
    [Header("Gerak saat bermain")]
    [Tooltip("Kecepatan kereta terdorong ke kanan (unit/detik)")]
    [SerializeField] private float driftSpeed = 0.5f;
    [Tooltip("Jarak kereta meluncur ke kiri setiap jawaban benar")]
    [SerializeField] private float boostDistance = 3f;
    [Tooltip("Lama luncuran boost (detik)")]
    [SerializeField] private float boostDuration = 0.6f;
    [Tooltip("Batas kiri posisi kereta agar tidak keluar layar")]
    [SerializeField] private float minX = -6f;

    [Header("Animasi masuk / kabur")]
    [Tooltip("Posisi X kereta saat permainan dimulai")]
    [SerializeField] private float defaultX = -4f;
    [Tooltip("Posisi X awal di luar layar kanan")]
    [SerializeField] private float enterX = 14f;
    [SerializeField] private float enterDuration = 2.5f;
    [Tooltip("Posisi X di luar layar kiri saat kereta berhasil kabur")]
    [SerializeField] private float escapeX = -22f;
    [SerializeField] private float escapeDuration = 2.5f;

    [Header("Getaran")]
    [Tooltip("Tinggi getaran kereta saat berjalan")]
    [SerializeField] private float rumbleHeight = 0.03f;
    [SerializeField] private float rumbleSpeed = 18f;

    // Dipanggil sekali saat sisi kanan kereta menyentuh garis jangkauan bandit
    public event Action ReachedBandit;

    public bool IsDrifting { get; private set; }

    private Transform banditLine;
    private Renderer[] renderers;
    private float baseY;
    private float boostElapsed = -1f;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        baseY = transform.position.y;
        // Kereta menunggu di luar layar sampai Enter()
        SetX(enterX);
    }

    // Mulai terdorong ke arah garis bandit
    public void StartDrift(Transform line)
    {
        banditLine = line;
        IsDrifting = true;
    }

    public void StopDrift()
    {
        IsDrifting = false;
        boostElapsed = -1f;
    }

    public void Boost()
    {
        if (IsDrifting) boostElapsed = 0f;
    }

    void Update()
    {
        Rumble();
        if (!IsDrifting) return;

        float x = transform.position.x + driftSpeed * Time.deltaTime;

        // Boost: ease out, cepat di awal lalu melambat
        if (boostElapsed >= 0f)
        {
            float k0 = boostElapsed / boostDuration;
            boostElapsed = Mathf.Min(boostElapsed + Time.deltaTime, boostDuration);
            float k1 = boostElapsed / boostDuration;
            x -= boostDistance * (EaseOut(k1) - EaseOut(k0));
            if (boostElapsed >= boostDuration) boostElapsed = -1f;
        }

        SetX(Mathf.Max(x, minX));

        if (banditLine != null && RightEdge() >= banditLine.position.x)
        {
            StopDrift();
            ReachedBandit?.Invoke();
        }
    }

    public IEnumerator Enter()
    {
        SetX(enterX);
        yield return MoveTo(defaultX, enterDuration, true);
    }

    public IEnumerator Escape()
    {
        StopDrift();
        yield return MoveTo(escapeX, escapeDuration, false);
    }

    IEnumerator MoveTo(float targetX, float duration, bool easeOut)
    {
        float startX = transform.position.x;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            SetX(Mathf.Lerp(startX, targetX, easeOut ? EaseOut(k) : k * k));
            yield return null;
        }
        SetX(targetX);
    }

    float RightEdge()
    {
        float maxX = float.MinValue;
        foreach (Renderer r in renderers) maxX = Mathf.Max(maxX, r.bounds.max.x);
        return maxX == float.MinValue ? transform.position.x : maxX;
    }

    void Rumble()
    {
        Vector3 p = transform.position;
        p.y = baseY + Mathf.Abs(Mathf.Sin(Time.time * rumbleSpeed)) * rumbleHeight;
        transform.position = p;
    }

    static float EaseOut(float k) { return 1f - (1f - k) * (1f - k); }

    void SetX(float x)
    {
        Vector3 p = transform.position;
        p.x = x;
        transform.position = p;
    }
}
