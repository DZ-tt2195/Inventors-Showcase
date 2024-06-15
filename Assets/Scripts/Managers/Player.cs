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

    [Foldout("Misc", true)]
        [ReadOnly] public PhotonView pv;
        Canvas canvas;
        [ReadOnly] public int coins;
        [ReadOnly] public int negCrowns;
        bool myTurn;
        [ReadOnly] public int playerPosition;
        [SerializeField] Card dudPrefab;

    [Foldout("Lists", true)]
        [ReadOnly] public List<Card> listOfHand = new List<Card>();
        [SerializeField] Transform cardhand;
        [ReadOnly] public List<Card> listOfPlay = new List<Card>();
        [SerializeField] Transform cardplay;
        public Dictionary<string, MethodInfo> dictionary = new();
        [ReadOnly] public List<Card> cardsPlayed = new();

    [Foldout("Choices", true)]
        [ReadOnly] public int choice;
        [ReadOnly] public Card chosenCard;

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

    public void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(dictionary[methodName].Name, affects, parameters);
        else
            dictionary[methodName].Invoke(this, parameters);
    }

    public IEnumerator MultiEnumerator(string methodName, RpcTarget affects, object[] parameters = null)
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
        this.playerPosition = position;
        this.transform.SetParent(GameObject.Find("Store Players").transform);
        this.transform.localPosition = new Vector3(-280 + 2500 * this.playerPosition, 0, 0);

        Invoke(nameof(CreateDudRPC), 0.5f);
        Invoke(nameof(CreateDudRPC), 0.5f);
    }

    #endregion

#region Cards in Hand

    public void DiscardRPC(Card card)
    {
        if (PhotonNetwork.IsConnected)
            pv.RPC(nameof(SendDiscard), RpcTarget.All, card.pv.ViewID);
        else
            SendDiscard(card);
    }

    [PunRPC]
    void SendDiscard(int cardID)
    {
        Card discardMe = PhotonView.Find(cardID).GetComponent<Card>();
        SendDiscard(discardMe);
    }

    void SendDiscard(Card discardMe)
    {
        listOfPlay.Remove(discardMe);
        listOfHand.Remove(discardMe);
        SortHand();
        SortPlay();

        discardMe.transform.SetParent(Manager.instance.discard);
        StartCoroutine(discardMe.MoveCard(new Vector2(-2000, -330), new Vector3(0, 0, 0), 0.3f));
    }

    [PunRPC]
    public void RequestDraw(int cardsToDraw)
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
            newCard.cg.alpha = 0;
            listOfHand.Add(newCard);

            if (!PhotonNetwork.IsConnected || this.pv.AmOwner)
            {
                Log.instance.AddText($"{this.name} draws {newCard.name}.");
                StartCoroutine(newCard.RevealCard(0.3f));
            }
            else
            {
                Log.instance.AddText($"{this.name} draws a card.");
            }
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
        Debug.Log(multiplier);

        for (int i = 0; i<6; i++)
        {
            try
            {
                Card nextCard = listOfPlay[i];
                Vector2 newPosition = new(-750 + (75 * multiplier * i), 300 * canvas.transform.localScale.x);
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
                Vector2 newPosition = new(-750 + (300 * multiplier * i), -90 * canvas.transform.localScale.x);
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

    public IEnumerator ChooseCardToPlay(List<Card> cardsToPlay, bool replace)
    {
        //List<Card> cardsToPlay = listOfHand.Where(card => card.dataFile.coinCost <= coins).ToList();
        yield return ChooseCard(cardsToPlay, true);

        if (chosenCard != null)
        {
            if (replace)
            {

            }

            MultiFunction(nameof(AddToPlay), RpcTarget.All, new object[1] { chosenCard.pv.ViewID });
            yield return new WaitForSeconds(0.25f);
            yield return chosenCard.CommandInstructions(this);
        }
    }

    void AddToPlay(Card card)
    {
        card.cg.alpha = 1;

        cardsPlayed.Add(card);
        listOfHand.Remove(card);
        listOfPlay.Add(card);
        card.transform.SetParent(cardplay);

        LoseCoin(card.dataFile.coinCost);
        Log.instance.AddText($"{this.name} plays {card.name}.");
        SortPlay();
    }

    [PunRPC]
    void AddToPlay(int cardID)
    {
        AddToPlay(PhotonView.Find(cardID).GetComponent<Card>());
    }

    [PunRPC]
    public void CreateDudRPC()
    {
        if (PhotonNetwork.IsConnected)
        {
            GameObject newDud = PhotonNetwork.Instantiate(dudPrefab.name, Vector3.zero, new Quaternion());
            pv.RPC(nameof(AddToPlay), RpcTarget.All, newDud.GetComponent<PhotonView>().ViewID);

        }
        else
        {
            Card newDud = Instantiate(dudPrefab);
            AddToPlay(newDud);
        }
    }

    #endregion

#region Resources

    [PunRPC]
    public void GainCoin(int coins)
    {
        this.coins += coins;
    }

    [PunRPC]
    public void LoseCoin(int coins)
    {
        this.coins = Mathf.Max(this.coins - coins, 0);
    }

    [PunRPC]
    public void TakeNegCrown(int crowns)
    {
        this.negCrowns += crowns;
    }

    [PunRPC]
    public void RemoveNegCrown(int crowns)
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
        if (!PhotonNetwork.IsConnected || this.pv.IsMine)
        {
            yield return ChooseAction();

            int currentCards = cardsPlayed.Count;
            Manager.instance.instructions.text = "Play a new card (or add a Dud).";
            yield return ChooseCardToPlay(listOfHand.Where(card => card.dataFile.coinCost <= coins).ToList(), false);

            if (currentCards == cardsPlayed.Count)
                CreateDudRPC();

            MultiFunction(nameof(EndTurn), RpcTarget.All);
        }
    }

    IEnumerator ChooseAction()
    {
        Popup popup = Instantiate(CarryVariables.instance.cardPopup);
        popup.transform.SetParent(this.transform);
        popup.StatsSetup("Choose an action.", Vector3.zero);
        Manager.instance.instructions.text = "Choose an action.";

        foreach (Action action in Manager.instance.listOfActions)
            popup.AddCardButton(action, 1);
        yield return popup.WaitForChoice();

        Card actionToUse = popup.chosenCard;
        Destroy(popup.gameObject);
        yield return actionToUse.CommandInstructions(this);
    }

    [PunRPC]
    void EndTurn()
    {
        myTurn = false;
        cardsPlayed.Clear();
    }

    public IEnumerator ChooseCard(List<Card> possibleCards, bool optional)
    {
        choice = -1;
        chosenCard = null;

        if (possibleCards.Count > 0)
        {
            for (int i = 0; i < possibleCards.Count; i++)
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

            for (int i = 0; i < possibleCards.Count; i++)
            {
                Card nextCard = possibleCards[i];
                nextCard.button.onClick.RemoveAllListeners();
                nextCard.button.interactable = false;
            }
        }
    }

    void ReceiveChoice(int number)
    {
        choice = number;
    }

#endregion

}
