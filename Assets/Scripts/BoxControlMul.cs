using System;
using Unity.Netcode;
using UnityEngine;

public class BoxControlMul : NetworkBehaviour
{
    private void PickBox()
    {
        if (!NetworkObject.IsSpawned){ return;}
        NetworkObject.Despawn();
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (!NetworkManager.Singleton.IsServer || !NetworkObject.IsSpawned){return; }
        
        if (other.gameObject.CompareTag("Player"))
        {
            PickBox();
        }
    }
}
