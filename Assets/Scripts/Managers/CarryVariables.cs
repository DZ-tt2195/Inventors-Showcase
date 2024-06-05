using System.Collections.Generic;
using UnityEngine;
using MyBox;
using System.Reflection;
using Photon.Pun;
using System.Collections;

[RequireComponent(typeof(PhotonView))]
public class CarryVariables : MonoBehaviour
{
    public static CarryVariables instance;
    public Player playerPrefab;

    PhotonView pv;
    [ReadOnly] public Dictionary<string, MethodInfo> dictionary = new();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Application.targetFrameRate = 60;
            pv = GetComponent<PhotonView>();
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    private void Start()
    {
    }

    public void AddToDictionary(string methodName)
    {
        MethodInfo method = typeof(CarryVariables).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            dictionary.Add(methodName, method);
    }

    public void MultiFunction(MethodInfo function, RpcTarget affects, object[] parameters = null)
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
}
