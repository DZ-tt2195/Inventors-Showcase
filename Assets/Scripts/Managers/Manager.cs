using System.Collections;
using System.Collections.Generic;
using MyBox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;
using ExitGames.Client.Photon;
using Photon.Realtime;
using System.Linq;
using System.Reflection;

[RequireComponent(typeof(PhotonView))]
public class Manager : MonoBehaviour
{

#region Variables

    public static Manager instance;
    [ReadOnly] public PhotonView pv;

    [Foldout("Text", true)]
    public TMP_Text instructions;
    public Transform deck;
    public Transform discard;
    public Transform actions;
    public Transform events;

    [Foldout("Animation", true)]
    [ReadOnly] public float opacity = 1;
    [ReadOnly] public bool decrease = true;
    [ReadOnly] public bool gameOn = false;

    [Foldout("Lists", true)]
    [ReadOnly] public List<Player> playersInOrder = new();
    [ReadOnly] public List<Action> listOfActions = new();
    [ReadOnly] public List<Event> listOfEvents = new();
    [ReadOnly] public Dictionary<string, MethodInfo> dictionary = new();

    #endregion

#region Setup

    private void Awake()
    {
        instance = this;
        pv = GetComponent<PhotonView>();
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
        MethodInfo method = typeof(Manager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            dictionary.Add(methodName, method);
    }

    private void FixedUpdate()
    {
        if (decrease)
            opacity -= 0.05f;
        else
            opacity += 0.05f;
        if (opacity < 0 || opacity > 1)
            decrease = !decrease;
    }

    void Start()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Instantiate(CarryVariables.instance.playerPrefab.name, new Vector3(-10000, -10000, 0), new Quaternion());
            StartCoroutine(WaitForPlayers());
        }
        else
        {
            Player solitairePlayer = Instantiate(CarryVariables.instance.playerPrefab, new Vector3(-10000, -10000, 0), new Quaternion());
            solitairePlayer.name = "Solitaire";

            GetPlayers();
            CreateEmployees();
            CreateEvents();
            CreateActions();
            StartCoroutine(PlayUntilFinish());
        }
    }

    IEnumerator WaitForPlayers()
    {
        instructions.text = "Waiting...";
        while (PhotonNetwork.CurrentRoom.PlayerCount < PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            yield return null;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            yield return new WaitForSeconds(0.5f);
            GetPlayers();

            CreateEmployees();
            CreateActions();
            CreateEvents();
            StartCoroutine(PlayUntilFinish());
        }
    }

    void GetPlayers()
    {
        List<Player> listOfPlayers = FindObjectsOfType<Player>().ToList();
        int counter = 0;
        while (listOfPlayers.Count > 0)
        {
            int randomRemove = Random.Range(0, listOfPlayers.Count);
            MultiFunction(nameof(AddPlayer), RpcTarget.All, new object[2] { listOfPlayers[randomRemove].name, counter });
            listOfPlayers.RemoveAt(randomRemove);
            counter++;
        }
    }

    [PunRPC]
    void AddPlayer(string name, int position)
    {
        Player nextPlayer = GameObject.Find(name).GetComponent<Player>();
        playersInOrder.Insert(position, nextPlayer);
        nextPlayer.AssignInfo(position);
    }

    void CreateEmployees()
    {
        for (int i = 0; i < DownloadSheets.instance.robotData.Count; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                Robot nextCard = null;
                if (PhotonNetwork.IsConnected)
                {
                    nextCard = PhotonNetwork.Instantiate(CarryVariables.instance.robotPrefab.name, new Vector3(-10000, -10000), new Quaternion()).GetComponent<Robot>();
                    nextCard.pv.RPC("GetDataFile", RpcTarget.All, i);
                }
                else
                {
                    nextCard = Instantiate(CarryVariables.instance.robotPrefab, new Vector3(-10000, -10000), new Quaternion());
                    nextCard.GetDataFile(i);
                }
            }
        }
        deck.Shuffle();
        foreach (Player player in playersInOrder)
        {
            player.MultiFunction(nameof(player.RequestDraw), RpcTarget.MasterClient, new object[2] { 2, 0 });
            player.MultiFunction(nameof(player.GainCoin), RpcTarget.All, new object[2] { 4, 0 });
        }
    }

    void CreateActions()
    {
        for (int i = 0; i < DownloadSheets.instance.mainActionData.Count; i++)
        {
            Action nextCard = null;
            if (PhotonNetwork.IsConnected)
            {
                nextCard = PhotonNetwork.Instantiate(CarryVariables.instance.actionPrefab.name, new Vector3(-10000, -10000), new Quaternion()).GetComponent<Action>();
                nextCard.pv.RPC("GetDataFile", RpcTarget.All, i);
            }
            else
            {
                nextCard = Instantiate(CarryVariables.instance.actionPrefab, new Vector3(-10000, -10000), new Quaternion());
                nextCard.GetDataFile(i);
            }
        }
    }

    void CreateEvents()
    {
        DownloadSheets.instance.eventData = DownloadSheets.instance.eventData.Shuffle();
        Event nextCard = null;
        if (PhotonNetwork.IsConnected)
        {
            nextCard = PhotonNetwork.Instantiate(CarryVariables.instance.eventPrefab.name, new Vector3(-10000, -10000), new Quaternion()).GetComponent<Event>();
            nextCard.pv.RPC("GetDataFile", RpcTarget.All, 0);
        }
        else
        {
            nextCard = Instantiate(CarryVariables.instance.eventPrefab, new Vector3(-10000, -10000), new Quaternion());
            nextCard.GetDataFile(0);
        }

        /*
        for (int i = 0; i < 2; i++)
        {
            Event nextCard = null;
            if (PhotonNetwork.IsConnected)
            {
                nextCard = PhotonNetwork.Instantiate(CarryVariables.instance.eventPrefab.name, new Vector3(-10000, -10000), new Quaternion()).GetComponent<Event>();
                nextCard.pv.RPC("GetDataFile", RpcTarget.All, i);
            }
            else
            {
                nextCard = Instantiate(CarryVariables.instance.eventPrefab, new Vector3(-10000, -10000), new Quaternion());
                nextCard.GetDataFile(i);
            }
        }
        */
    }

    #endregion

    #region Gameplay

    IEnumerator PlayUntilFinish()
    {
        gameOn = true;

        for (int j = 0; j < 10; j++)
        {
            foreach (Player player in playersInOrder)
            {
                yield return player.TakeTurnRPC(j+1);
                yield return new WaitForSeconds(0.25f);
            }
        }
    }

    #endregion

}
