using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class GuidePage
{
    [Tooltip("Screenshot kejadian permainan yang dijelaskan")]
    public Sprite image;

    [TextArea(2, 5)]
    [Tooltip("Penjelasan untuk gambar di atas")]
    public string text;
}

// Panel panduan bergambar yang tampil sebelum permainan dimulai.
// << dan >> untuk berpindah halaman, X untuk menutup panduan dan mulai bermain
[RequireComponent(typeof(CanvasGroup))]
public class GuidePanel : MonoBehaviour
{
    [SerializeField] private List<GuidePage> pages = new List<GuidePage>();

    [Header("Referensi UI")]
    [SerializeField] private Image pageImage;
    [SerializeField] private TMP_Text pageText;
    [Tooltip("Penanda halaman, mis. 1 / 5 (opsional)")]
    [SerializeField] private TMP_Text pageCounter;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;
    [Tooltip("Panel yang dianimasikan: bergeser masuk dari kiri saat muncul, mengecil saat ditutup")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private float fadeDuration = 0.25f;
    [Tooltip("Durasi panel bergeser masuk dari luar layar kiri")]
    [SerializeField] private float slideInDuration = 0.6f;

    // Dipanggil setelah panduan ditutup dengan tombol X
    public event Action Closed;

    public bool IsOpen { get; private set; }

    private CanvasGroup group;
    private Coroutine routine;
    private int index;
    private Vector2 panelHome;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (panel != null) panelHome = panel.anchoredPosition;
        SetVisible(0f);
        group.interactable = false;
        group.blocksRaycasts = false;

        prevButton.onClick.AddListener(Prev);
        nextButton.onClick.AddListener(Next);
        closeButton.onClick.AddListener(Close);
    }

    void Update()
    {
        if (!IsOpen || PauseMenu.IsPaused) return;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Prev();
        else if (Input.GetKeyDown(KeyCode.RightArrow)) Next();
    }

    public void Show()
    {
        index = 0;
        Refresh();
        IsOpen = true;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(SlideIn());
    }

    public void Next()
    {
        if (index >= pages.Count - 1) return;
        index++;
        Refresh();
    }

    public void Prev()
    {
        if (index <= 0) return;
        index--;
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(0f));
        Closed?.Invoke();
    }

    void Refresh()
    {
        if (pages.Count == 0) return;
        GuidePage page = pages[index];

        pageImage.sprite = page.image;
        pageImage.enabled = page.image != null;
        pageImage.preserveAspect = true;
        pageText.text = page.text;
        if (pageCounter != null) pageCounter.text = (index + 1) + " / " + pages.Count;

        // Halaman pertama tanpa <<, halaman terakhir tanpa >>
        prevButton.gameObject.SetActive(index > 0);
        nextButton.gameObject.SetActive(index < pages.Count - 1);
    }

    // Latar gelap memudar masuk sementara panel bergeser dari luar layar kiri ke tengah
    IEnumerator SlideIn()
    {
        // Tombol langsung aktif agar tidak tampak pudar (warna disabled) selama panel bergeser
        group.interactable = true;
        group.blocksRaycasts = true;
        float startAlpha = group.alpha;
        Vector2 from = panelHome + Vector2.left * OffscreenDistance();
        if (panel != null) panel.localScale = Vector3.one;

        float duration = Mathf.Max(fadeDuration, slideInDuration);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetVisible(Mathf.Lerp(startAlpha, 1f, t / fadeDuration));
            if (panel != null) panel.anchoredPosition = Vector2.LerpUnclamped(from, panelHome, EaseOutBack(Mathf.Clamp01(t / slideInDuration)));
            yield return null;
        }

        SetVisible(1f);
        if (panel != null) panel.anchoredPosition = panelHome;
        routine = null;
    }

    IEnumerator Fade(float target)
    {
        float start = group.alpha;
        group.interactable = false;
        group.blocksRaycasts = target > 0f;

        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            float alpha = Mathf.Lerp(start, target, t / fadeDuration);
            SetVisible(alpha);
            if (panel != null) panel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, alpha);
            yield return null;
        }

        SetVisible(target);
        group.interactable = target > 0f;
        group.blocksRaycasts = target > 0f;
        routine = null;
    }

    void SetVisible(float alpha)
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        group.alpha = alpha;
    }

    // Jarak geser agar panel mulai tepat di luar tepi kiri layar
    float OffscreenDistance()
    {
        if (panel == null) return 0f;
        var parent = panel.parent as RectTransform;
        float halfScreen = parent != null ? parent.rect.width * 0.5f : 960f;
        return halfScreen + panel.rect.width * 0.5f + 50f;
    }

    // Melambat di akhir dengan sedikit melewati posisi lalu kembali, supaya terasa "mendarat"
    static float EaseOutBack(float k)
    {
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
