using System;
using Unity.Netcode;
using UnityEngine;

public class BulletMovementMul : NetworkBehaviour
{
    public int damage = 1;

    private void DestroyBullet()
    {
        if (!NetworkObject.IsSpawned){ return;}
        NetworkObject.Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!NetworkManager.Singleton.IsServer || !NetworkObject.IsSpawned){return; }

        if (!other.gameObject.CompareTag("Bullet")) {
            DestroyBullet();
        }

    }
}
