using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Rombongan bandit: masuk dari kanan lalu diam di tempat dengan animasi berderap.
// Background yang bergerak membuat bandit terlihat ikut mengejar.
public class BanditGroup : MonoBehaviour
{
    [Tooltip("Garis jangkauan bandit. Kereta yang menyentuh garis ini dirampok (kalah)")]
    [SerializeField] private SpriteRenderer reachLine;
    [Tooltip("Posisi X bandit saat mengejar")]
    [SerializeField] private float chaseX = 7f;
    [Tooltip("Posisi X awal di luar layar kanan")]
    [SerializeField] private float enterX = 14f;
    [SerializeField] private float enterDuration = 1.5f;
    [Tooltip("Jarak bandit maju saat berhasil menyusul kereta")]
    [SerializeField] private float catchUpDistance = 1.5f;
    [SerializeField] private float catchUpDuration = 0.6f;

    [Header("Animasi berderap")]
    [SerializeField] private float gallopHeight = 0.15f;
    [SerializeField] private float gallopSpeed = 9f;

    public Transform ReachLine => reachLine != null ? reachLine.transform : null;

    private Transform[] riders;
    private float[] riderBaseY;
    private float lineAlpha;

    void Awake()
    {
        // Setiap anak selain garis jangkauan dianggap satu bandit
        var list = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (reachLine == null || child != reachLine.transform) list.Add(child);
        }
        riders = list.ToArray();
        // Bandit menunggu di luar layar sampai Enter()
        SetX(enterX);
        riderBaseY = new float[riders.Length];
        for (int i = 0; i < riders.Length; i++) riderBaseY[i] = riders[i].localPosition.y;

        if (reachLine != null)
        {
            lineAlpha = reachLine.color.a;
            SetLineAlpha(0f);
        }
    }

    void Update()
    {
        // Tiap bandit berderap dengan fase berbeda
        for (int i = 0; i < riders.Length; i++)
        {
            Vector3 p = riders[i].localPosition;
            p.y = riderBaseY[i] + Mathf.Abs(Mathf.Sin(Time.time * gallopSpeed + i * 1.3f)) * gallopHeight;
            riders[i].localPosition = p;
        }
    }

    public IEnumerator Enter()
    {
        SetX(enterX);
        yield return MoveTo(chaseX, enterDuration);
        // Garis jangkauan muncul setelah bandit sampai
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            SetLineAlpha(Mathf.Lerp(0f, lineAlpha, t / 0.4f));
            yield return null;
        }
        SetLineAlpha(lineAlpha);
    }

    // Bandit maju menyusul kereta saat pemain kalah
    public IEnumerator CatchUp()
    {
        yield return MoveTo(transform.position.x - catchUpDistance, catchUpDuration);
    }

    IEnumerator MoveTo(float targetX, float duration)
    {
        float startX = transform.position.x;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            SetX(Mathf.Lerp(startX, targetX, 1f - (1f - k) * (1f - k)));
            yield return null;
        }
        SetX(targetX);
    }

    void SetLineAlpha(float a)
    {
        if (reachLine == null) return;
        Color c = reachLine.color;
        c.a = a;
        reachLine.color = c;
    }

    void SetX(float x)
    {
        Vector3 p = transform.position;
        p.x = x;
        transform.position = p;
    }
}
