using System;
using System.ComponentModel;
using Unity.Netcode;
using UnityEngine;

public class EnemyMovementMul : NetworkBehaviour
{
    public static int health = 1;
    

    private void DestroyEnemy()
    {
        if (!NetworkObject.IsSpawned){ return;}
        NetworkObject.Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!NetworkManager.Singleton.IsServer || !NetworkObject.IsSpawned){return; }

        DestroyEnemy();
        return;
        if (other.gameObject.CompareTag("Bullet"))
        {
            DestroyEnemy();
        }else if (other.gameObject.CompareTag("Barier"))
        {
            DestroyEnemy();

        }
    }
}
