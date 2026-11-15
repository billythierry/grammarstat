using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public enum TicketAnswer
{
    A,
    B,
    NoError
}

[Serializable]
public class TicketData
{
    [Tooltip("ID unik tiket, mis. TI-001")]
    public string id;
    public string passengerName;
    public int age;

    [Tooltip("Materi tense soal ini, mis. Simple Present")]
    public string tense;

    [Tooltip("Kalimat soal. Tandai bagian yang diperiksa dengan [A:...] dan [B:...], mis. She [A:go] to school [B:every day].")]
    [TextArea(2, 4)]
    public string note;

    [Tooltip("Bagian yang salah, atau NoError bila kalimat sudah benar")]
    public TicketAnswer correctAnswer;

    [Tooltip("Kalimat yang benar, untuk feedback \"Jawaban yang benar adalah ...\"")]
    [TextArea(1, 3)]
    public string correction;

    static readonly Regex MarkerRegex = new Regex(@"\[(A|B):([^\]]+)\]");

    // Note dalam format TextMeshPro: bagian soal diberi warna dan garis bawah, diikuti label (A) / (B)
    public string GetFormattedNote(string highlightColorHex = "#1F4E9C")
    {
        if (string.IsNullOrEmpty(note)) return string.Empty;
        return MarkerRegex.Replace(note, m =>
            "<color=" + highlightColorHex + "><u>" + m.Groups[2].Value + "</u> <b>(" + m.Groups[1].Value + ")</b></color>");
    }

    // Note tanpa penanda, mis. untuk log
    public string GetPlainNote()
    {
        return StripMarkers(note);
    }

    // Correction dengan bagian yang diperbaiki (A atau B) diberi garis bawah.
    // Bagian itu dicari dari teks sebelum dan sesudah penanda di note, yang sama persis di correction.
    // Bila tidak cocok atau jawabannya No Error, correction dikembalikan tanpa garis bawah
    public string GetFormattedCorrection()
    {
        if (string.IsNullOrEmpty(correction) || string.IsNullOrEmpty(note) || correctAnswer == TicketAnswer.NoError)
            return correction ?? string.Empty;

        string key = correctAnswer.ToString();
        foreach (Match m in MarkerRegex.Matches(note))
        {
            if (m.Groups[1].Value != key) continue;
            string before = StripMarkers(note.Substring(0, m.Index));
            string after = StripMarkers(note.Substring(m.Index + m.Length));
            if (before.Length + after.Length >= correction.Length
                || !correction.StartsWith(before) || !correction.EndsWith(after))
                break;

            string fixedPart = correction.Substring(before.Length, correction.Length - before.Length - after.Length);
            return before + "<u>" + fixedPart + "</u>" + after;
        }
        return correction;
    }

    static string StripMarkers(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return MarkerRegex.Replace(text, m => m.Groups[2].Value);
    }
}

[CreateAssetMenu(fileName = "TicketBank", menuName = "Grammar Station/Ticket Bank")]
public class TicketBank : ScriptableObject
{
    public List<TicketData> tickets = new List<TicketData>();
}
