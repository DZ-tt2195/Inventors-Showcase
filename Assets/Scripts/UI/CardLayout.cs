using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MyBox;
using TMPro;

public class CardLayout : MonoBehaviour, IPointerClickHandler
{
    [ReadOnly] public CanvasGroup cg;
    TMP_Text titleText;
    TMP_Text descriptionText;
    TMP_Text artText;
    TMP_Text coinText;
    TMP_Text crownText;
    Image artBox;
    CardData dataFile;

    private void Awake()
    {
        cg = transform.Find("Canvas Group").GetComponent<CanvasGroup>();
        titleText = cg.transform.Find("Title").GetComponent<TMP_Text>();
        try
        {
            coinText = cg.transform.Find("Coin").GetComponent<TMP_Text>();
            crownText = cg.transform.Find("Crown").GetComponent<TMP_Text>();
        }
        catch
        {
            coinText = null;
            crownText = null;
        }
        descriptionText = cg.transform.Find("Description").GetComponent<TMP_Text>();
        artBox = cg.transform.Find("Art Box").GetComponent<Image>();
        artText = cg.transform.Find("Art Credit").GetComponent<TMP_Text>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            RightClickInfo();
        }
    }

    public void FillInCards(CardData dataFile, Sprite sprite)
    {
        this.dataFile = dataFile;
        titleText.text = dataFile.cardName;
        descriptionText.text = dataFile.textBox;
        titleText.text = KeywordTooltip.instance.EditText(titleText.text);
        descriptionText.text = KeywordTooltip.instance.EditText(descriptionText.text);

        artText.text = dataFile.artCredit;
        artBox.sprite = sprite;

        try
        {
            coinText.text = $"{dataFile.coinCost} Coin";
            crownText.text = $"{dataFile.scoringCrowns} Pos Crown";
            coinText.text = KeywordTooltip.instance.EditText(coinText.text);
            crownText.text = KeywordTooltip.instance.EditText(crownText.text);
        }
        catch
        {
            //do nothing
        }
    }

    void RightClickInfo()
    {
        CarryVariables.instance.RightClickDisplay(cg.alpha, artBox.sprite, this.dataFile);
    }
}
