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

    [Foldout("Prefabs", true)]
        [SerializeField] Card junkPrefab;
        [SerializeField] Button playerButtonPrefab;

    [Foldout("Misc", true)]
        [ReadOnly] public PhotonView pv;
        Canvas canvas;
        [ReadOnly] public int coins;
        [ReadOnly] public int negCrowns;
        bool myTurn;
        [ReadOnly] public int playerPosition;

    [Foldout("UI", true)]
        [ReadOnly] public List<Card> listOfHand = new List<Card>();
        [SerializeField] Transform cardhand;
        [ReadOnly] public List<Card> listOfPlay = new List<Card>();
        [SerializeField] Transform cardplay;
        [ReadOnly] public List<Card> cardsPlayed = new();
        TMP_Text buttonText;
        Transform storePlayers;

    [Foldout("Choices", true)]
        public Dictionary<string, MethodInfo> dictionary = new();
        [ReadOnly] public int choice;
        [ReadOnly] public Card chosenCard;
        [ReadOnly] public Card lastUsedAction;
        [ReadOnly] public int ignoreInstructions = 0;

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
        storePlayers = GameObject.Find("Store Players").transform;
        this.transform.SetParent(storePlayers);
        this.transform.localPosition = new Vector3(2500 * this.playerPosition, 0, 0);

        Button newButton = Instantiate(playerButtonPrefab, Vector3.zero, new Quaternion());
        newButton.transform.SetParent(this.transform.parent.parent);
        newButton.transform.localPosition = new(-1100, 400 - (200 * playerPosition));
        buttonText = newButton.transform.GetChild(0).GetComponent<TMP_Text>();
        newButton.onClick.AddListener(MoveScreen);

        if (!PhotonNetwork.IsConnected || pv.IsMine)
        {
            CreateJunkRPC(true, -1);
            CreateJunkRPC(true, -1);
            MoveScreen();
        }
    }

    [PunRPC]
    void UpdateButton()
    {
        if (buttonText != null)
        {
            buttonText.text = $"{this.name}\n{listOfHand.Count} Card, {coins} Coin, -{negCrowns} Neg Crown";
            buttonText.text = KeywordTooltip.instance.EditText(buttonText.text);
        }
    }

    void MoveScreen()
    {
        storePlayers.localPosition = new Vector3(-2500 * this.playerPosition, 0, 0);
    }

    #endregion

