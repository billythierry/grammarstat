using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel soal Runaway: kalimat, bar waktu, dan 4 tombol tense dengan posisi tetap
[RequireComponent(typeof(CanvasGroup))]
public class RunawayQuizUI : MonoBehaviour
{
    [SerializeField] private TMP_Text sentenceText;
    [Tooltip("Tombol jawaban, urut sesuai enum Tense: Simple Present, Present Continuous, Simple Past, Future")]
    [SerializeField] private Button[] answerButtons = new Button[4];
    [Tooltip("Isi bar waktu. Pivot kiri, lebarnya diskalakan sesuai sisa waktu")]
    [SerializeField] private RectTransform timerFill;
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Warna")]
    [SerializeField] private Color correctColor = new Color(0.45f, 0.85f, 0.35f);
    [SerializeField] private Color wrongColor = new Color(0.95f, 0.35f, 0.35f);
    [Tooltip("Warna bar waktu saat waktu hampir habis")]
    [SerializeField] private Color timerWarningColor = new Color(0.95f, 0.35f, 0.35f);
    [Tooltip("Sisa waktu (0-1) saat bar mulai berwarna peringatan")]
    [SerializeField] private float timerWarningThreshold = 0.3f;

    // Dipanggil dengan tense yang dipilih pemain
    public event Action<Tense> AnswerSelected;

    private CanvasGroup group;
    private Coroutine routine;
    private Color[] buttonColors;
    private Image timerImage;
    private Color timerColor;
    private bool locked = true;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        buttonColors = new Color[answerButtons.Length];
        for (int i = 0; i < answerButtons.Length; i++)
        {
            Tense tense = (Tense)i;
            buttonColors[i] = answerButtons[i].image.color;
            answerButtons[i].onClick.AddListener(() => Select(tense));
            TMP_Text label = answerButtons[i].GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = TenseNames.Get(tense);
        }

        if (timerFill != null)
        {
            timerImage = timerFill.GetComponent<Image>();
            if (timerImage != null) timerColor = timerImage.color;
        }
    }

    public void ShowQuestion(TenseQuestion question)
    {
        sentenceText.text = question.sentence;
        for (int i = 0; i < answerButtons.Length; i++) answerButtons[i].image.color = buttonColors[i];
        SetTimer(1f);
        locked = false;
        FadeTo(1f);
    }

    public void Hide()
    {
        locked = true;
        FadeTo(0f);
    }

    // fraction: sisa waktu 0-1
    public void SetTimer(float fraction)
    {
        if (timerFill == null) return;
        fraction = Mathf.Clamp01(fraction);
        timerFill.localScale = new Vector3(fraction, 1f, 1f);
        if (timerImage != null) timerImage.color = fraction <= timerWarningThreshold ? timerWarningColor : timerColor;
    }

    // Kunci tombol lalu tandai jawaban: pilihan pemain merah (bila salah), jawaban benar hijau.
    // chosen = null bila waktu habis
    public void ShowResult(Tense? chosen, Tense correct)
    {
        locked = true;
        if (chosen.HasValue && chosen.Value != correct) answerButtons[(int)chosen.Value].image.color = wrongColor;
        answerButtons[(int)correct].image.color = correctColor;
    }

    void Select(Tense tense)
    {
        if (locked) return;
        locked = true;
        AnswerSelected?.Invoke(tense);
    }

    void FadeTo(float target)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(target));
    }

    IEnumerator Fade(float target)
    {
        group.blocksRaycasts = target > 0f;
        float start = group.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        group.alpha = target;
        routine = null;
    }
}
