using System.Collections.Generic;

[System.Serializable]
public class WrongQuestionItem
{
    public string questionText;
    public string[] options;
    public int chosenIndex;
    public int correctIndex;
    public string aiExplanation; 
}

public static class ReviewSessionData
{
    public static List<WrongQuestionItem> wrongQuestions = new List<WrongQuestionItem>();

    public static void Clear() => wrongQuestions.Clear();
    public static void AddWrong(string qText, string[] opts, int chosen, int correct)
    {
        wrongQuestions.Add(new WrongQuestionItem
        {
            questionText = qText,
            options = opts,
            chosenIndex = chosen,
            correctIndex = correct,
            aiExplanation = ""
        });
    }
}
