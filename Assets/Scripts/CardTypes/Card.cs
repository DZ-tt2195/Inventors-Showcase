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

[RequireComponent(typeof(CardLayout))]
[RequireComponent(typeof(PhotonView))]
public class Card : MonoBehaviour
{

#region Variables

    [Foldout("Misc", true)]
        [ReadOnly] public PhotonView pv;
        [ReadOnly] public CardData dataFile;
        [ReadOnly] public Button button;

    [Foldout("Art", true)]
        [ReadOnly] public CanvasGroup cg;
        public Sprite faceDownSprite;
        [ReadOnly] public Sprite originalSprite;

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

        foreach (string nextSection in dataFile.commandInstructions)
        {
            if (nextSection.Equals("None") || nextSection.Equals("") || dictionary.ContainsKey(nextSection))
                continue;

            MethodInfo method = typeof(Card).GetMethod(nextSection, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null && method.ReturnType == typeof(IEnumerator))
                dictionary.Add(nextSection, method);
            else
                Debug.LogError($"{dataFile.cardName}: instructions: {nextSection} doesn't exist");
        }

        foreach (string nextSection in dataFile.replaceInstructions)
        {
            if (nextSection.Equals("None") || nextSection.Equals("") || dictionary.ContainsKey(nextSection))
                continue;

            MethodInfo method = typeof(Card).GetMethod(nextSection, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null && method.ReturnType == typeof(IEnumerator))
                dictionary.Add(nextSection, method);
            else
                Debug.LogError($"{dataFile.cardName}: instructions: {nextSection} doesn't exist");
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
                int playerTracker = player.playerPosition;
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
