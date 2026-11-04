using UnityEngine;

// Bentuk kalimat / bagian level. Urutan soal di bank menentukan urutan bagian
public enum SentenceForm
{
    Positive,
    Negative,
    Interrogative,
    Review
}

// Warna dan nama tiap bagian, dipakai label bagian dan panel tutorial
public static class SentenceSection
{
    public static Color GetColor(SentenceForm form)
    {
        switch (form)
        {
            case SentenceForm.Positive: return new Color32(0x43, 0xA0, 0x47, 0xFF);
            case SentenceForm.Negative: return new Color32(0xE5, 0x39, 0x35, 0xFF);
            case SentenceForm.Interrogative: return new Color32(0x1E, 0x88, 0xE5, 0xFF);
            default: return new Color32(0xF9, 0xA8, 0x25, 0xFF);
        }
    }

    public static string GetLabel(SentenceForm form)
    {
        switch (form)
        {
            case SentenceForm.Positive: return "Positif (+)";
            case SentenceForm.Negative: return "Negatif (-)";
            case SentenceForm.Interrogative: return "Tanya (?)";
            default: return "Review";
        }
    }

    // Bentuk kalimat sebenarnya dari sebuah soal. Soal Review (campuran) ditebak dari kalimatnya:
    // diakhiri '?' = tanya, berisi kata "not" = negatif, selain itu positif
    public static SentenceForm FormOf(Question question, char separator = '-')
    {
        if (question.section != SentenceForm.Review) return question.section;
        string[] words = question.GetWords(separator);
        if (words.Length > 0 && words[words.Length - 1].EndsWith("?")) return SentenceForm.Interrogative;
        foreach (string word in words)
            if (string.Equals(word, "not", System.StringComparison.OrdinalIgnoreCase)) return SentenceForm.Negative;
        return SentenceForm.Positive;
    }

    public static string GetShortName(SentenceForm form)
    {
        switch (form)
        {
            case SentenceForm.Positive: return "Positif";
            case SentenceForm.Negative: return "Negatif";
            case SentenceForm.Interrogative: return "Tanya";
            default: return "Review";
        }
    }
}
