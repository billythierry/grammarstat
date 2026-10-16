using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup detail tiket: layar digelapkan, panel tiket, dan tombol jawaban A / B / No Error
[RequireComponent(typeof(CanvasGroup))]
public class TicketDetailUI : MonoBehaviour
{
    public const string AnswerA = "A";
    public const string AnswerB = "B";
    public const string AnswerNoError = "No Error";

    [Header("Isi tiket")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text ageText;
    [SerializeField] private TMP_Text noteText;
    [Tooltip("Warna kata bagian soal (A / B) pada kalimat note")]
    [SerializeField] private Color highlightColor = new Color(0.12f, 0.31f, 0.61f);

    [Header("Tombol jawaban")]
    [SerializeField] private Button buttonA;
    [SerializeField] private Button buttonB;
    [SerializeField] private Button buttonNoError;
    [Tooltip("Panel tiket yang dianimasikan (skala) saat popup muncul")]
    [SerializeField] private RectTransform popupBody;
    [SerializeField] private float fadeDuration = 0.25f;

    // Dipanggil dengan jawaban yang dipilih pemain (AnswerA, AnswerB, AnswerNoError)
    public event Action<string> AnswerSelected;

    public bool IsOpen { get; private set; }

    private CanvasGroup group;
    private Coroutine routine;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        SetVisible(0f);
        group.interactable = false;
        group.blocksRaycasts = false;

        buttonA.onClick.AddListener(() => Select(AnswerA));
        buttonB.onClick.AddListener(() => Select(AnswerB));
        buttonNoError.onClick.AddListener(() => Select(AnswerNoError));
    }

    public void Show(TicketData ticket)
    {
        if (nameText != null) nameText.text = ticket.passengerName;
        if (ageText != null) ageText.text = ticket.age.ToString();
        if (noteText != null) noteText.text = ticket.GetFormattedNote("#" + ColorUtility.ToHtmlStringRGB(highlightColor));
        IsOpen = true;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(1f));
    }

    public void Hide()
    {
        IsOpen = false;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(0f));
    }

    void Select(string answer)
    {
        if (!IsOpen) return;
        AnswerSelected?.Invoke(answer);
    }

    IEnumerator Fade(float target)
    {
        float start = group.alpha;
        // tombol hanya aktif saat popup terbuka penuh
        group.interactable = false;
        group.blocksRaycasts = target > 0f;

        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            SetVisible(Mathf.Lerp(start, target, t / fadeDuration));
            yield return null;
        }

        SetVisible(target);
        group.interactable = target > 0f;
        group.blocksRaycasts = target > 0f;
        routine = null;
    }

    // Hanya mengatur tampilan; status klik (blocksRaycasts/interactable) diatur di Fade
    void SetVisible(float alpha)
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        group.alpha = alpha;
        if (popupBody != null) popupBody.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, alpha);
    }
}
