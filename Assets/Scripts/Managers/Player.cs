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

    public int coins;
    public int negCrowns;

    public Dictionary<string, MethodInfo> dictionary = new();
    bool myTurn;
    [ReadOnly] public int playerPosiiton;

    protected int choice;
    protected Card chosenCard;
    [ReadOnly] public List<Card> cardsPlayed = new();

    #endregion

#region Setup

    private void Awake()
    {
        pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && pv.AmOwner)
            pv.Owner.NickName = PlayerPrefs.GetString("Online Username");

        canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
    }

    private void Start()
    {
        if (PhotonNetwork.IsConnected)
            this.name = pv.Owner.NickName;
    }

    void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(dictionary[methodName].Name, affects, parameters);
        else
            dictionary[methodName].Invoke(this, parameters);
    }

    IEnumerator MultiEnumerator(string methodName, RpcTarget affects, object[] parameters = null)
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

    public IEnumerator ChooseCardToPlay(bool replace)
    {
        List<Card> cardsToPlay = listOfHand.Where(card => card.dataFile.coinCost <= coins).ToList();
        yield return ChooseCard(cardsToPlay, true);

        if (chosenCard != null)
            MultiFunction(nameof(AddToPlay), RpcTarget.All, new object[1] { chosenCard.pv.ViewID });

        yield return new WaitForSeconds(1f);
    }

    [PunRPC]
    void AddToPlay(int cardID)
    {
        Card toPlay = PhotonView.Find(cardID).GetComponent<Card>();
        cardsPlayed.Add(toPlay);
        listOfHand.Remove(toPlay);
        listOfPlay.Add(toPlay);
        toPlay.transform.SetParent(cardplay);
        LoseCoin(toPlay.dataFile.coinCost);
        Log.instance.AddText($"{this.name} plays {toPlay.name}.");
        SortPlay();
    }

    #endregion

#region Resources

    [PunRPC]
    void GainCoin(int coins)
    {
        this.coins += coins;
    }

    [PunRPC]
    void LoseCoin(int coins)
    {
        this.coins = Mathf.Max(this.coins - coins, 0);
    }

    [PunRPC]
    void TakeNegCrown(int crowns)
    {
        this.negCrowns += crowns;
    }

    [PunRPC]
    void RemoveNegCrown(int crowns)
    {
        this.negCrowns = Mathf.Max(this.negCrowns - crowns, 0);
    }

#endregion

#region Turn

    public IEnumerator TakeTurnRPC()
    {
        StartCoroutine(MultiEnumerator(nameof(TakeTurn), RpcTarget.All));
        myTurn = true;
        while (myTurn)
            yield return null;
    }

    [PunRPC]
    IEnumerator TakeTurn()
    {
        if (this.pv.IsMine)
        {
            yield return ChooseAction();

            yield return ChooseCardToPlay(false);

            MultiFunction(nameof(EndTurn), RpcTarget.All);
        }
    }

    IEnumerator ChooseAction()
    {
        yield return null;
    }

    [PunRPC]
    void EndTurn()
    {
        myTurn = false;
        cardsPlayed.Clear();
    }

    IEnumerator ChooseCard(List<Card> possibleCards, bool optional)
    {
        choice = -1;
        chosenCard = null;

        for (int i = 0; i<possibleCards.Count; i++)
        {
            Card nextCard = possibleCards[i];
            int buttonNumber = i;

            nextCard.button.onClick.RemoveAllListeners();
            nextCard.button.interactable = true;
            nextCard.button.onClick.AddListener(() => ReceiveChoice(buttonNumber));
        }

        while (choice == -1)
        {
            yield return null;
        }

        chosenCard = possibleCards[choice];

        for (int i = 0; i<possibleCards.Count; i++)
        {
            Card nextCard = possibleCards[i];
            nextCard.button.onClick.RemoveAllListeners();
            nextCard.button.interactable = false;
        }
    }

    void ReceiveChoice(int number)
    {
        choice = number;
    }

#endregion

}
