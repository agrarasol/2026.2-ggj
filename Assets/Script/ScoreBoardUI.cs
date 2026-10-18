using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private GameObject root;            // objeto com tudo do placar
    [SerializeField] private TMP_Text finalTimeText;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text listText;
    [SerializeField] private GameObject continueHint;    // "Aperte ESPAÇO pra continuar"

    private bool submitted;

    private void Awake()
    {
        if (root != null) root.SetActive(false);

        if (submitButton != null)
            submitButton.onClick.AddListener(() => submitted = true);

        if (nameInput != null)
        {
            nameInput.characterLimit = ScoreBoard.MaxNameLength;
            nameInput.onSubmit.AddListener(_ => submitted = true);   // Enter também confirma
        }
    }

    public IEnumerator ShowRoutine()
    {
        float time = RunStats.Instance != null ? RunStats.Instance.TimeSurvived : 0f;
        int rank = -1;

        root.SetActive(true);
        continueHint.SetActive(false);
        finalTimeText.text = "VOCÊ SOBREVIVEU\n" + RunStats.Format(time);
        listText.text = "";

        bool qualifies = ScoreBoard.Qualifies(time);
        nameInput.gameObject.SetActive(qualifies);
        submitButton.gameObject.SetActive(qualifies);

        if (qualifies)
        {
            submitted = false;
            nameInput.text = "";

            yield return null;
            nameInput.Select();
            nameInput.ActivateInputField();

            while (!submitted)
                yield return null;

            rank = ScoreBoard.Add(nameInput.text, time);
            nameInput.gameObject.SetActive(false);
            submitButton.gameObject.SetActive(false);
        }

        listText.text = BuildList(rank);
        continueHint.SetActive(true);

        yield return null;
        while (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame)
            yield return null;

        root.SetActive(false);
    }

    private string BuildList(int highlightRank)
    {
        List<ScoreEntry> list = ScoreBoard.Load();
        if (list.Count == 0) return "";

        StringBuilder sb = new StringBuilder();
        sb.Append("<b>MELHORES TEMPOS</b>\n");

        for (int i = 0; i < list.Count; i++)
        {
            string line = (i + 1) + ".<pos=12%>" + list[i].name + "<pos=75%>" + RunStats.Format(list[i].time);

            if (i == highlightRank)
                line = "<color=#ffcc33>" + line + "</color>";   // destaca a sua posição

            sb.Append(line).Append('\n');
        }

        return sb.ToString();
    }

    [ContextMenu("DEBUG/Apagar placar")]
    private void ClearBoard()
    {
        ScoreBoard.Clear();
    }
}