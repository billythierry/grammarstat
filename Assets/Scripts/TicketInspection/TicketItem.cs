using System;
using System.Collections;
using UnityEngine;

// Tiket yang keluar dari slot tiket dan bisa diklik untuk membuka detail tiket
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class TicketItem : MonoBehaviour
{
    [SerializeField] private float appearDuration = 0.6f;
    [SerializeField] private float startScale = 0.35f;
    [Tooltip("Rotasi akhir tiket di atas meja (derajat)")]
    [SerializeField] private float restAngle = -4f;
    [Tooltip("Skala saat kursor berada di atas tiket")]
    [SerializeField] private float hoverScale = 1.08f;

    public event Action Clicked;

    // Tiket hanya bisa diklik setelah selesai keluar dari slot dan saat UI detail tertutup
    public bool IsClickable { get; set; }

    private SpriteRenderer sr;
    private Collider2D col;
    private Vector3 baseScale;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        baseScale = transform.localScale;
    }

    // Tiket muncul dari posisi slot lalu meluncur ke atas meja
    public void Appear(Vector3 slotPosition, Vector3 deskPosition, Action onDone = null)
    {
        StopAllCoroutines();
        gameObject.SetActive(true);
        StartCoroutine(AppearRoutine(slotPosition, deskPosition, onDone));
    }

    public void Hide()
    {
        StopAllCoroutines();
        IsClickable = false;
        col.enabled = false;
        gameObject.SetActive(false);
    }

    IEnumerator AppearRoutine(Vector3 from, Vector3 to, Action onDone)
    {
        IsClickable = false;
        col.enabled = false;
        Color c = sr.color;

        for (float t = 0f; t < appearDuration; t += Time.deltaTime)
        {
            float k = t / appearDuration;
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            transform.position = Vector3.Lerp(from, to, eased);
            transform.localScale = baseScale * Mathf.Lerp(startScale, 1f, eased);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, restAngle, eased));
            c.a = Mathf.Clamp01(k * 3f);
            sr.color = c;
            yield return null;
        }

        transform.position = to;
        transform.localScale = baseScale;
        transform.rotation = Quaternion.Euler(0f, 0f, restAngle);
        c.a = 1f;
        sr.color = c;

        col.enabled = true;
        IsClickable = true;
        onDone?.Invoke();
    }

    void OnMouseEnter()
    {
        if (IsClickable) transform.localScale = baseScale * hoverScale;
    }

    void OnMouseExit()
    {
        transform.localScale = baseScale;
    }

    void OnMouseDown()
    {
        if (!IsClickable) return;
        transform.localScale = baseScale;
        Clicked?.Invoke();
    }
}
