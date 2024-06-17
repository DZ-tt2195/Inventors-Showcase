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

    [Foldout("Misc", true)]
        [ReadOnly] public PhotonView pv;
        public CardData dataFile;
        [ReadOnly] public Button button;

    [Foldout("Art", true)]
        [ReadOnly] public CanvasGroup cg;
        public Sprite faceDownSprite;
        [ReadOnly] public Sprite originalSprite;
        public Image border;

    [Foldout("Methods", true)]
        public Dictionary<string, MethodInfo> dictionary = new();
        protected bool runNextMethod;
        protected bool runningMethod;

    #endregion

#region Setup

    private void Awake()
    {
        button = GetComponent<Button>();
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
        this.name = dataFile.cardName;
        this.gameObject.GetComponent<CardLayout>().FillInCards(this.dataFile, this.originalSprite);

        GetMethods(dataFile.commandInstructions);
        GetMethods(dataFile.replaceInstructions);
    }

    void GetMethods(string[] listOfInstructions)
    {
        foreach (string nextSection in listOfInstructions)
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
        if (cg.alpha == 0)
        {
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

    private void FixedUpdate()
    {
        this.border.SetAlpha(Manager.instance.opacity);
    }

    #endregion

#region Instructions

    public IEnumerator CommandInstructions(Player player, int logged = -1)
    {
        yield return ResolveInstructions(dataFile.commandInstructions, player, logged);
    }

    public IEnumerator ReplaceInstructions(Player player, int logged = -1)
    {
        yield return ResolveInstructions(dataFile.replaceInstructions, player, logged);
    }

    IEnumerator ResolveInstructions(string[] listOfInstructions, Player player, int logged = -1)
    {
        runNextMethod = true;
        for (int i = 0; i < listOfInstructions.Count(); i++)
        {
            string nextPart = listOfInstructions[i];
            string[] listOfSmallInstructions = DownloadSheets.instance.SpliceString(nextPart, '/');

            if (dataFile.whoToTarget[i] == PlayerTarget.You)
            {
                runningMethod = true;
                foreach (string methodName in listOfSmallInstructions)
                {
                    runningMethod = true;
                    StartCoroutine((IEnumerator)dictionary[methodName].Invoke(this, new object[2] { player, logged }));

                    while (runningMethod)
                        yield return null;
                    if (!runNextMethod) break;
                }
            }
            else
            {
                int playerTracker = player.playerPosition;
                for (int j = 0; j < Manager.instance.playersInOrder.Count; j++)
                {
                    Player nextPlayer = Manager.instance.playersInOrder[playerTracker];

                    if (dataFile.whoToTarget[i] == PlayerTarget.Others && player == nextPlayer)
                        continue;

                    foreach (string methodName in listOfSmallInstructions)
                    {
                        runningMethod = true;
                        StartCoroutine((IEnumerator)dictionary[methodName].Invoke(this, new object[2] { nextPlayer, logged }));
                        while (runningMethod)
                            yield return null;
                    }

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

    IEnumerator DiscardHand(Player player, int logged)
    {
        yield return null;
        foreach (Card card in player.listOfHand)
            player.DiscardRPC(card);
        runningMethod = false;
    }

    IEnumerator MandatoryDiscard(Player player, int logged)
    {
        for (int i = 0; i<dataFile.numDraw; i++)
        {
            Manager.instance.instructions.text = $"Discard a card ({dataFile.numDraw-i} more).";
            yield return player.ChooseCard(player.listOfHand, false);
            player.DiscardRPC(player.chosenCard);
        }
        runningMethod = false;
    }

    IEnumerator OptionalDiscard(Player player, int logged)
    {
        for (int i = 0; i < dataFile.numDraw; i++)
        {
            Manager.instance.instructions.text = $"Discard a card ({dataFile.numDraw - i} more)?";
            yield return player.ChooseCard(player.listOfHand, i == 0);

            if (player.chosenCard == null)
            {
                runNextMethod = false;
                break;
            }
            else
            {
                player.DiscardRPC(player.chosenCard);
            }
        }
        runningMethod = false;
    }

    IEnumerator MoneyOrLess(Player player, int logged)
    {
        yield return null;
        if (!(player.coins <= dataFile.numMisc))
            runNextMethod = false;
        runningMethod = false;
    }

    #endregion

}
