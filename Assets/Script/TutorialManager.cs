using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    private const string START_TEXT = "Clique com o mouse para se mover";
    private const string CHASE_TEXT = "Cuidado com o perseguidor...";
    private const string CANDLE_TEXT = "Vá para a vela apagada e acenda clicando nele, senão...";
    private const string HIDEN_TEXT = "Evite aquele que se esconde pelos móveis...";

    public static bool startingTutorial;        // Se moveu a primeira vez? Mostra mensagem
    public static bool chaseEnemyTutorial;      // Primera vez que apareceu? Mostra mensagem
    public static bool candleEnemyTutorial;     // Primeira vez que apagou? Mostra mensagem
    public static bool hidenEnemyTutorial;      // Primeira vez que se escondeu? Mostra mensagem

    void Awake()
    {
        startingTutorial = false;
        chaseEnemyTutorial = false;
        candleEnemyTutorial = false;
        hidenEnemyTutorial = false;   
    }

    public static void CallStartTutorial()
    {
        if (startingTutorial)
        {
            return;
        }

        Debug.Log("Tutorial: Mensagem inicial");
        TutorialUI.instance.ShowMensage(START_TEXT);
        startingTutorial = true;
    }

    public static void CallChaseTutorial()
    {
        if (chaseEnemyTutorial)
        {
            return;
        }

        Debug.Log("Tutorial: Inimigo perseguidor");
        TutorialUI.instance.ShowMensage(CHASE_TEXT);
        chaseEnemyTutorial = true;
    }


    public static void CallCandleTutorial()
    {
        if (candleEnemyTutorial)
        {
            return;
        }

        Debug.Log("Tutorial: Inimigo vela");
        TutorialUI.instance.ShowMensage(CANDLE_TEXT);
        candleEnemyTutorial = true;
    }

    public static void CallHidenTutorial()
    {
        if (hidenEnemyTutorial)
        {
            return;
        }

        Debug.Log("Tutorial: Inimigo escondido");
        TutorialUI.instance.ShowMensage(HIDEN_TEXT);
        hidenEnemyTutorial = true;
    }
}
