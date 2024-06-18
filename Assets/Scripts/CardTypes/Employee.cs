using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Employee : Card
{
    [PunRPC]
    public override void GetDataFile(int fileSlot)
    {
        this.dataFile = DownloadSheets.instance.deckCardData[fileSlot];
        this.transform.SetParent(Manager.instance.deck);
        this.originalSprite = Resources.Load<Sprite>($"Employee/{this.dataFile.cardName}");
        base.GetDataFile(fileSlot);
    }
}
