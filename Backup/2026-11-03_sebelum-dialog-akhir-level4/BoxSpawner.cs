using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BoxSpawner : MonoBehaviour
{
    [FormerlySerializedAs("cardPrefab")]
    [SerializeField] public GameObject boxPrefab;
    [Tooltip("Spawn area di peron, sebanyak SlotLayout.MaxSlots. Yang aktif mengikuti jumlah box soal")]
    [SerializeField] public SpawnAreaScript[] spawnAreas;
    [Tooltip("Kereta yang membawa drop area (kolom jawaban) di tiap gerbong")]
    [SerializeField] private TrainController train;
    [Tooltip("Panel UI untuk notifikasi jawaban benar/salah")]
    [SerializeField] private NotificationUI notification;
    [Tooltip("Panel tutorial terpandu (rumus, penjelasan, petunjuk)")]
    [SerializeField] private TutorialPanel tutorialPanel;
    [Tooltip("Teks label bagian yang sedang dimainkan, mis. Positif (+)")]
    [FormerlySerializedAs("questionCounterText")]
    [SerializeField] private TMP_Text sectionLabelText;
    [Tooltip("Latar label bagian, warnanya mengikuti bagian")]
    [SerializeField] private Image sectionLabelBackground;
    [Tooltip("Transisi masuk level dengan nama materi (opsional)")]
    [SerializeField] private LevelIntro levelIntro;
    [Tooltip("Panel hasil akhir level. Bila kosong, hasil tampil di notifikasi")]
    [SerializeField] private ResultPanel resultPanel;
    [Tooltip("Panduan cara bermain bergambar, tampil setelah intro. Kereta baru datang setelah panduan ditutup")]
    [SerializeField] private GuidePanel guidePanel;
    [Tooltip("Level yang menampilkan panduan. 0 = semua level")]
    [SerializeField] private int guideLevel = 1;

    [Header("Questions")]
    [Tooltip("Bank soal per level, urut dari Level 1. Level dipilih dari menu (LevelSelection.Level)")]
    [SerializeField] private QuestionBank[] levelBanks;
    [Tooltip("Bank cadangan bila level yang dipilih belum punya bank di Level Banks")]
    [SerializeField] private QuestionBank questionBank;
    [Tooltip("Minimal jawaban benar di tiap bagian Positif / Negatif / Tanya untuk lulus")]
    [SerializeField] private int minCorrectPerSection = 3;
    [Tooltip("Minimal jawaban benar di bagian Review untuk lulus")]
    [SerializeField] private int minCorrectReview = 2;
    [Header("Bintang (hanya bila lulus; lulus = 1 bintang)")]
    [Tooltip("Minimal persentase jawaban benar dari semua soal untuk 2 bintang")]
    [Range(0f, 1f)] [SerializeField] private float twoStarRatio = 0.75f;
    [Tooltip("Minimal persentase jawaban benar dari semua soal untuk 3 bintang")]
    [Range(0f, 1f)] [SerializeField] private float threeStarRatio = 0.9f;
    [Tooltip("Jika aktif, soal baru berlanjut bila jawaban benar. Jika tidak, tetap lanjut walau salah")]
    [SerializeField] private bool advanceOnlyWhenCorrect = false;
    [SerializeField] private char separator = '-';
    [Tooltip("Jeda setelah kalimat contoh tutorial lengkap sebelum kereta berangkat")]
    [SerializeField] private float tutorialCompleteDelay = 0.8f;
    [Tooltip("Warna kolom yang menyala sebagai petunjuk tutorial (kuning agar terlihat di gerbong warna apa pun)")]
    [SerializeField] private Color guideColor = new Color32(0xFF, 0xE2, 0x7A, 0xFF);

    [Tooltip("Posisi z box, harus lebih kecil (lebih dekat kamera) dari z spawn/drop area")]
    [FormerlySerializedAs("cardZ")]
    [SerializeField] private float boxZ = -1f;

    // Satu langkah level: soal biasa, atau tutorial terpandu (tidak dinilai)
    class LevelStep
    {
        public Question question;
        public TutorialStep tutorial;
        public bool IsTutorial => tutorial != null;
    }

    // Urutan kata yang benar untuk soal yang sedang tampil
    public string[] CorrectWords { get; private set; }
    // Kalimat utama + susunan alternatif yang juga dinilai benar
    public List<string[]> AcceptedAnswers { get; private set; }
    public Question CurrentQuestion { get; private set; }
    public bool IsTutorialActive { get; private set; }
    // Jumlah soal yang dinilai (tanpa tutorial)
    public int TotalQuestions { get; private set; }
    public int CorrectCount { get; private set; }
    public bool IsFinished { get; private set; }
    public bool IsPassed { get; private set; }
    // Bintang hasil level, 0 bila tidak lulus
    public int Stars { get; private set; }
    // Sedang ada animasi kereta: submit dan reset diabaikan
    public bool IsBusy { get; private set; }

    // Drop area milik gerbong yang sedang berhenti di tengah layar
    public DropAreaScript[] dropAreas => train.CurrentWagon != null ? train.CurrentWagon.DropAreas : new DropAreaScript[0];

    // Box untuk soal yang sedang tampil
    public List<BoxScript> SpawnedBoxes { get; } = new List<BoxScript>();
    // Semua box yang pernah dibuat (pool), dipakai ulang antar soal
    private readonly List<BoxScript> boxPool = new List<BoxScript>();

    private readonly List<LevelStep> steps = new List<LevelStep>();
    private int stepIndex;
    // Urutan bagian sesuai kemunculan di bank, beserta jumlah soal dan jawaban benar
    private readonly List<SentenceForm> sectionOrder = new List<SentenceForm>();
    private readonly Dictionary<SentenceForm, int> sectionTotal = new Dictionary<SentenceForm, int>();
    private readonly Dictionary<SentenceForm, int> sectionCorrect = new Dictionary<SentenceForm, int>();

    // Tutorial: peran tiap kolom dan kolom yang sedang diberi petunjuk
    private string[] tutorialRoles;
    private int shownGuideIndex = -2;

    LevelStep CurrentStep => steps[stepIndex];

    void OnEnable()
    {
        DropAreaScript.Rejected += OnDropRejected;
    }

    void OnDisable()
    {
        DropAreaScript.Rejected -= OnDropRejected;
    }

    void Start()
    {
        int level = LevelSelection.Level;
        if (levelBanks != null && level >= 1 && level <= levelBanks.Length && levelBanks[level - 1] != null)
            questionBank = levelBanks[level - 1];
        else
            Debug.LogWarning("BoxSpawner: Level " + level + " belum punya bank soal, memakai bank cadangan");
        StartLevel();
    }

    public void StartLevel()
    {
        if (questionBank == null || questionBank.questions.Count == 0)
        {
            Debug.LogError("BoxSpawner: Question Bank belum diisi atau kosong");
            return;
        }
        foreach (string warning in questionBank.Validate(separator)) Debug.LogWarning("BoxSpawner (" + questionBank.name + "): " + warning);

        BuildSteps();
        stepIndex = 0;
        CorrectCount = 0;
        IsFinished = false;
        IsPassed = false;
        StartCoroutine(BeginLevelRoutine());
    }

    // Soal disusun sesuai urutan bank. Saat bagian baru dimulai, tutorial bagian itu (bila ada) disisipkan di depannya
    void BuildSteps()
    {
        steps.Clear();
        sectionOrder.Clear();
        sectionTotal.Clear();
        sectionCorrect.Clear();
        TotalQuestions = 0;

        foreach (Question question in questionBank.questions)
        {
            if (!sectionOrder.Contains(question.section))
            {
                TutorialStep tutorial = questionBank.GetTutorial(question.section);
                if (tutorial != null)
                {
                    var example = new Question { id = "TUTORIAL-" + question.section, section = question.section, sentence = tutorial.exampleSentence };
                    steps.Add(new LevelStep { question = example, tutorial = tutorial });
                }
                sectionOrder.Add(question.section);
                sectionTotal[question.section] = 0;
                sectionCorrect[question.section] = 0;
            }
            steps.Add(new LevelStep { question = question });
            sectionTotal[question.section]++;
            TotalQuestions++;
        }
    }

    IEnumerator BeginLevelRoutine()
    {
        IsBusy = true;
        // Jumlah box dan label peran tiap gerbong dikirim ke kereta,
        // agar gerbong sudah sesuai sebelum terlihat
        var plans = new List<WagonPlan>();
        foreach (LevelStep step in steps)
        {
            plans.Add(new WagonPlan
            {
                boxCount = BoxCount(step.question),
                form = SentenceSection.FormOf(step.question, separator),
                roles = step.IsTutorial ? step.tutorial.GetRoles() : null
            });
        }
        train.Setup(plans);
        // Kereta sudah di luar layar sebelum intro memudar, jadi posisi kereta di editor tidak sempat terlihat
        if (levelIntro != null)
            yield return levelIntro.Play(questionBank.tenseName + " Tense", "Level " + LevelSelection.Level);
        if (guidePanel != null && (guideLevel == 0 || guideLevel == LevelSelection.Level))
        {
            guidePanel.Show();
            while (guidePanel.IsOpen) yield return null;
        }
        yield return train.Arrive();
        LoadStep();
        IsBusy = false;
    }

    void LoadStep()
    {
        LevelStep step = CurrentStep;
        Question question = step.question;
        CurrentQuestion = question;
        CorrectWords = question.GetWords(separator);
        AcceptedAnswers = question.GetAcceptedAnswers(separator);

        if (CorrectWords.Length > spawnAreas.Length)
            Debug.LogWarning("BoxSpawner: soal " + question.id + " punya " + CorrectWords.Length + " kata, tapi spawn area hanya " + spawnAreas.Length + ". Sebagian kata tidak di-spawn.");

        UpdateSectionLabel(question.section);
        LayoutSpawnAreas(BoxCount(question));
        PlaceBoxes(Shuffle(CorrectWords, AcceptedAnswers));

        if (step.IsTutorial) BeginTutorial(step.tutorial);
        else if (IsFirstOfSection() && questionBank.GetTutorial(question.section) == null && notification != null)
            notification.ShowInfo("<b>" + SentenceSection.GetLabel(question.section) + "</b>\n<size=70%><color=#FFFFFF>Campuran semua bentuk kalimat</color></size>");

        Debug.Log((step.IsTutorial ? "Tutorial " : "Soal ") + question.id + " : " + string.Join(" ", CorrectWords));
    }

    bool IsFirstOfSection()
    {
        return stepIndex == 0 || steps[stepIndex - 1].question.section != CurrentStep.question.section;
    }

    void UpdateSectionLabel(SentenceForm section)
    {
        if (sectionLabelText != null) sectionLabelText.text = SentenceSection.GetLabel(section);
        if (sectionLabelBackground != null) sectionLabelBackground.color = SentenceSection.GetColor(section);
    }

    int BoxCount(Question question)
    {
        return Mathf.Clamp(question.GetWords(separator).Length, 1, spawnAreas.Length);
    }

    // Tampilkan spawn area sebanyak jumlah box, tersusun simetris di tengah peron
    void LayoutSpawnAreas(int count)
    {
        float width = SlotLayout.Width(count);
        for (int i = 0; i < spawnAreas.Length; i++)
        {
            bool used = i < count;
            spawnAreas[i].gameObject.SetActive(used);
            if (!used) continue;

            Transform t = spawnAreas[i].transform;
            t.localPosition = new Vector3(SlotLayout.X(i, count), t.localPosition.y, t.localPosition.z);
            t.localScale = new Vector3(width, t.localScale.y, t.localScale.z);
        }
    }

    // Tempatkan box di spawn area sesuai urutan kata yang sudah diacak
    void PlaceBoxes(string[] shuffled)
    {
        int count = Mathf.Min(shuffled.Length, spawnAreas.Length);
        float width = SlotLayout.Width(count);

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
            box.SetHighlight(false);
            box.SetWidth(width);
            box.SetWord(shuffled[i]);
            box.SetHomeSpawnArea(spawnAreas[i]);
            box.ResetToHome();
        }
        // Petunjuk tutorial dihitung ulang karena posisi box berubah
        shownGuideIndex = -2;
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

    // ---------- Tutorial terpandu ----------

    void BeginTutorial(TutorialStep tutorial)
    {
        IsTutorialActive = true;
        tutorialRoles = tutorial.GetRoles();
        if (tutorialPanel != null) tutorialPanel.Show(tutorial, SentenceSection.GetColor(tutorial.section));

        // Kolom hanya menerima box yang benar, dan harus diisi berurutan dari kiri
        DropAreaScript[] areas = dropAreas;
        for (int i = 0; i < areas.Length; i++)
        {
            int index = i;
            areas[i].AcceptFilter = box => index == NextGuideIndex() && box.Word == CorrectWords[index];
        }
        shownGuideIndex = -2;
    }

    void Update()
    {
        if (!IsTutorialActive || IsBusy) return;

        int next = NextGuideIndex();
        if (next != shownGuideIndex) ShowGuide(next);
        if (next < 0) StartCoroutine(CompleteTutorialRoutine());
    }

    // Kolom paling kiri yang belum berisi box yang benar, -1 bila kalimat contoh sudah lengkap
    int NextGuideIndex()
    {
        DropAreaScript[] areas = dropAreas;
        for (int i = 0; i < areas.Length && i < CorrectWords.Length; i++)
        {
            if (areas[i].CurrentBox == null || areas[i].CurrentBox.Word != CorrectWords[i]) return i;
        }
        return -1;
    }

    // Nyalakan kolom tujuan dan box yang harus dipindah, lalu tulis petunjuk langkahnya
    void ShowGuide(int next)
    {
        shownGuideIndex = next;
        DropAreaScript[] areas = dropAreas;
        for (int i = 0; i < areas.Length; i++) areas[i].SetHighlight(i == next, guideColor);

        BoxScript target = null;
        if (next >= 0)
        {
            foreach (BoxScript box in SpawnedBoxes)
                if (target == null && !box.IsInDropArea && box.Word == CorrectWords[next]) target = box;
        }
        foreach (BoxScript box in SpawnedBoxes) box.SetHighlight(box == target);

        if (tutorialPanel != null && next >= 0)
            tutorialPanel.SetHint("Langkah " + (next + 1) + ": pindahkan box \"" + CorrectWords[next] + "\"" + RoleSuffix(next) + " ke kolom yang menyala", false);
    }

    // " (Subject)" untuk ditulis setelah kata box; kosong bila peran sama dengan katanya (mis. not)
    string RoleSuffix(int index)
    {
        if (tutorialRoles == null || index >= tutorialRoles.Length) return "";
        string role = tutorialRoles[index];
        return string.Equals(role, CorrectWords[index], System.StringComparison.OrdinalIgnoreCase) ? "" : " (" + role + ")";
    }

    void OnDropRejected(DropAreaScript area, BoxScript box)
    {
        if (!IsTutorialActive || IsBusy || tutorialPanel == null) return;
        int next = NextGuideIndex();
        if (next < 0) return;
        tutorialPanel.SetHint("Belum tepat! Kolom yang menyala butuh box \"" + CorrectWords[next] + "\"" + RoleSuffix(next), true);
    }

    IEnumerator CompleteTutorialRoutine()
    {
        IsBusy = true;
        IsTutorialActive = false;
        foreach (DropAreaScript area in dropAreas) area.SetHighlight(false, Color.clear);
        if (tutorialPanel != null) tutorialPanel.SetHint("Kalimat contoh lengkap!", false);
        yield return new WaitForSeconds(tutorialCompleteDelay);
        if (tutorialPanel != null) tutorialPanel.Hide();
        if (notification != null) notification.ShowCorrect("<b>Bagus!</b>\n<size=70%><color=#FFFFFF>Sekarang coba sendiri</color></size>", true);
        yield return AdvanceRoutine();
    }

    // ---------- Tombol ----------

    // Dipanggil tombol Reset: semua box ditarik kembali ke spawn area dengan urutan diacak ulang
    public void ResetBoxes()
    {
        if (IsBusy || IsFinished || CorrectWords == null) return;
        string[] shuffled = Shuffle(CorrectWords, AcceptedAnswers);
        PlaceBoxes(shuffled);
        Debug.Log("Box diacak ulang: " + string.Join(" | ", shuffled));
    }

    // Dipanggil tombol Submit: cek apakah urutan box di drop area sudah benar
    public bool SubmitAnswer()
    {
        if (IsBusy || IsFinished || CorrectWords == null) return false;

        if (IsTutorialActive)
        {
            // Tutorial selesai otomatis saat kalimat contoh lengkap, jadi Submit cukup mengingatkan petunjuk
            int next = NextGuideIndex();
            if (tutorialPanel != null && next >= 0)
                tutorialPanel.SetHint("Ikuti petunjuk dulu: letakkan box \"" + CorrectWords[next] + "\"" + RoleSuffix(next) + " di kolom yang menyala", true);
            return false;
        }

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

        bool correct = IsAccepted(answer.ToArray());
        if (correct)
        {
            CorrectCount++;
            sectionCorrect[CurrentQuestion.section]++;
        }
        bool advance = correct || !advanceOnlyWhenCorrect;
        if (notification != null)
        {
            // Saat pindah gerbong, notifikasi tetap tampil selama kereta bergerak lalu disembunyikan di AdvanceRoutine
            if (correct) notification.ShowCorrect("<b>Jawaban Benar!</b>", advance);
            else notification.ShowWrong("<b>Jawaban Salah!</b>\n<size=70%><color=#FFFFFF>Jawaban benar: " + string.Join(" ", CorrectWords) + "</color></size>", advance);
        }

        if (advance) StartCoroutine(AdvanceRoutine());
        return correct;
    }

    // Versi void untuk event onClick Button
    public void OnSubmitButton()
    {
        SubmitAnswer();
    }

    IEnumerator AdvanceRoutine()
    {
        IsBusy = true;

        // Box jawaban dimuat ke gerbong dan ikut berangkat bersama kereta
        train.CurrentWagon.LoadBoxes();
        SpawnedBoxes.Clear();

        stepIndex++;
        if (stepIndex < steps.Count)
        {
            yield return train.MoveToNext();
            if (notification != null) notification.Hide();
            LoadStep();
        }
        else
        {
            stepIndex = steps.Count - 1;
            yield return train.Depart();
            FinishLevel();
        }

        IsBusy = false;
    }

    // Lulus bila tiap bagian mencapai minimal jawaban benar bagian tersebut
    void FinishLevel()
    {
        IsFinished = true;
        IsPassed = true;
        var parts = new List<string>();
        var lines = new List<string>();
        foreach (SentenceForm section in sectionOrder)
        {
            int correct = sectionCorrect[section];
            int total = sectionTotal[section];
            int needed = Mathf.Min(total, section == SentenceForm.Review ? minCorrectReview : minCorrectPerSection);
            if (correct < needed) IsPassed = false;
            // Bagian yang belum mencapai batas lulus ditandai merah
            string color = correct >= needed ? "#FFFFFF" : "#FFB4A8";
            parts.Add("<color=" + color + ">" + SentenceSection.GetShortName(section) + " " + correct + "/" + total + "</color>");
            string line = SentenceSection.GetShortName(section) + " : " + correct + "/" + total;
            lines.Add(correct >= needed ? line : "<color=#C0392B>" + line + "</color>");
        }
        Stars = CountStars();

        Debug.Log("Level selesai: benar " + CorrectCount + "/" + TotalQuestions + " - " + (IsPassed ? "LULUS" : "TIDAK LULUS") + ", bintang " + Stars);
        if (notification != null) notification.Hide();
        if (resultPanel != null)
        {
            bool hasNextLevel = levelBanks != null && LevelSelection.Level < levelBanks.Length;
            resultPanel.Show(IsPassed, string.Join("\n", lines.ToArray()), Stars, hasNextLevel);
            return;
        }

        string result = "<b>Level Selesai! - " + (IsPassed ? "LULUS" : "TIDAK LULUS") + "</b>\n<size=60%>" + string.Join("   |   ", parts.ToArray()) + "</size>";
        if (notification != null)
        {
            // Tetap tampil sampai level dimulai ulang
            if (IsPassed) notification.ShowCorrect(result, true);
            else notification.ShowWrong(result, true);
        }
    }

    // Tidak lulus = 0, lulus = 1, lalu 2 / 3 bintang sesuai persentase jawaban benar dari semua soal
    int CountStars()
    {
        if (!IsPassed || TotalQuestions == 0) return 0;
        float ratio = (float)CorrectCount / TotalQuestions;
        // Toleransi kecil agar mis. 0.75 tidak gagal karena pembulatan float
        if (ratio >= threeStarRatio - 0.0001f) return 3;
        if (ratio >= twoStarRatio - 0.0001f) return 2;
        return 1;
    }

    // Jawaban benar bila sama dengan kalimat utama atau salah satu susunan alternatif
    bool IsAccepted(string[] answer)
    {
        foreach (string[] accepted in AcceptedAnswers)
            if (answer.Length == accepted.Length && SameOrder(answer, accepted)) return true;
        return false;
    }

    // Fisher-Yates; diulang bila hasilnya kebetulan sama dengan salah satu jawaban benar
    static string[] Shuffle(string[] source, List<string[]> accepted)
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
            if (!accepted.Exists(a => a.Length == result.Length && SameOrder(result, a))) break;
        }
        return result;
    }

    static bool SameOrder(string[] a, string[] b)
    {
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
