using System;
using System.ComponentModel;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

public class EnemyMovementMul : NetworkBehaviour
{
    static string s_ObjectPoolTag = "ObjectPool";
    NetworkObjectPool m_ObjectPool;
    public GameObject BulletPrefab;
    private GameObject player;

    public float bulletForce = 5;
    public float fireRate = 3;

    public NetworkVariable<int> health = new NetworkVariable<int>();
    private Random random;
    private float scale = 10f;
    private float range = 256;

	void Awake()
	{
		random = new Random();
        m_ObjectPool = GameObject.FindWithTag(s_ObjectPoolTag).GetComponent<NetworkObjectPool>();
    }

    void Start()
    {
        if (NetworkManager.Singleton.IsServer){
            SetHpServerRpc();
        }

		InvokeRepeating("ChangeMovement", 0, 3);
        InvokeRepeating("Fire", fireRate, fireRate);
    }

    public GameObject GetClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject closest = null;
        float minDistance = Mathf.Infinity;
        Vector3 position = transform.position;

        foreach (GameObject target in players)
        {
            Vector3 diff = target.transform.position - position;
            //vector.sqrMagnitude - returns the squared length of vector
            float distance = diff.sqrMagnitude;
            if (distance < minDistance)
            {
                closest = target;
                minDistance = distance;
            }
        }
        return closest;
    }

    void Update(){
        player = GetClosestPlayer();
        Vector2 direction = player.transform.position - transform.position;
        float angle = Vector2.SignedAngle(Vector2.up, direction);
        transform.eulerAngles = new Vector3 (0, 0, angle);
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

        }
    }

    void Fire()
    {
		if (!NetworkObject.IsSpawned || !NetworkManager.Singleton.IsServer){ return;}
        FireServerRpc();
    }

    [ServerRpc]
    void FireServerRpc()
    {
        GameObject bullet = m_ObjectPool.GetNetworkObject(BulletPrefab).gameObject;
        bullet.transform.position = transform.position;
        bullet.transform.rotation = transform.rotation;
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        rb.AddForce(transform.up * bulletForce, ForceMode2D.Impulse);
        Physics2D.IgnoreCollision(bullet.GetComponent<Collider2D>(),  GetComponent<Collider2D>());
        bullet.GetComponent<NetworkObject>().Spawn(true);
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
		if (!NetworkObject.IsSpawned || !NetworkManager.Singleton.IsServer){ return;}
		ChangeMovementServerRpc();
	}

	[ServerRpc]
	void ChangeMovementServerRpc()
	{		
		Rigidbody2D rb = NetworkObject.GetComponent<Rigidbody2D>();
		Vector3 v = new Vector3(random.Next((int) -range, (int) range) / range * scale,
        random.Next((int) -range, (int) range) / range * scale, 0);
    	v.Normalize();

        rb.velocity = transform.TransformDirection(v);
    	//rb.AddForce(v * 1, ForceMode2D.Impulse);
	}
}
