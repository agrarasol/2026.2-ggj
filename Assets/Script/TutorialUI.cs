using TMPro;
using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    public static TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    public static void ShowMensage(string mensage)
    {
        text.text = mensage;
    }
}
