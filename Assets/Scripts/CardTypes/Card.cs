using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyBox;
using System.Reflection;
using System.Linq;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using UnityEditor.Playables;

[RequireComponent(typeof(PhotonView))]
public class Card : MonoBehaviour
{

#region Variables

    public Dictionary<string, MethodInfo> enumeratorDictionary = new();

    [ReadOnly] public PhotonView pv;
    [ReadOnly] public Image image;
    [ReadOnly] public CardData dataFile;
    [ReadOnly] public Button button;
    protected CanvasGroup cg;

    protected TMP_Text titleText;
    protected TMP_Text descriptionText;
    protected TMP_Text artText;

    public Sprite faceDownSprite;
    [ReadOnly] public Sprite originalSprite;

    #endregion

#region Setup

    private void Awake()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        pv = GetComponent<PhotonView>();
        cg = transform.Find("Canvas Group").GetComponent<CanvasGroup>();
    }

    [PunRPC]
    public virtual void GetDataFile(int fileSlot)
    {
        titleText.text = dataFile.cardName;
        descriptionText.text = dataFile.textBox;
        artText.text = dataFile.artCredit;

        foreach (string nextSection in dataFile.commandInstructions)
        {
            string[] nextSplit = DownloadSheets.instance.SpliceString(nextSection.Trim(), '/');
            foreach (string small in nextSplit)
            {
                if (small.Equals("None") || small.Equals("") || enumeratorDictionary.ContainsKey(small))
                    continue;

                MethodInfo method = typeof(Card).GetMethod(small, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && method.ReturnType == typeof(IEnumerator))
                    enumeratorDictionary.Add(small, method);
                else
                    Debug.LogError($"{dataFile.cardName}: instructions: {small} doesn't exist");
            }
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
        if (image.sprite == faceDownSprite)
        {
            cg.alpha = 0;
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
            image.sprite = null;
            image.color = Color.black;
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

}
