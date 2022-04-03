using System;
using System.ComponentModel;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

public class EnemyMovementMul : NetworkBehaviour
{
    public NetworkVariable<int> health = new NetworkVariable<int>();

    void Start()
    {
        SetHpServerRpc();
    }
    
    private void DestroyEnemy()
    {
        if (!NetworkObject.IsSpawned){ return;}
        
        //stupid but works 
        SetHpServerRpc();
        NetworkObject.Despawn();
        
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!NetworkManager.Singleton.IsServer || !NetworkObject.IsSpawned){return; }
        
        if (other.gameObject.CompareTag("Bullet"))
        {
            DecreaseHpServerRpc();
            Debug.Log("bullet " + health.Value);
            if (health.Value <= 0)
            {
                DestroyEnemy();
            }

        }else if (other.gameObject.CompareTag("Barier"))
        {
            DestroyEnemy();
        }
    }

    [ServerRpc]
    void DecreaseHpServerRpc()
    {
        health.Value -= 1;
    }
    
    [ServerRpc]
    void SetHpServerRpc()
    {
        health.Value = 3;
        Debug.Log("my hp: " + health.Value);
    }
}
