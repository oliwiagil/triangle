using System;
using System.ComponentModel;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

public class EnemyMovementMul : NetworkBehaviour
{
    public NetworkVariable<int> health = new NetworkVariable<int>();
    private Random random;
    private float scale = 10f;
    private float range = 256;

	void Awake()
	{
		random = new Random();
	}

    void Start()
    {
        SetHpServerRpc();
		InvokeRepeating("ChangeMovement", 0, 3);
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
    }
	
	void ChangeMovement()
	{
		if (!NetworkObject.IsSpawned){ return;}
		ChangeMovementServerRpc();
	}

	[ServerRpc]
	void ChangeMovementServerRpc()
	{		
		Rigidbody2D rb = NetworkObject.GetComponent<Rigidbody2D>();
		Vector3 v = new Vector3(random.Next((int) -range, (int) range) / range * scale,
        random.Next((int) -range, (int) range) / range * scale, 0);
    	v.Normalize();
    	rb.AddForce(v * 1, ForceMode2D.Impulse);
	}
}
