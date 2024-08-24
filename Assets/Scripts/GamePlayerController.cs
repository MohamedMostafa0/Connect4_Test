using System.Collections;
using System.Collections.Generic;
using ConnectFour;
using Mirror;
using Mirror.Examples.NetworkRoom;
using UnityEngine;

public class GamePlayerController : NetworkBehaviour
{
    public int id;

    public void Init(int id)
    {
        this.id = id;
    }

    public override void OnStartAuthority()
    {
        this.enabled = true;
    }

    public override void OnStopAuthority()
    {
        this.enabled = false;
    }
}
