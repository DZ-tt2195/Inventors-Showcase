using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyBox;
using System.Reflection;
using System.Linq;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using System;

[RequireComponent(typeof(PhotonView))]
public class Card : MonoBehaviour
{

#region Variables

    public Dictionary<string, MethodInfo> dictionary = new();

    [ReadOnly] public PhotonView pv;
    [ReadOnly] public Image image;
    [ReadOnly] public CardData dataFile;
    [ReadOnly] public Button button;
    protected CanvasGroup cg;

    protected TMP_Text titleText;
    protected TMP_Text descriptionText;
    protected TMP_Text artText;
    protected TMP_Text coinText;
    protected TMP_Text crownText;

    public Sprite faceDownSprite;
    [ReadOnly] public Sprite originalSprite;

    protected bool runNextMethod;
    protected bool runningMethod;

    #endregion

#region Setup

    private void Awake()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        pv = GetComponent<PhotonView>();
        cg = transform.Find("Canvas Group").GetComponent<CanvasGroup>();
    }

    protected void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(dictionary[methodName].Name, affects, parameters);
        else
            dictionary[methodName].Invoke(this, parameters);
    }

    protected IEnumerator MultiEnumerator(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(dictionary[methodName].Name, affects, parameters);
        else
            yield return (IEnumerator)dictionary[methodName].Invoke(this, parameters);
    }

    void AddToDictionary(string methodName)
    {
        MethodInfo method = typeof(Card).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            dictionary.Add(methodName, method);
    }

    [PunRPC]
    public virtual void GetDataFile(int fileSlot)
    {
        titleText.text = dataFile.cardName;
        descriptionText.text = dataFile.textBox;
        artText.text = dataFile.artCredit;

        if (dataFile.coinCost < 0)
            coinText.gameObject.SetActive(false);
        else
            coinText.text = $"{dataFile.coinCost} Coin";

        if (dataFile.scoringCrowns < 0)
            crownText.gameObject.SetActive(false);
        else
            crownText.text = $"{dataFile.scoringCrowns} Pos Crown";

        titleText.text = KeywordTooltip.instance.EditText(titleText.text);
        descriptionText.text = KeywordTooltip.instance.EditText(descriptionText.text);
        coinText.text = KeywordTooltip.instance.EditText(coinText.text);
        crownText.text = KeywordTooltip.instance.EditText(crownText.text);

        foreach (string nextSection in dataFile.commandInstructions)
        {
            string[] nextSplit = DownloadSheets.instance.SpliceString(nextSection.Trim(), '/');
            foreach (string small in nextSplit)
            {
                if (small.Equals("None") || small.Equals("") || dictionary.ContainsKey(small))
                    continue;

                MethodInfo method = typeof(Card).GetMethod(small, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && method.ReturnType == typeof(IEnumerator))
                    dictionary.Add(small, method);
                else
                    Debug.LogError($"{dataFile.cardName}: instructions: {small} doesn't exist");
            }
        }

        foreach (string nextSection in dataFile.replaceInstructions)
        {
            string[] nextSplit = DownloadSheets.instance.SpliceString(nextSection.Trim(), '/');
            foreach (string small in nextSplit)
            {
                if (small.Equals("None") || small.Equals("") || dictionary.ContainsKey(small))
                    continue;

                MethodInfo method = typeof(Card).GetMethod(small, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && method.ReturnType == typeof(IEnumerator))
                    dictionary.Add(small, method);
                else
                    Debug.LogError($"{dataFile.cardName}: instructions: {small} doesn't exist");
            }
        }
    }

#endregion

