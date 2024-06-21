using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class Event : Card
{
    [PunRPC]
    public override void GetDataFile(int fileSlot)
    {
        this.dataFile = DownloadSheets.instance.eventData[fileSlot];
        this.transform.SetParent(Manager.instance.events);
        this.transform.localPosition = new Vector3(-800 + 250*fileSlot, 525);
        Manager.instance.listOfEvents.Add(this);
        this.originalSprite = Resources.Load<Sprite>($"Event/{this.dataFile.cardName}");
        base.GetDataFile(fileSlot);
    }
}
