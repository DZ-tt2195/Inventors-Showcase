using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class Action : Card
{
    [PunRPC]
    public override void GetDataFile(int fileSlot)
    {
        this.dataFile = DownloadSheets.instance.mainActionData[fileSlot];
        this.transform.SetParent(Manager.instance.actions);
        Manager.instance.listOfActions.Add(this);
        this.originalSprite = Resources.Load<Sprite>($"Action/{this.dataFile.cardName}");
        base.GetDataFile(fileSlot);
    }
}
