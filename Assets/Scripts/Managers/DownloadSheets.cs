using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyBox;
using System.Linq;
using UnityEngine.Networking;
using System.IO;
using System;
using System.Text.RegularExpressions;

[Serializable]
public class CardData
{
    public string cardName;
    public string textBox;
    public int coinCost;
    public int scoringCrowns;
    public string[] playInstructions;
    public string[] replaceInstructions;
    public int numDraw;
    public int numGain;
    public int numCrowns;
    public int numPlayCost;
    public int numMisc;
    public bool isDirector;
    public string artCredit;
    public PlayerTarget[] whoToTarget;
    public List<int> eventTimes;
}

public enum PlayerTarget { You, All, Others }

public class DownloadSheets : MonoBehaviour
{
    private string ID = "1P-uWG_LQwUUX57IsNwXg-FHdNa3b1dzpJt0oe7en7PY";
    private string apiKey = "AIzaSyCl_GqHd1-WROqf7i2YddE3zH6vSv3sNTA";
    private string baseUrl = "https://sheets.googleapis.com/v4/spreadsheets/";

    public static DownloadSheets instance;

    Dictionary<string, int> cardSheetsColumns = new();
    [ReadOnly] public List<CardData> mainActionData = new();
    [ReadOnly] public List<CardData> robotData = new();
    [ReadOnly] public List<CardData> eventData = new();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(DownloadEverything());
    }

    IEnumerator DownloadEverything()
    {
        if (Application.isEditor)
        {
            CoroutineGroup group = new(this);
            group.StartCoroutine(Download("Main Action"));
            group.StartCoroutine(Download("Robots"));
            group.StartCoroutine(Download("Events"));

            while (group.AnyProcessing)
                yield return null;
        }

        mainActionData = ReadCardData("Main Action");
        robotData = ReadCardData("Robots");
        eventData = ReadCardData("Events");
    }

    IEnumerator Download(string range)
    {
        string url = $"{baseUrl}{ID}/values/{range}?key={apiKey}";
        using UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError($"Download failed: {www.error}");
        }
        else
        {
            string filePath = $"Assets/Resources/File Data/{range}.txt";
            File.WriteAllText($"{filePath}", www.downloadHandler.text);

            string[] allLines = File.ReadAllLines($"{filePath}");
            List<string> modifiedLines = allLines.ToList();
            modifiedLines.RemoveRange(1, 3);
            File.WriteAllLines($"{filePath}", modifiedLines.ToArray());
            Debug.Log($"downloaded {range}");
        }
    }

    string[][] ReadFile(string file)
    {
        TextAsset data = Resources.Load($"File Data/{file}") as TextAsset;

        string editData = data.text;
        editData = editData.Replace("],", "").Replace("{", "").Replace("}", "");

        string[] numLines = editData.Split("[");
        string[][] list = new string[numLines.Length][];

        for (int i = 0; i < numLines.Length; i++)
        {
            list[i] = numLines[i].Split("\",");
        }
        return list;
    }

    List<CardData> ReadCardData(string fileToLoad)
    {
        List<CardData> listOfData = new();
        var data = ReadFile(fileToLoad);

        for (int i = 0; i < data[1].Length; i++)
        {
            string next = data[1][i].Trim().Replace("\"", "");
            if (!cardSheetsColumns.ContainsKey(next))
            {
                cardSheetsColumns.Add(next, i);
                //Debug.Log($"{next}, {i}");
            }
        }

        for (int i = 2; i < data.Length; i++)
        {
            CardData nextData = new();
            listOfData.Add(nextData);

            for (int j = 0; j < data[i].Length; j++)
                data[i][j] = data[i][j].Trim().Replace("\"", "").Replace("\\", "").Replace("]", "");

            nextData.cardName = data[i][cardSheetsColumns[nameof(CardData.cardName)]];
            nextData.textBox = data[i][cardSheetsColumns[nameof(CardData.textBox)]];
            nextData.coinCost = StringToInt(data[i][cardSheetsColumns[nameof(CardData.coinCost)]]);
            nextData.scoringCrowns = StringToInt(data[i][cardSheetsColumns[nameof(CardData.scoringCrowns)]]);

            string nextRow = data[i][cardSheetsColumns[nameof(CardData.playInstructions)]];
            nextData.playInstructions = (nextRow == "") ? new string[1] { "None" } : SpliceString(nextRow, '-');

            nextRow = data[i][cardSheetsColumns[nameof(CardData.replaceInstructions)]];
            nextData.replaceInstructions = (nextRow == "") ? new string[1] { "None" } : SpliceString(nextRow, '-');

            nextData.numDraw = StringToInt(data[i][cardSheetsColumns[nameof(CardData.numDraw)]]);
            nextData.numGain = StringToInt(data[i][cardSheetsColumns[nameof(CardData.numGain)]]);
            nextData.numCrowns = StringToInt(data[i][cardSheetsColumns[nameof(CardData.numCrowns)]]);
            nextData.numPlayCost = StringToInt(data[i][cardSheetsColumns[nameof(CardData.numPlayCost)]]);
            nextData.numMisc = StringToInt(data[i][cardSheetsColumns[nameof(CardData.numMisc)]]);

            nextData.isDirector = data[i][cardSheetsColumns[nameof(CardData.isDirector)]] == "TRUE";
            nextData.artCredit = data[i][cardSheetsColumns[nameof(CardData.artCredit)]];

            string[] listOfTargets = (data[i][cardSheetsColumns[nameof(CardData.whoToTarget)]].Equals("") ? new string[1] { "None" } :
                SpliceString(data[i][cardSheetsColumns[nameof(CardData.whoToTarget)]].Trim().ToUpper(), '-'));
            PlayerTarget[] convertToTargets = new PlayerTarget[listOfTargets.Length];
            for (int j = 0; j < listOfTargets.Length; j++)
                convertToTargets[j] = StringToPlayerTarget(listOfTargets[j]);
            nextData.whoToTarget = convertToTargets;

            string[] arrayOfInts = (data[i][cardSheetsColumns[nameof(CardData.eventTimes)]].Equals("") ? null :
                SpliceString(data[i][cardSheetsColumns[nameof(CardData.eventTimes)]].Trim().ToUpper(), ','));
            if (arrayOfInts == null)
            {
                nextData.eventTimes = new();
            }
            else
            {
                List<int> listOfInts = new();
                for (int j = 0; j < arrayOfInts.Length; j++)
                    listOfInts.Add(StringToInt(arrayOfInts[j]));
                nextData.eventTimes = listOfInts;
            }
        }

        return listOfData;
    }

    PlayerTarget StringToPlayerTarget(string line)
    {
        switch (line)
        {
            case "YOU":
                return PlayerTarget.You;
            case "ALL":
                return PlayerTarget.All;
            case "OTHERS":
                return PlayerTarget.Others;
            default:
                Debug.LogError("missing team target");
                break;
        }
        return PlayerTarget.You;
    }

    int StringToInt(string line)
    {
        line = line.Trim();
        try
        {
            return (line.Equals("")) ? -1 : int.Parse(line);
        }
        catch (FormatException)
        {
            return -1;
        }
    }

    public string[] SpliceString(string text, char splitUp)
    {
        if (!text.IsNullOrEmpty())
        {
            string divide = text.Replace(" ", "").Trim();
            string[] splitIntoStrings = divide.Split(splitUp);
            return splitIntoStrings;
        }
        else
        {
            return new string[0];
        }
    }

    void VerifyNumbers(CardData data, string toReplace, string newText)
    {
        int count = Regex.Matches(data.textBox, toReplace).Count;
        if (count > 0)
        {
            try
            {
                if (int.Parse(newText) == 0)
                    Debug.LogError($"{data.cardName} has wrong {toReplace}");
            }
            catch (FormatException)
            {
                if (float.Parse(newText) == 0f)
                    Debug.LogError($"{data.cardName} has wrong {toReplace}");
            }
        }
    }
}
