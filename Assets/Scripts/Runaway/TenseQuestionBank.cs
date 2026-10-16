using System;
using System.Collections.Generic;
using UnityEngine;

// Urutan enum = urutan tombol jawaban di layar
public enum Tense
{
    SimplePresent,
    PresentContinuous,
    SimplePast,
    Future
}

public static class TenseNames
{
    public static string Get(Tense tense)
    {
        switch (tense)
        {
            case Tense.SimplePresent: return "Simple Present";
            case Tense.PresentContinuous: return "Present Continuous";
            case Tense.SimplePast: return "Simple Past";
            default: return "Future";
        }
    }
}

[Serializable]
public class TenseQuestion
{
    [Tooltip("ID unik soal, mis. RW-001")]
    public string id;

    [Tooltip("Kalimat soal yang ditebak tense-nya")]
    [TextArea(1, 3)]
    public string sentence;

    public Tense tense;
}

[CreateAssetMenu(fileName = "TenseQuestionBank", menuName = "Grammar Station/Tense Question Bank")]
public class TenseQuestionBank : ScriptableObject
{
    public List<TenseQuestion> questions = new List<TenseQuestion>();
}