#region Animations

    public IEnumerator MoveCard(Vector2 newPos, Vector3 newRot, float waitTime)
    {
        float elapsedTime = 0;
        Vector2 originalPos = this.transform.localPosition;
        Vector3 originalRot = this.transform.localEulerAngles;

        while (elapsedTime < waitTime)
        {
            this.transform.localPosition = Vector2.Lerp(originalPos, newPos, elapsedTime / waitTime);
            this.transform.localEulerAngles = Vector3.Lerp(originalRot, newRot, elapsedTime / waitTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        this.transform.localPosition = newPos;
        this.transform.localEulerAngles = newRot;
    }

    public IEnumerator RevealCard(float totalTime)
    {
        if (image.sprite == faceDownSprite)
        {
            cg.alpha = 0;
            transform.localEulerAngles = new Vector3(0, 0, 0);
            float elapsedTime = 0f;

            Vector3 originalRot = this.transform.localEulerAngles;
            Vector3 newRot = new(0, 90, 0);

            while (elapsedTime < totalTime)
            {
                this.transform.localEulerAngles = Vector3.Lerp(originalRot, newRot, elapsedTime / totalTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            cg.alpha = 1;
            image.sprite = null;
            image.color = Color.black;
            elapsedTime = 0f;

            while (elapsedTime < totalTime)
            {
                this.transform.localEulerAngles = Vector3.Lerp(newRot, originalRot, elapsedTime / totalTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            this.transform.localEulerAngles = originalRot;
        }
    }



    #endregion

#region Instructions

    public IEnumerator CommandInstructions(Player player, int logged = -1)
    {
        runNextMethod = true;
        for (int i = 0; i < dataFile.commandInstructions.Count(); i++)
        {
            string methodName = dataFile.commandInstructions[i];

            if (methodName.Equals("None") || methodName.Equals(""))
                continue;

            runningMethod = true;
            StartCoroutine((IEnumerator)dictionary[methodName].Invoke(this, new object[2] { player, logged }));

            while (runningMethod)
                yield return null;
            if (!runNextMethod) break;
        }
    }

    public IEnumerator ReplaceInstructions(Player player, int logged)
    {
        runNextMethod = true;
        for (int i = 0; i < dataFile.replaceInstructions.Count(); i++)
        {
            string methodName = dataFile.replaceInstructions[i];

            if (methodName.Equals("None") || methodName.Equals(""))
                continue;

            if (dataFile.whoToTarget[i] == PlayerTarget.You)
            {
                runningMethod = true;
                StartCoroutine((IEnumerator)dictionary[methodName].Invoke(this, new object[2] { player, logged }));

                while (runningMethod)
                    yield return null;
                if (!runNextMethod) break;
            }
            else
            {
                int playerTracker = player.playerPosiiton;
                for (int j = 0; j<Manager.instance.playersInOrder.Count; j++)
                {
                    runningMethod = true;
                    Player nextPlayer = Manager.instance.playersInOrder[playerTracker];

                    if (dataFile.whoToTarget[i] == PlayerTarget.Others && player == nextPlayer)
                        continue;

                    StartCoroutine((IEnumerator)dictionary[methodName].Invoke(this, new object[2] { nextPlayer, logged }));
                    while (runningMethod)
                        yield return null;
                    playerTracker = (playerTracker == Manager.instance.playersInOrder.Count - 1) ? 0 : playerTracker + 1;
                }
            }
        }
    }

    IEnumerator DrawCards(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.RequestDraw), RpcTarget.MasterClient, new object[1] {dataFile.numDraw});
        runningMethod = false;
    }

    IEnumerator GainCoins(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.GainCoin), RpcTarget.All, new object[1] { dataFile.numGain });
        runningMethod = false;
    }

    IEnumerator LoseCoins(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.LoseCoin), RpcTarget.All, new object[1] { dataFile.numGain });
        runningMethod = false;
    }

    IEnumerator TakeNeg(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.TakeNegCrown), RpcTarget.All, new object[1] { dataFile.numCrowns });
        runningMethod = false;
    }

    IEnumerator RemoveNeg(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.RemoveNegCrown), RpcTarget.All, new object[1] { dataFile.numCrowns });
        runningMethod = false;
    }

    IEnumerator ReplaceCardOrMore(Player player, int logged)
    {
        yield return player.ChooseCardToPlay(player.listOfHand.Where(
            card => card.dataFile.coinCost <= player.coins && card.dataFile.coinCost >= card.dataFile.numPlayCost).
            ToList(), true);
        runningMethod = false;
    }

    IEnumerator ReplaceCardOrLess(Player player, int logged)
    {
        yield return player.ChooseCardToPlay(player.listOfHand.Where(
            card => card.dataFile.coinCost <= player.coins && card.dataFile.coinCost <= card.dataFile.numPlayCost).
            ToList(), true);
        runningMethod = false;
    }

    #endregion

}