#region Cards in Hand

    public void DiscardRPC(Card card, int logged)
    {
        if (PhotonNetwork.IsConnected)
            pv.RPC(nameof(SendDiscard), RpcTarget.All, card.pv.ViewID);
        else
            SendDiscard(card, logged);
    }

    [PunRPC]
    void SendDiscard(int cardID, int logged)
    {
        Card discardMe = PhotonView.Find(cardID).GetComponent<Card>();
        SendDiscard(discardMe, logged);
    }

    void SendDiscard(Card discardMe, int logged)
    {
        listOfPlay.Remove(discardMe);
        listOfHand.Remove(discardMe);

        discardMe.transform.SetParent(Manager.instance.discard);
        StartCoroutine(discardMe.MoveCard(new Vector2(-2000, -330), new Vector3(0, 0, 0), 0.3f));
        Log.instance.AddText($"{this.name} discards {discardMe.name}.", logged);

        SortHand();
        SortPlay();

        if (Manager.instance.ActiveEvent("Recycling"))
            GainCoin(1, logged);

        if (PhotonNetwork.IsConnected && discardMe.pv.AmOwner)
            PhotonNetwork.Destroy(discardMe.pv);
    }

    [PunRPC]
    public void RequestDraw(int cardsToDraw, int logged)
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
            pv.RPC(nameof(SendDraw), RpcTarget.All, cardIDs, logged);
        else
            AddToHand(listOfDraw, logged);
    }

    [PunRPC]
    void SendDraw(int[] cardIDs, int logged)
    {
        Card[] listOfCards = new Card[cardIDs.Length];
        for (int i = 0; i < cardIDs.Length; i++)
            listOfCards[i] = PhotonView.Find(cardIDs[i]).gameObject.GetComponent<Card>();

        AddToHand(listOfCards, logged);
    }

    void AddToHand(Card[] listOfCards, int logged)
    {
        string cardList = "";
        for (int i = 0; i < listOfCards.Length; i++)
        {
            Card newCard = listOfCards[i];
            newCard.transform.SetParent(this.cardhand);
            newCard.transform.localPosition = new Vector2(0, -1100);
            newCard.cg.alpha = 0;
            listOfHand.Add(newCard);

            if (!PhotonNetwork.IsConnected || this.pv.AmOwner)
                StartCoroutine(newCard.RevealCard(0.3f));

            cardList += $"{newCard.name}{(i < listOfCards.Length - 1 ? ", " : ".")}";
        }
        SortHand();

        if (!PhotonNetwork.IsConnected || this.pv.AmOwner)
            Log.instance.AddText($"{this.name} draws {cardList}", logged);
        else
            Log.instance.AddText($"{this.name} draws {listOfCards.Length} Card.");
    }

    public void SortHand()
    {
        float firstCalc = Mathf.Round(canvas.transform.localScale.x * 4) / 4f;
        float multiplier = firstCalc / 0.25f;
        listOfHand = listOfHand.OrderBy(card => card.dataFile.coinCost).ToList();
        UpdateButton();

        for (int i = 0; i < listOfHand.Count; i++)
        {
            Card nextCard = listOfHand[i];
            nextCard.transform.SetSiblingIndex(i);
            float startingX = (listOfHand.Count > 7) ? (-250 - (150 * multiplier)) : (listOfHand.Count - 1) * (-50 - 25 * multiplier);
            float difference = (listOfHand.Count > 7) ? (-250 - (150 * multiplier)) * -2 / (listOfHand.Count - 1) : 100 + (50 * multiplier);

            Vector2 newPosition = new(startingX + difference * i, -535 * canvas.transform.localScale.x);
            StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.3f));
        }

        if (!PhotonNetwork.IsConnected || this.pv.AmOwner)
        {
            foreach (Card card in listOfHand)
                StartCoroutine(card.RevealCard(0.25f));
        }
    }

    #endregion

#region Cards in Play

    public void SortPlay()
    {
        float firstCalc = Mathf.Round(canvas.transform.localScale.x * 4) / 4f;
        float multiplier = firstCalc / 0.25f;
        UpdateButton();

        for (int i = 0; i<6; i++)
        {
            try
            {
                Card nextCard = listOfPlay[i];
                nextCard.transform.SetSiblingIndex(i);
                Vector2 newPosition = new(-800 + (62.5f * multiplier * i), 175 * canvas.transform.localScale.x);
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
                nextCard.transform.SetSiblingIndex(i+6);
                Vector2 newPosition = new(-800 + (62.5f * multiplier * i), -185 * canvas.transform.localScale.x);
                StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.3f));
            }
            catch
            {
                break;
            }
        }

        foreach (Card card in listOfPlay)
            StartCoroutine(card.RevealCard(0.25f));
    }

    public IEnumerator ChooseCardToPlay(List<Card> cardsToPlay, List<Card> cardsToReplace, int logged)
    {
        if (cardsToPlay.Count == 0)
            yield break;

        Manager.instance.instructions.text = "Choose a card to play.";
        yield return ChooseCard(cardsToPlay, true);

        if (chosenCard != null)
        {
            Card cardToPlay = chosenCard;
            Card cardToDiscard = null;
            if (cardsToReplace.Count > 0)
            {
                Manager.instance.instructions.text = "Choose a card to replace.";
                yield return ChooseCard(cardsToReplace, false);
                cardToDiscard = chosenCard;
                DiscardRPC(cardToDiscard, logged);
            }

            if (PhotonNetwork.IsConnected)
                pv.RPC(nameof(PhotonViewToPlay), RpcTarget.All, cardToPlay.pv.ViewID, logged);
            else
                AddToPlayArea(cardToPlay, logged);

            MultiFunction(nameof(LoseCoin), RpcTarget.All, new object[2] { cardToPlay.dataFile.coinCost, logged });
            yield return new WaitForSeconds(0.25f);
            if (cardToDiscard != null)
                yield return cardToDiscard.ReplaceInstructions(this, logged + 1);
            yield return cardToPlay.PlayInstructions(this, logged+1);
        }
        else
        {
            MultiFunction(nameof(FailToPlay), RpcTarget.All, new object[1] { logged });
        }
    }

    void AddToPlayArea(Card card, int logged)
    {
        if (card == null)
        {
            cardsPlayed.Add(null);
            Log.instance.AddText($"{this.name} doesn't play anything.", logged);
            SortHand();
            SortPlay();
        }
        else
        {
            card.name = card.name.Replace("(Clone)", "");
            card.cg.alpha = 1;

            cardsPlayed.Add(card);
            listOfHand.Remove(card);
            listOfPlay.Add(card);
            card.transform.SetParent(cardplay);

            LoseCoin(card.dataFile.coinCost, logged);
            Log.instance.AddText($"{this.name} plays {card.name}.", logged);

            SortHand();
            SortPlay();
        }
    }

    [PunRPC]
    void FailToPlay(int logged)
    {
        AddToPlayArea(null, logged);
    }

    [PunRPC]
    void PhotonViewToPlay(int cardID, int logged)
    {
        AddToPlayArea(PhotonView.Find(cardID).GetComponent<Card>(), logged);
    }

    [PunRPC]
    public Card CreateJunkRPC(bool addToPlay, int logged)
    {
        Card newJunk = null;

        if (PhotonNetwork.IsConnected)
        {
            newJunk = PhotonNetwork.Instantiate(junkPrefab.name, Vector3.zero, new Quaternion()).GetComponent<Card>();
            if (addToPlay)
                pv.RPC(nameof(PhotonViewToPlay), RpcTarget.All, newJunk.pv.ViewID, logged);
        }
        else
        {
            newJunk = Instantiate(junkPrefab);
            if (addToPlay)
                AddToPlayArea(newJunk, logged);
        }

        return newJunk;
    }

    #endregion

