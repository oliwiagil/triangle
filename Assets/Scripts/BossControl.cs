using System;
using System.ComponentModel;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;
using System.Linq;

public class BossControl : NetworkBehaviour
{
    static string s_ObjectPoolTag = "ObjectPool";
    private NetworkObjectPool m_ObjectPool;
    public GameObject BulletPrefab;
    private GameObject player;

    public Image healthBar;

    public float bulletForce = 10;
    public float fireRate = 3;

    private NetworkVariable<int> health = new NetworkVariable<int>();
    private int maxHealth = 20;

    private Random random;
    private float scale = 10f;
    private float range = 256;

    private bool seePlayer = false;
    private bool randomMovementOn = true;

    private Rigidbody2D rigidbody2D;

    void Awake()
    {
        random = new Random();
        m_ObjectPool = GameObject.FindWithTag(s_ObjectPoolTag).GetComponent<NetworkObjectPool>();
        rigidbody2D = NetworkObject.GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SetHpServerRpc();
        }
		GameObject[] boss = GameObject.FindGameObjectsWithTag("Boss");
		boss[0].SetActive(true);
        InvokeRepeating("ChangeMovement", 0, 10);
        DecreaseHpClientRpc(maxHealth);
    }

    public GameObject GetClosestVisiblePlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        //players is sorted according to distance from this enemy
        players = players.OrderBy(
            x => (this.transform.position - x.transform.position).sqrMagnitude
        ).ToArray();

        foreach (GameObject target in players)
        {
            Vector2 direction = target.transform.position - transform.position;
            float distance = direction.magnitude;
            direction.Normalize();

            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance);
            if (hit.collider == null)
            {
                //  Debug.DrawRay(transform.position, direction* distance, Color.green);
                return target;
            }
            /*
            else
            {
                Debug.DrawRay(transform.position, direction* hit.distance , Color.white);
            }
            */
        }

        return null;

    }

    void Update()
    {
        player = GetClosestVisiblePlayer();

        if (player != null)
        {
            seePlayer = true;
            if (randomMovementOn)
            {
                CancelInvoke("ChangeMovement");
                InvokeRepeating("Fire", 0, fireRate);
                randomMovementOn = false;
            }

            Vector2 direction = player.transform.position - transform.position;
            if (direction.sqrMagnitude > 5)
            {
                float angle = Vector2.SignedAngle(Vector2.up, direction);
                transform.eulerAngles = new Vector3(0, 0, angle);
                rigidbody2D.velocity = transform.TransformDirection(Vector2.up);
            }
            else
            {
                rigidbody2D.velocity = new Vector2(0, 0);
            }
        }
        else
        {
            seePlayer = false;
        }

        if (!seePlayer && !randomMovementOn)
        {
            InvokeRepeating("ChangeMovement", 0, 10);
            CancelInvoke("Fire");
            randomMovementOn = true;
        }

        healthBar.transform.rotation = Quaternion.Euler(0, 0, 0);
        healthBar.transform.position = transform.position + new Vector3(0, 0.9f, 0);
    }

    private void DestroyEnemy()
    {
        if (!NetworkObject.IsSpawned)
        {
            return;
        }

        //stupid but works 
        SetHpServerRpc();
        DecreaseHpClientRpc(health.Value);
        NetworkObject.Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!NetworkObject.IsSpawned || !NetworkManager.Singleton.IsServer
                                     || !other.gameObject.CompareTag("PlayerBullet"))
        {
            return;
        }

        DecreaseHpServerRpc();
        if (health.Value <= 0)
        {
            DestroyEnemy();
        }
    }

    void Fire()
    {
        if (!NetworkObject.IsSpawned || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            FireServerRpc(i);
        }
    }

    [ServerRpc]
    void FireServerRpc(int i)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject bullet = m_ObjectPool.GetNetworkObject(BulletPrefab).gameObject;
        bullet.transform.position = transform.position;
        bullet.transform.rotation = transform.rotation;
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        rb.AddForce(transform.up * bulletForce, ForceMode2D.Impulse);
        Physics2D.IgnoreCollision(bullet.GetComponent<Collider2D>(), GetComponent<Collider2D>());
        bullet.GetComponent<NetworkObject>().Spawn(true);
        
        GameObject bullet2 = m_ObjectPool.GetNetworkObject(BulletPrefab).gameObject;
        bullet2.transform.position = transform.position;
        bullet2.transform.rotation = transform.rotation;
        Rigidbody2D rb2 = bullet2.GetComponent<Rigidbody2D>();
        rb2.AddForce(transform.up * -bulletForce, ForceMode2D.Impulse);
        Physics2D.IgnoreCollision(bullet2.GetComponent<Collider2D>(), GetComponent<Collider2D>());
        bullet2.GetComponent<NetworkObject>().Spawn(true);
		
		GameObject bullet3 = m_ObjectPool.GetNetworkObject(BulletPrefab).gameObject;
        bullet3.transform.position = transform.position;
        bullet3.transform.rotation = transform.rotation;
        Rigidbody2D rb3 = bullet3.GetComponent<Rigidbody2D>();
        rb3.AddForce(transform.right * bulletForce, ForceMode2D.Impulse);
        Physics2D.IgnoreCollision(bullet3.GetComponent<Collider2D>(), GetComponent<Collider2D>());
        bullet3.GetComponent<NetworkObject>().Spawn(true);

		GameObject bullet4 = m_ObjectPool.GetNetworkObject(BulletPrefab).gameObject;
        bullet4.transform.position = transform.position;
        bullet4.transform.rotation = transform.rotation;
        Rigidbody2D rb4 = bullet4.GetComponent<Rigidbody2D>();
        rb4.AddForce(transform.right * -bulletForce, ForceMode2D.Impulse);
        Physics2D.IgnoreCollision(bullet4.GetComponent<Collider2D>(), GetComponent<Collider2D>());
        bullet4.GetComponent<NetworkObject>().Spawn(true);
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
        int healthBarWidth = 220;
        healthBar.rectTransform.sizeDelta = new Vector2((healthBarWidth * currentHealth) / maxHealth, 10);
        byte maxByteValue = 255;
        byte green = (byte) ((maxByteValue * currentHealth) / maxHealth);
        healthBar.color = new Color32((byte) (maxByteValue - green), green, 0, maxByteValue);
    }

    [ServerRpc]
    void SetHpServerRpc()
    {
        health.Value = maxHealth;
    }

    void ChangeMovement()
    {
        if (!NetworkObject.IsSpawned || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        ChangeMovementServerRpc();
    }

    [ServerRpc]
    void ChangeMovementServerRpc()
    {
        Vector3 v = new Vector3(random.Next((int) -range, (int) range) / range * scale,
            random.Next((int) -range, (int) range) / range * scale, 0);
        v.Normalize();

        rigidbody2D.velocity = transform.TransformDirection(v);
        //rigidbody2D.AddForce(v * 1, ForceMode2D.Impulse);
    }
}