using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class Action : Card
{
    [PunRPC]
    public override void GetDataFile(int fileSlot)
    {
        try
        {
            this.dataFile = DownloadSheets.instance.mainActionData[fileSlot];
        }
        catch
        {
            this.dataFile = DownloadSheets.instance.specialActionData[fileSlot- DownloadSheets.instance.mainActionData.Count];
        }
        this.transform.SetParent(Manager.instance.actions);
        Manager.instance.listOfActions.Add(this);
        this.originalSprite = Resources.Load<Sprite>($"Action/{this.dataFile.cardName}");
        base.GetDataFile(fileSlot);
    }
}
