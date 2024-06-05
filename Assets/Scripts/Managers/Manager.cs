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
    public static Manager instance;
    [ReadOnly] public PhotonView pv;

    [Foldout("Text", true)]
    public TMP_Text instructions;
    public Transform deck;
    public Transform discard;

    [Foldout("Animation", true)]
    [ReadOnly] public float opacity = 1;
    [ReadOnly] public bool decrease = true;
    [ReadOnly] public bool gameon = false;

    [ReadOnly] public Dictionary<string, MethodInfo> dictionary = new();

    private void Awake()
    {
        AddToDictionary(nameof(AssignPlayers));
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
            BeginGame();
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
            BeginGame();
        }
    }

    void BeginGame()
    {
        if (PhotonNetwork.IsConnected)
        {
            for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
            {
                Player nextPlayer = GameObject.Find(PhotonNetwork.PlayerList[i].NickName).GetComponent<Player>();
                //nextPlayer.PlayerSetup(i, nextPlayer.name);
            }
        }

        MultiFunction(dictionary[nameof(AssignPlayers)], RpcTarget.All);
        StartCoroutine(PlayUntilFinish());
    }

    [PunRPC]
    void AssignPlayers()
    {
    }

    IEnumerator PlayUntilFinish()
    {
        yield return null;
    }
}
