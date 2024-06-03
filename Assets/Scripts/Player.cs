using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using ExitGames.Client.Photon;
using Photon.Realtime;
using System.Linq;
using MyBox;
using System.Reflection;

[RequireComponent(typeof(PhotonView))]
public class Player : MonoBehaviour
{

#region Setup

    [ReadOnly] public PhotonView pv;
    Canvas canvas;

    [ReadOnly] public List<Card> listOfHand = new List<Card>();
    [SerializeField] Transform cardhand;

    public Dictionary<string, MethodInfo> dictionary = new();

    private void Awake()
    {
        pv = GetComponent<PhotonView>();
        canvas = GameObject.Find("Canvas").GetComponent<Canvas>();

        AddToDictionary(nameof(SendDiscard));
        AddToDictionary(nameof(RequestDraw));
        AddToDictionary(nameof(SendDraw));
    }

    void MultiFunction(MethodInfo function, RpcTarget affects, object[] parameters = null)
    {
        if (PhotonNetwork.IsConnected)
        {
            pv.RPC(function.Name, affects, parameters);
        }
        else
        {
            function.Invoke(this, parameters);
        }
    }

    IEnumerator MultiEnumerator(MethodInfo function, RpcTarget affects, object[] parameters = null)
    {
        if (PhotonNetwork.IsConnected)
        {
            pv.RPC(function.Name, affects, parameters);
        }
        else
        {
            yield return (IEnumerator)function.Invoke(this, parameters);
        }
    }

    void AddToDictionary(string methodName)
    {
        MethodInfo method = typeof(Player).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            dictionary.Add(methodName, method);
    }

    #endregion

#region Cards in Hand

    [PunRPC]
    void SendDiscard(int cardID)
    {
        Card discardMe = PhotonView.Find(cardID).GetComponent<Card>();
        listOfHand.Remove(discardMe);
        SortHand();

        discardMe.transform.SetParent(Manager.instance.discard);
        StartCoroutine(discardMe.MoveCard(new Vector2(-2000, -330), new Vector3(0, 0, 0), 0.3f));
    }

    [PunRPC]
    void RequestDraw(int cardsToDraw)
    {
        int[] cardIDs = new int[cardsToDraw];
        for (int i = 0; i < cardsToDraw; i++)
        {
            if (Manager.instance.deck.childCount == 0)
            {
                Manager.instance.discard.Shuffle();
                while (Manager.instance.discard.childCount > 0)
                    Manager.instance.discard.GetChild(0).SetParent(Manager.instance.deck);
            }

            PhotonView x = Manager.instance.deck.GetChild(i).GetComponent<PhotonView>();
            cardIDs[i] = x.ViewID;
        }

        MultiFunction(dictionary[nameof(SendDraw)], RpcTarget.All, new object[1] { cardIDs });
    }

    [PunRPC]
    void SendDraw(int[] cardIDs)
    {
        for (int i = 0; i < cardIDs.Length; i++)
        {
            Card newCard = PhotonView.Find(cardIDs[i]).GetComponent<Card>();
            newCard.transform.SetParent(this.cardhand);
            newCard.transform.localPosition = new Vector2(0, -1100);
            listOfHand.Add(newCard);

            if (this.pv.AmOwner)
            {
                Log.instance.AddText($"{this.name} draws {newCard.name}.");
                StartCoroutine(newCard.RevealCard(0.3f));
            }
            else
            {
                Log.instance.AddText($"{this.name} draws a card.");
            }

            newCard.image.sprite = (this.pv.AmOwner) ? newCard.originalSprite : newCard.faceDownSprite;
        }
        SortHand();
    }

    public void SortHand()
    {
        float firstCalc = Mathf.Round(canvas.transform.localScale.x * 4) / 4f;
        float multiplier = firstCalc / 0.25f;

        for (int i = 0; i < listOfHand.Count; i++)
        {
            Card nextCard = listOfHand[i];
            float startingX = (listOfHand.Count > 7) ? (-300 - (150 * multiplier)) : (listOfHand.Count - 1) * (-50 - 25 * multiplier);
            float difference = (listOfHand.Count > 7) ? (-300 - (150 * multiplier)) * -2 / (listOfHand.Count - 1) : 100 + (50 * multiplier);
            Vector2 newPosition = new(startingX + difference * i, -515 * canvas.transform.localScale.x);
            StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.3f));
        }

        foreach (Card card in listOfHand)
            StartCoroutine(card.RevealCard(0.3f));

        //pv.RPC("UpdateMyText", RpcTarget.All, listOfHand.Count);
    }

    #endregion

}
