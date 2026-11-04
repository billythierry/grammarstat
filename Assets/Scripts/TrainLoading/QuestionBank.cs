using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Question
{
    [Tooltip("ID unik soal, mis. SP-001")]
    public string id;

    [Tooltip("Bagian soal: Positive, Negative, Interrogative, atau Review (campuran)")]
    public SentenceForm section;

    [Tooltip("Kalimat yang benar. Pisahkan tiap box dengan '-', satu box satu kata, mis. I-cook-breakfast-every-day")]
    public string sentence;

    [Tooltip("Susunan lain yang juga benar (opsional), dipisah '|'. Box-nya harus sama persis dengan kalimat utama, mis. We-play-after school-with friends")]
    public string alternativeSentences;

    [Tooltip("Terjemahan / arti kalimat (opsional)")]
    public string translation;

    public string[] GetWords(char separator = '-')
    {
        return SplitCards(sentence, separator);
    }

    // Kalimat utama + semua susunan alternatif yang juga dinilai benar
    public List<string[]> GetAcceptedAnswers(char separator = '-')
    {
        var answers = new List<string[]> { GetWords(separator) };
        if (string.IsNullOrEmpty(alternativeSentences)) return answers;
        foreach (string alternative in alternativeSentences.Split('|'))
        {
            string[] cards = SplitCards(alternative, separator);
            if (cards.Length > 0) answers.Add(cards);
        }
        return answers;
    }

    static string[] SplitCards(string text, char separator)
    {
        var cards = new List<string>();
        if (string.IsNullOrEmpty(text)) return cards.ToArray();
        foreach (string part in text.Split(separator))
        {
            string card = part.Trim();
            if (card.Length > 0) cards.Add(card);
        }
        return cards.ToArray();
    }
}

// Tutorial terpandu yang tampil sebelum soal pertama sebuah bagian
[Serializable]
public class TutorialStep
{
    [Tooltip("Bagian yang dijelaskan tutorial ini")]
    public SentenceForm section;

    [Tooltip("Rumus kalimat, tiap bagian dipisah '|', mis. Subject|Verb 1 (+s/es)|Object")]
    public string formula;

    [Tooltip("Penjelasan singkat aturan kalimat")]
    [TextArea(2, 4)]
    public string explanation;

    [Tooltip("Kalimat contoh yang disusun pemain dengan petunjuk, dipisah '-', mis. She-reads-books")]
    public string exampleSentence;

    [Tooltip("Peran tiap box kalimat contoh, dipisah '|', jumlahnya sama dengan box, mis. Subject|Verb + s|Object")]
    public string exampleRoles;

    public string[] GetFormulaParts() { return SplitParts(formula); }
    public string[] GetRoles() { return SplitParts(exampleRoles); }

    static string[] SplitParts(string text)
    {
        var parts = new List<string>();
        if (string.IsNullOrEmpty(text)) return parts.ToArray();
        foreach (string part in text.Split('|'))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0) parts.Add(trimmed);
        }
        return parts.ToArray();
    }
}

[CreateAssetMenu(fileName = "QuestionBank", menuName = "Grammar Station/Question Bank")]
public class QuestionBank : ScriptableObject
{
    [Tooltip("Nama tense untuk bank soal ini, mis. Simple Present")]
    public string tenseName;

    [Tooltip("Soal dimainkan sesuai urutan daftar ini (tidak diacak). Tutorial bagian muncul sebelum soal pertama bagian tersebut")]
    public List<Question> questions = new List<Question>();

    [Tooltip("Tutorial terpandu per bagian. Bagian tanpa tutorial (mis. Review) langsung ke soal")]
    public List<TutorialStep> tutorials = new List<TutorialStep>();

    public TutorialStep GetTutorial(SentenceForm section)
    {
        return tutorials.Find(t => t.section == section);
    }

    // Cek isi bank: jumlah box per soal, ID dobel, dan box pada susunan alternatif
    public List<string> Validate(char separator = '-')
    {
        var warnings = new List<string>();
        var ids = new HashSet<string>();
        foreach (Question q in questions)
        {
            string label = string.IsNullOrEmpty(q.id) ? q.sentence : q.id;
            string[] cards = q.GetWords(separator);
            if (cards.Length == 0) warnings.Add(label + ": kalimat kosong");
            if (cards.Length > SlotLayout.MaxSlots) warnings.Add(label + ": " + cards.Length + " box, maksimal " + SlotLayout.MaxSlots);
            if (!ids.Add(q.id ?? "")) warnings.Add(label + ": id dobel");
            foreach (string card in cards)
                if (card.Contains(" ")) warnings.Add(label + ": box \"" + card + "\" berisi lebih dari satu kata (aturan: satu box satu kata)");

            List<string[]> answers = q.GetAcceptedAnswers(separator);
            for (int i = 1; i < answers.Count; i++)
            {
                if (!SameCards(cards, answers[i]))
                    warnings.Add(label + ": susunan alternatif ke-" + i + " memakai box yang berbeda dari kalimat utama");
            }
        }

        foreach (TutorialStep tutorial in tutorials)
        {
            string label = "Tutorial " + tutorial.section;
            int words = new Question { sentence = tutorial.exampleSentence }.GetWords(separator).Length;
            if (words == 0) warnings.Add(label + ": kalimat contoh kosong");
            if (words > SlotLayout.MaxSlots) warnings.Add(label + ": " + words + " box, maksimal " + SlotLayout.MaxSlots);
            if (tutorial.GetRoles().Length != words) warnings.Add(label + ": jumlah peran (" + tutorial.GetRoles().Length + ") tidak sama dengan jumlah box (" + words + ")");
        }
        return warnings;
    }

    static bool SameCards(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        var sortedA = (string[])a.Clone();
        var sortedB = (string[])b.Clone();
        Array.Sort(sortedA, StringComparer.Ordinal);
        Array.Sort(sortedB, StringComparer.Ordinal);
        for (int i = 0; i < sortedA.Length; i++)
            if (sortedA[i] != sortedB[i]) return false;
        return true;
    }
}