#region Resources

    [PunRPC]
    public void GainCoin(int coins, int logged)
    {
        this.coins += coins;
        if (coins > 0)
            Log.instance.AddText($"{this.name} gains {coins} Coin.", logged);
        UpdateButton();
    }

    [PunRPC]
    public void LoseCoin(int coins, int logged)
    {
        this.coins = Mathf.Max(this.coins - coins, 0);
        if (coins > 0)
        Log.instance.AddText($"{this.name} loses {coins} Coin.", logged);
        UpdateButton();
    }

    [PunRPC]
    public void TakeNegCrown(int crowns, int logged)
    {
        this.negCrowns += crowns;
        if (crowns > 0)
            Log.instance.AddText($"{this.name} takes -{crowns} Neg Crown.", logged);
        UpdateButton();
    }

    [PunRPC]
    public void RemoveNegCrown(int crowns, int logged)
    {
        this.negCrowns = Mathf.Max(this.negCrowns - crowns, 0);
        if (crowns > 0)
            Log.instance.AddText($"{this.name} removes -{crowns} Neg Crown.", logged);
        UpdateButton();
    }

    [PunRPC]
    public void IgnoreUntilTurn(int amount)
    {
        if (amount > ignoreInstructions)
            ignoreInstructions = amount;
    }

    #endregion

