using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel tutorial terpandu: judul bagian, rumus kalimat berwarna, penjelasan, dan petunjuk langkah
public class TutorialPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Baris tempat chip rumus disusun (HorizontalLayoutGroup)")]
    [SerializeField] private RectTransform formulaRow;
    [Tooltip("Template chip rumus (nonaktif), berisi TMP_Text")]
    [SerializeField] private Image chipTemplate;
    [Tooltip("Template tanda + di antara chip (nonaktif)")]
    [SerializeField] private TMP_Text plusTemplate;
    [SerializeField] private TMP_Text explanationText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Color hintColor = new Color32(0xFF, 0xE2, 0x7A, 0xFF);
    [SerializeField] private Color warningColor = new Color32(0xFF, 0x8A, 0x65, 0xFF);
    [Tooltip("Tampilkan rumus kalimat (chip). Nonaktif karena rumus sudah terlihat di label kolom gerbong")]
    [SerializeField] private bool showFormula = false;
    [Tooltip("Tiap baris penjelasan diberi penanda - di awal. Baris yang terlalu panjang turun ke bawah, rata dengan teksnya")]
    [SerializeField] private bool bulletLines = true;
    [Tooltip("Jarak dari atas panel ke penjelasan (pixel)")]
    [SerializeField] private float explanationTop = 76f;
    [Tooltip("Jarak antara penjelasan dan baris peringatan (pixel)")]
    [SerializeField] private float hintSpacing = 4f;
    [Tooltip("Ruang kosong di bawah isi panel (pixel)")]
    [SerializeField] private float bottomPadding = 18f;
    [Tooltip("Ruang kiri-kanan teks di dalam chip (pixel)")]
    [SerializeField] private float chipPadding = 24f;
    [SerializeField] private float fadeDuration = 0.25f;

    private readonly List<Image> chips = new List<Image>();
    private readonly List<TMP_Text> pluses = new List<TMP_Text>();
    private Coroutine fadeRoutine;

    void Awake()
    {
        if (chipTemplate != null) chipTemplate.gameObject.SetActive(false);
        if (plusTemplate != null) plusTemplate.gameObject.SetActive(false);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void Show(TutorialStep step, Color sectionColor)
    {
        if (titleText != null)
        {
            // Nama bagian diberi warna bagian (dicerahkan agar terbaca di latar gelap)
            string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(sectionColor, Color.white, 0.35f));
            titleText.text = "Kalimat <color=#" + hex + ">" + SentenceSection.GetLabel(step.section) + "</color>";
            titleText.color = Color.white;
        }
        if (explanationText != null) explanationText.text = FormatExplanation(step.explanation);
        if (formulaRow != null) formulaRow.gameObject.SetActive(showFormula);
        if (showFormula) BuildFormula(step.GetFormulaParts(), sectionColor);
        SetHint("", false);
        Fade(1f);
    }

    public void Hide()
    {
        Fade(0f);
    }

    public void SetHint(string message, bool warning)
    {
        if (hintText == null) return;
        hintText.text = message;
        hintText.color = warning ? warningColor : hintColor;
        Relayout();
    }

    string FormatExplanation(string text)
    {
        if (!bulletLines || string.IsNullOrEmpty(text)) return text;
        var lines = new List<string>();
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            // indent membuat lanjutan baris yang turun ke bawah sejajar dengan teks, bukan dengan tanda -
            if (trimmed.Length > 0) lines.Add("-<indent=0.9em>" + trimmed + "</indent>");
        }
        return string.Join("\n", lines.ToArray());
    }

    // Tinggi penjelasan mengikuti jumlah baris (ukuran font tetap), panel ikut memanjang ke bawah.
    // Baris peringatan hanya diberi tempat saat ada isinya
    void Relayout()
    {
        if (explanationText == null) return;
        RectTransform explanationRect = explanationText.rectTransform;
        float height = explanationText.GetPreferredValues(explanationText.text, explanationRect.rect.width, 0f).y;
        explanationRect.anchoredPosition = new Vector2(explanationRect.anchoredPosition.x, -explanationTop);
        explanationRect.sizeDelta = new Vector2(explanationRect.sizeDelta.x, height);
        float bottom = explanationTop + height;

        if (hintText != null)
        {
            bool hasHint = !string.IsNullOrEmpty(hintText.text);
            RectTransform hintRect = hintText.rectTransform;
            hintRect.anchoredPosition = new Vector2(hintRect.anchoredPosition.x, -(bottom + hintSpacing));
            if (hasHint) bottom += hintSpacing + hintRect.sizeDelta.y;
        }

        RectTransform panel = (RectTransform)transform;
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, bottom + bottomPadding);
    }

    // Rumus ditampilkan sebagai chip berwarna: [Subject] + [Verb 1] + [Object]
    void BuildFormula(string[] parts, Color sectionColor)
    {
        if (formulaRow == null || chipTemplate == null) return;
        while (chips.Count < parts.Length) chips.Add(Instantiate(chipTemplate, formulaRow));
        while (plusTemplate != null && pluses.Count < Mathf.Max(0, parts.Length - 1)) pluses.Add(Instantiate(plusTemplate, formulaRow));

        // Susun ulang urutan anak: chip, +, chip, +, ...
        int sibling = 0;
        for (int i = 0; i < chips.Count; i++)
        {
            Image chip = chips[i];
            bool used = i < parts.Length;
            chip.gameObject.SetActive(used);
            if (!used) continue;

            chip.color = sectionColor;
            TMP_Text label = chip.GetComponentInChildren<TMP_Text>(true);
            label.text = parts[i];
            label.ForceMeshUpdate();
            RectTransform rect = chip.rectTransform;
            rect.sizeDelta = new Vector2(label.preferredWidth + chipPadding * 2f, rect.sizeDelta.y);
            chip.transform.SetSiblingIndex(sibling++);

            if (i < pluses.Count)
            {
                bool plusUsed = i < parts.Length - 1;
                pluses[i].gameObject.SetActive(plusUsed);
                if (plusUsed) pluses[i].transform.SetSiblingIndex(sibling++);
            }
        }
        for (int i = parts.Length; i < pluses.Count; i++) pluses[i].gameObject.SetActive(false);
        LayoutRebuilder.ForceRebuildLayoutImmediate(formulaRow);
    }

    void Fade(float target)
    {
        if (canvasGroup == null) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float start = canvasGroup.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = target;
        fadeRoutine = null;
    }
}
