using System;
using System.ComponentModel;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

public class EnemyMovementMul : NetworkBehaviour
{
    static string s_ObjectPoolTag = "ObjectPool";
    NetworkObjectPool m_ObjectPool;
    public GameObject BulletPrefab;
    private GameObject player;

    public Image healthBar;

    public float bulletForce = 5;
    public float fireRate = 3;

    NetworkVariable<int> health = new NetworkVariable<int>();
    private int maxHealth=3;

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

        healthBar.transform.rotation = Quaternion.Euler (0, 0, 0);
        healthBar.transform.position = transform.position + new Vector3 (0, 0.9f,0);
    
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject target in players)
        {
            Vector2 direction2 = target.transform.position - transform.position;
            //vector.magnitude - returns the length of vector
            float distance = direction2.magnitude;
            direction2.Normalize();

            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction2, distance);
            Debug.Log(distance);
            if (hit.collider == null)
            {
                Debug.DrawRay(transform.position, direction2* distance, Color.green);
            }
            else
            {
                Debug.DrawRay(transform.position, direction2* hit.distance , Color.white);
            }
        }
        
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
        if (!NetworkObject.IsSpawned || !NetworkManager.Singleton.IsServer 
            || !other.gameObject.CompareTag("PlayerBullet")){return; }

        DecreaseHpServerRpc();
        if (health.Value <= 0)
        {
            DestroyEnemy();
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
        DecreaseHpClientRpc(health.Value);
    }

    [ClientRpc]
    void DecreaseHpClientRpc(int currentHealth)
    {
        int healthBarWidth=220;
        healthBar.rectTransform.sizeDelta = new Vector2((healthBarWidth*currentHealth)/maxHealth, 20);
        byte maxByteValue=255;
        byte green=(byte)((maxByteValue*currentHealth)/maxHealth);
        healthBar.color=new Color32((byte)(maxByteValue-green),green,0,maxByteValue);
    }
    
    [ServerRpc]
    void SetHpServerRpc()
    {
        health.Value = maxHealth;
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
