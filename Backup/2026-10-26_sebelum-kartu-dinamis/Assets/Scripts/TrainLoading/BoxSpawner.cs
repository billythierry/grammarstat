using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class BoxSpawner : MonoBehaviour
{
    [FormerlySerializedAs("cardPrefab")]
    [SerializeField] public GameObject boxPrefab;
    [SerializeField] public SpawnAreaScript[] spawnAreas;
    [Tooltip("Kereta yang membawa drop area (kolom jawaban) di tiap gerbong")]
    [SerializeField] private TrainController train;
    [Tooltip("Panel UI untuk notifikasi jawaban benar/salah")]
    [SerializeField] private NotificationUI notification;
    [Tooltip("Teks penghitung soal, mis. 1/15")]
    [SerializeField] private TMP_Text questionCounterText;

    [Header("Questions")]
    [SerializeField] private QuestionBank questionBank;
    [Tooltip("Jumlah soal per level. 0 = semua soal di bank")]
    [SerializeField] private int questionCount = 15;
    [Tooltip("Minimal jawaban benar untuk lulus level")]
    [SerializeField] private int passingScore = 7;
    [Tooltip("Acak urutan soal setiap kali level dimulai")]
    [SerializeField] private bool shuffleQuestions = true;
    [Tooltip("Jika aktif, soal baru berlanjut bila jawaban benar. Jika tidak, tetap lanjut walau salah")]
    [SerializeField] private bool advanceOnlyWhenCorrect = false;
    [SerializeField] private char separator = '-';

    [Tooltip("Posisi z box, harus lebih kecil (lebih dekat kamera) dari z spawn/drop area")]
    [FormerlySerializedAs("cardZ")]
    [SerializeField] private float boxZ = -1f;

    // Urutan kata yang benar untuk soal yang sedang tampil
    public string[] CorrectWords { get; private set; }
    public Question CurrentQuestion { get; private set; }
    public int CurrentQuestionNumber => orderIndex + 1;
    public int TotalQuestions => questionOrder.Count;
    public int CorrectCount { get; private set; }
    public bool IsFinished { get; private set; }
    // Sedang ada animasi kereta: submit dan reset diabaikan
    public bool IsBusy { get; private set; }

    // Drop area milik gerbong yang sedang berhenti di tengah layar
    public DropAreaScript[] dropAreas => train.CurrentWagon != null ? train.CurrentWagon.DropAreas : new DropAreaScript[0];

    // Box untuk soal yang sedang tampil
    public List<BoxScript> SpawnedBoxes { get; } = new List<BoxScript>();
    // Semua box yang pernah dibuat (pool), dipakai ulang antar soal
    private readonly List<BoxScript> boxPool = new List<BoxScript>();

    private readonly List<int> questionOrder = new List<int>();
    private int orderIndex;

    void Start()
    {
        StartLevel();
    }

    public void StartLevel()
    {
        if (questionBank == null || questionBank.questions.Count == 0)
        {
            Debug.LogError("BoxSpawner: Question Bank belum diisi atau kosong");
            return;
        }

        questionOrder.Clear();
        for (int i = 0; i < questionBank.questions.Count; i++) questionOrder.Add(i);
        if (shuffleQuestions) ShuffleInPlace(questionOrder);

        int count = questionCount > 0 ? questionCount : questionOrder.Count;
        if (count > questionOrder.Count)
        {
            Debug.LogWarning("BoxSpawner: level butuh " + count + " soal, tapi bank hanya punya " + questionOrder.Count + " soal");
            count = questionOrder.Count;
        }
        questionOrder.RemoveRange(count, questionOrder.Count - count);

        orderIndex = 0;
        CorrectCount = 0;
        IsFinished = false;
        UpdateQuestionCounter();
        StartCoroutine(BeginLevelRoutine());
    }

    void UpdateQuestionCounter()
    {
        if (questionCounterText != null) questionCounterText.text = CurrentQuestionNumber + "/" + TotalQuestions;
    }

    IEnumerator BeginLevelRoutine()
    {
        IsBusy = true;
        train.Setup(TotalQuestions);
        yield return train.Arrive();
        LoadQuestion(questionBank.questions[questionOrder[orderIndex]]);
        IsBusy = false;
    }

    void LoadQuestion(Question question)
    {
        CurrentQuestion = question;
        CorrectWords = question.GetWords(separator);

        if (CorrectWords.Length > spawnAreas.Length)
            Debug.LogWarning("BoxSpawner: soal " + question.id + " punya " + CorrectWords.Length + " kata, tapi spawn area hanya " + spawnAreas.Length + ". Sebagian kata tidak di-spawn.");

        PlaceBoxes(Shuffle(CorrectWords));
        UpdateQuestionCounter();
        Debug.Log("Soal " + CurrentQuestionNumber + "/" + TotalQuestions + " (" + question.id + ") : " + string.Join(" ", CorrectWords));
    }

    // Tempatkan box di spawn area sesuai urutan kata yang sudah diacak
    void PlaceBoxes(string[] shuffled)
    {
        int count = Mathf.Min(shuffled.Length, spawnAreas.Length);

        // Lepas semua box dulu, supaya spawn area tidak terhalang box lain
        foreach (BoxScript box in SpawnedBoxes) box.DetachFromArea();

        while (SpawnedBoxes.Count < count) SpawnedBoxes.Add(GetFreeBox());
        while (SpawnedBoxes.Count > count)
        {
            SpawnedBoxes[SpawnedBoxes.Count - 1].Release();
            SpawnedBoxes.RemoveAt(SpawnedBoxes.Count - 1);
        }

        for (int i = 0; i < count; i++)
        {
            BoxScript box = SpawnedBoxes[i];
            box.gameObject.SetActive(true);
            box.SetWord(shuffled[i]);
            box.SetHomeSpawnArea(spawnAreas[i]);
            box.ResetToHome();
        }
    }

    // Ambil box nonaktif dari pool, atau buat baru bila pool habis
    BoxScript GetFreeBox()
    {
        foreach (BoxScript box in boxPool)
        {
            if (!box.gameObject.activeSelf && !box.IsLocked && !SpawnedBoxes.Contains(box)) return box;
        }

        GameObject boxObject = Instantiate(boxPrefab);
        boxObject.SetActive(true);

        // Box harus di depan collider spawn/drop area (z lebih dekat ke kamera),
        // kalau sama-sama z = 0 klik kadang mengenai area, bukan box
        Vector3 p = boxObject.transform.position;
        p.z = boxZ;
        boxObject.transform.position = p;

        BoxScript newBox = boxObject.GetComponent<BoxScript>();
        boxPool.Add(newBox);
        return newBox;
    }

    // Dipanggil tombol Reset: semua box ditarik kembali ke spawn area dengan urutan diacak ulang
    public void ResetBoxes()
    {
        if (IsBusy || IsFinished || CorrectWords == null) return;
        string[] shuffled = Shuffle(CorrectWords);
        PlaceBoxes(shuffled);
        Debug.Log("Box diacak ulang: " + string.Join(" | ", shuffled));
    }

    // Dipanggil tombol Submit: cek apakah urutan box di drop area sudah benar
    public bool SubmitAnswer()
    {
        if (IsBusy || IsFinished || CorrectWords == null) return false;

        var answer = new List<string>();
        foreach (DropAreaScript area in dropAreas)
        {
            if (area == null || area.CurrentBox == null)
            {
                if (notification != null) notification.ShowInfo("Lengkapi semua kolom jawaban terlebih dahulu");
                return false;
            }
            answer.Add(area.CurrentBox.Word);
        }

        bool correct = answer.Count == CorrectWords.Length && SameOrder(answer.ToArray(), CorrectWords);
        if (correct) CorrectCount++;
        bool advance = correct || !advanceOnlyWhenCorrect;
        if (notification != null)
        {
            // Saat pindah gerbong, notifikasi tetap tampil selama kereta bergerak lalu disembunyikan di NextQuestionRoutine
            if (correct) notification.ShowCorrect("<b>Jawaban Benar!</b>", advance);
            else notification.ShowWrong("<b>Jawaban Salah!</b>\n<size=70%><color=#FFFFFF>Jawaban benar: " + string.Join(" ", CorrectWords) + "</color></size>", advance);
        }

        if (advance) StartCoroutine(NextQuestionRoutine());
        return correct;
    }

    // Versi void untuk event onClick Button
    public void OnSubmitButton()
    {
        SubmitAnswer();
    }

    IEnumerator NextQuestionRoutine()
    {
        IsBusy = true;

        // Box jawaban dimuat ke gerbong dan ikut berangkat bersama kereta
        train.CurrentWagon.LoadBoxes();
        SpawnedBoxes.Clear();

        orderIndex++;
        if (orderIndex < questionOrder.Count)
        {
            yield return train.MoveToNext();
            if (notification != null) notification.Hide();
            LoadQuestion(questionBank.questions[questionOrder[orderIndex]]);
        }
        else
        {
            orderIndex = questionOrder.Count - 1;
            yield return train.Depart();
            IsFinished = true;
            bool passed = CorrectCount >= passingScore;
            if (notification != null)
            {
                string result = "<b>Level Selesai!</b>\n<size=70%>Benar " + CorrectCount + " dari " + TotalQuestions + " soal - " + (passed ? "LULUS" : "TIDAK LULUS") + "</size>";
                // Tetap tampil sampai level dimulai ulang
                if (passed) notification.ShowCorrect(result, true);
                else notification.ShowWrong(result, true);
            }
        }

        IsBusy = false;
    }

    // Fisher-Yates; diulang bila hasilnya kebetulan sama dengan urutan benar
    static string[] Shuffle(string[] source)
    {
        string[] result = (string[])source.Clone();
        if (result.Length < 2) return result;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            for (int i = result.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                string tmp = result[i];
                result[i] = result[j];
                result[j] = tmp;
            }
            if (!SameOrder(result, source)) break;
        }
        return result;
    }

    static void ShuffleInPlace(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    static bool SameOrder(string[] a, string[] b)
    {
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
