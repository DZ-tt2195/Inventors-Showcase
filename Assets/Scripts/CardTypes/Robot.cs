using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Robot : Card
{
    [PunRPC]
    public override void GetDataFile(int fileSlot)
    {
        this.dataFile = DownloadSheets.instance.robotData[fileSlot];
        this.transform.SetParent(Manager.instance.deck);
        this.originalSprite = Resources.Load<Sprite>($"Robot/{this.dataFile.cardName}");
        base.GetDataFile(fileSlot);
    }
}
