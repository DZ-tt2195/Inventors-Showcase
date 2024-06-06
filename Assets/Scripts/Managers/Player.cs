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

#region Variables

    [ReadOnly] public PhotonView pv;
    Canvas canvas;

    [ReadOnly] public List<Card> listOfHand = new List<Card>();
    [SerializeField] Transform cardhand;
    [ReadOnly] public List<Card> listOfPlay = new List<Card>();
    [SerializeField] Transform cardplay;

    public Dictionary<string, MethodInfo> dictionary = new();
    bool myTurn;
    [ReadOnly] public int playerPosiiton;

    #endregion

#region Setup

    private void Awake()
    {
        pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && pv.AmOwner)
            pv.Owner.NickName = PlayerPrefs.GetString("Online Username");

        canvas = GameObject.Find("Canvas").GetComponent<Canvas>();

        AddToDictionary(nameof(SendDiscard));
        AddToDictionary(nameof(RequestDraw));
        AddToDictionary(nameof(TakeTurn));
        AddToDictionary(nameof(EndTurn));
    }

    private void Start()
    {
        if (PhotonNetwork.IsConnected)
            this.name = pv.Owner.NickName;
    }

    void MultiFunction(MethodInfo function, RpcTarget affects, object[] parameters = null)
    {
        if (PhotonNetwork.IsConnected)
            pv.RPC(function.Name, affects, parameters);
        else
            function.Invoke(this, parameters);
    }

    IEnumerator MultiEnumerator(MethodInfo function, RpcTarget affects, object[] parameters = null)
    {
        if (PhotonNetwork.IsConnected)
            pv.RPC(function.Name, affects, parameters);
        else
            yield return (IEnumerator)function.Invoke(this, parameters);
    }

    void AddToDictionary(string methodName)
    {
        MethodInfo method = typeof(Player).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            dictionary.Add(methodName, method);
    }

    internal void AssignInfo(int position)
    {
        this.playerPosiiton = position;
        this.transform.localPosition = new Vector3(-280 + 2500 * this.playerPosiiton, 0, 0);
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
        Card[] listOfDraw = new Card[cardsToDraw];

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
            Card y = Manager.instance.deck.GetChild(i).GetComponent<Card>();
            listOfDraw[i] = y;
        }

        if (PhotonNetwork.IsConnected)
            pv.RPC(nameof(SendDraw), RpcTarget.All, cardIDs);
        else
            AddToHand(listOfDraw);
    }

    [PunRPC]
    void SendDraw(int[] cardIDs)
    {
        Card[] listOfCards = new Card[cardIDs.Length];
        for (int i = 0; i < cardIDs.Length; i++)
            listOfCards[i] = PhotonView.Find(cardIDs[i]).gameObject.GetComponent<Card>();

        AddToHand(listOfCards);
    }

    void AddToHand(Card[] listOfCards)
    {
        for (int i = 0; i < listOfCards.Length; i++)
        {
            Card newCard = listOfCards[i];
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
            float startingX = (listOfHand.Count > 7) ? (-250 - (150 * multiplier)) : (listOfHand.Count - 1) * (-50 - 25 * multiplier);
            float difference = (listOfHand.Count > 7) ? (-250 - (150 * multiplier)) * -2 / (listOfHand.Count - 1) : 100 + (50 * multiplier);
            Vector2 newPosition = new(startingX + difference * i, -535 * canvas.transform.localScale.x);
            StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.3f));
        }

        foreach (Card card in listOfHand)
            StartCoroutine(card.RevealCard(0.3f));

        //pv.RPC("UpdateMyText", RpcTarget.All, listOfHand.Count);
    }

    #endregion

#region Cards in Play

    public void SortPlay()
    {
        float firstCalc = Mathf.Round(canvas.transform.localScale.x * 4) / 4f;
        float multiplier = firstCalc / 0.25f;

        for (int i = 0; i<6; i++)
        {
            try
            {
                Card nextCard = listOfPlay[i];
                float xPosition = -750 + (300 * multiplier * i);
                Vector2 newPosition = new(xPosition, 300 * canvas.transform.localScale.x);
                StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.3f));
            }
            catch
            {
                break;
            }
        }
        for (int i = 0; i < 6; i++)
        {
            try
            {
                Card nextCard = listOfPlay[i+6];
                float xPosition = -750 + (300 * multiplier * i);
                Vector2 newPosition = new(xPosition, -90 * canvas.transform.localScale.x);
                StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.3f));
            }
            catch
            {
                break;
            }
        }

        foreach (Card card in listOfPlay)
            StartCoroutine(card.RevealCard(0.3f));
    }

    #endregion

#region Turn

    public IEnumerator TakeTurnRPC()
    {
        StartCoroutine(MultiEnumerator(dictionary[nameof(TakeTurn)], RpcTarget.All));
        myTurn = true;
        while (myTurn)
            yield return null;
    }

    [PunRPC]
    IEnumerator TakeTurn()
    {
        yield return null;
    }

    [PunRPC]
    void EndTurn()
    {
        myTurn = false;
    }

#endregion

}