#region Turn/Decisions

    public IEnumerator TakeTurnRPC(int turnNumber)
    {
        StartCoroutine(MultiEnumerator(nameof(TakeTurn), RpcTarget.All, new object[1] {turnNumber} ));
        myTurn = true;
        while (myTurn)
            yield return null;
    }

    [PunRPC]
    IEnumerator TakeTurn(int turnNumber)
    {
        Log.instance.AddText($"");
        Log.instance.AddText($"Turn {turnNumber} - {this.name}");
        cardsPlayed.Clear();

        if (!PhotonNetwork.IsConnected || this.pv.IsMine)
        {
            //start of turn events
            List<Card> startOfTurnEvents = Manager.instance.listOfEvents.Where
                (card => (card.dataFile.textBox.StartsWith("START OF TURN")
                && Manager.instance.ActiveEvent(card.dataFile.cardName))).ToList();
            while (startOfTurnEvents.Count > 0)
            {
                Popup eventPopup = Instantiate(CarryVariables.instance.cardPopup);
                eventPopup.StatsSetup("Choose an event to resolve.", Vector3.zero);
                foreach (Card next in startOfTurnEvents)
                    eventPopup.AddCardButton(next, 1);
                yield return eventPopup.WaitForChoice();

                Card eventToResolve = eventPopup.chosenCard;
                Destroy(eventPopup.gameObject);
                startOfTurnEvents.Remove(eventToResolve);
                yield return eventToResolve.PlayInstructions(this, 0);
            }

            //choosing actions
            Card actionToUse = null;
            if (Manager.instance.ActiveEvent("Repairs"))
            {
                actionToUse = Manager.instance.listOfEvents.Find(card => card.dataFile.cardName == "Upgrade");
            }
            else
            {
                Popup actionPopup = Instantiate(CarryVariables.instance.cardPopup);
                actionPopup.transform.SetParent(this.transform);
                actionPopup.StatsSetup("Actions", Vector3.zero);
                Manager.instance.instructions.text = "Choose an action.";

                foreach (Card action in Manager.instance.listOfActions)
                    actionPopup.AddCardButton(action, 1);
                yield return actionPopup.WaitForChoice();

                actionToUse = actionPopup.chosenCard;
                Destroy(actionPopup.gameObject);
            }

            Log.instance.MultiFunction(nameof(Log.instance.AddText), RpcTarget.All, new object[2] { $"{this.name} uses {actionToUse.name}.", 0 });
            yield return actionToUse.PlayInstructions(this, 0);

            //playing new card
            int currentCards = cardsPlayed.Count;
            Manager.instance.instructions.text = "Play a new card (or add a Junk).";
            yield return ChooseCardToPlay(listOfHand.Where(card => card.dataFile.coinCost <= coins).ToList(), new(), 0);
            if (currentCards == cardsPlayed.Count)
                CreateJunkRPC(true, 0);

            MultiFunction(nameof(EndTurn), RpcTarget.All, new object[1] { Manager.instance.listOfActions.FindIndex(action => action == actionToUse) });
        }
    }

    [PunRPC]
    void EndTurn(int chosenAction)
    {
        lastUsedAction = Manager.instance.listOfActions[chosenAction];
        myTurn = false;
        cardsPlayed.Clear();
        ignoreInstructions = Mathf.Max(0, ignoreInstructions - 1);
    }

    public IEnumerator ChooseCard(List<Card> possibleCards, bool optional)
    {
        choice = -10;
        chosenCard = null;

        if (possibleCards.Count > 0)
        {
            Popup popup = null;

            if (optional)
            {
                popup = Instantiate(CarryVariables.instance.textPopup);
                popup.transform.SetParent(GameObject.Find("Canvas").transform);
                popup.StatsSetup("Decline?", Vector3.zero);
                popup.AddTextButton("Decline");
                StartCoroutine(popup.WaitForChoice());
            }

            for (int i = 0; i < possibleCards.Count; i++)
            {
                Card nextCard = possibleCards[i];
                int buttonNumber = i;

                nextCard.button.onClick.RemoveAllListeners();
                nextCard.button.interactable = true;
                nextCard.button.onClick.AddListener(() => ReceiveChoice(buttonNumber));
                nextCard.border.gameObject.SetActive(true);
            }

            if (possibleCards.Count == 1 && !optional)
            {
                choice = 0;
            }
            else
            {
                while (choice == -10)
                {
                    yield return null;
                    if (optional && popup.chosenButton > -10)
                        break;
                }
            }

            if (popup != null)
                Destroy(popup.gameObject);

            chosenCard = (choice >= 0) ? possibleCards[choice] : null;

            for (int i = 0; i < possibleCards.Count; i++)
            {
                Card nextCard = possibleCards[i];
                nextCard.button.onClick.RemoveAllListeners();
                nextCard.button.interactable = false;
                nextCard.border.gameObject.SetActive(false);
            }
        }
    }

    void ReceiveChoice(int number)
    {
        Debug.Log(number);
        choice = number;
    }

#endregion

}
