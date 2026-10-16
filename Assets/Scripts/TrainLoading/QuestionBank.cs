using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Question
{
    [Tooltip("ID unik soal, mis. SP-001")]
    public string id;

    [Tooltip("Kalimat yang benar. Pisahkan tiap box dengan '-', mis. I-cook-breakfast-every-day")]
    public string sentence;

    [Tooltip("Terjemahan / arti kalimat (opsional)")]
    public string translation;

    public string[] GetWords(char separator = '-')
    {
        var words = new List<string>();
        if (string.IsNullOrEmpty(sentence)) return words.ToArray();
        foreach (string part in sentence.Split(separator))
        {
            string w = part.Trim();
            if (w.Length > 0) words.Add(w);
        }
        return words.ToArray();
    }
}

[CreateAssetMenu(fileName = "QuestionBank", menuName = "Grammar Station/Question Bank")]
public class QuestionBank : ScriptableObject
{
    [Tooltip("Nama tense untuk bank soal ini, mis. Simple Present")]
    public string tenseName;

    public List<Question> questions = new List<Question>();
}
