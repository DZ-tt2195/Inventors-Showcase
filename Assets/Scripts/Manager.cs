using System.Collections;
using System.Collections.Generic;
using MyBox;
using TMPro;
using UnityEngine;

public class Manager : MonoBehaviour
{
    public static Manager instance;

    [Foldout("Text", true)]
    [ReadOnly] public TMP_Text instructions;
    [ReadOnly] public TMP_Text numbers;
    public Transform deck;
    public Transform discard;

    [Foldout("Animation", true)]
    [ReadOnly] public float opacity = 1;
    [ReadOnly] public bool decrease = true;
    [ReadOnly] public bool gameon = false;

}
