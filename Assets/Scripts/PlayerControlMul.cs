using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;

public class PlayerControlMul : NetworkBehaviour
{
    static string s_ObjectPoolTag = "ObjectPool";

    NetworkObjectPool m_ObjectPool;
    public GameObject BulletPrefab;

    public Image healthBar;
    NetworkVariable<int> health = new NetworkVariable<int>();
    private int maxHealth=25;

    public float speed = 5;

    float m_InputX;
    float m_InputY;
    Vector2 m_Direction;
    float m_OldInputX = 0;
    float m_OldInputY = 0;
    Vector2 m_OldDirection = new Vector2(0, 0);

    private float nextFire = 0;
    public float fireRate = 0.25f;
    public float bulletForce = 8;

    [SerializeField]
    public NetworkVariable<FixedString32Bytes> PlayerName =
        new NetworkVariable<FixedString32Bytes>(new FixedString32Bytes(""));

    Rigidbody2D m_Rigidbody2D;

    void Awake()
    {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
        m_ObjectPool = GameObject.FindWithTag(s_ObjectPoolTag).GetComponent<NetworkObjectPool>();
        Assert.IsNotNull(m_ObjectPool,
            $"{nameof(NetworkObjectPool)} not found in scene. Did you apply the {s_ObjectPoolTag} to the GameObject?");
    }

    void Start()
    {
        DontDestroyOnLoad(gameObject);
        if (IsLocalPlayer)
        {
            SetNameServerRpc($"Player{OwnerClientId}");
            SetHpServerRpc();
        }
    }

    void Update()
    {
        if (IsServer)
        {
            UpdateServer();
        }

        if (IsClient)
        {
            UpdateClient();
        }

        healthBar.transform.rotation = Quaternion.Euler (0, 0, 0);
        healthBar.transform.position = transform.position + new Vector3 (0, 1f,0);
    }

    void LateUpdate()
    {
        //IsLocalPlayer - true if this object is the one that represents the player on the local machine
        if (IsLocalPlayer)
        {
            // center camera on player
            Vector3 pos = transform.position;
            pos.z = -50;
            Camera.main.transform.position = pos;
        }
    }

    void UpdateServer()
    {
        m_Rigidbody2D.velocity=new Vector2(0, 0);

        //movement
        //Time.deltaTime - the interval in seconds from the last frame to the current one
        Vector3 movement = new Vector3(m_InputX, m_InputY, 0);
        movement.Normalize();
        movement *= speed;
        movement *= Time.deltaTime;
        float rotation = m_Rigidbody2D.rotation;
        movement = Quaternion.Euler(0, 0, -rotation) * movement;
        //movement is rotated by the opposite of player orientation to realign with absolute directions 
        m_Rigidbody2D.transform.Translate(movement);

        //rotation
        float angle = Vector2.SignedAngle(Vector2.up, m_Direction);
        m_Rigidbody2D.transform.eulerAngles = new Vector3(0, 0, angle);
    }

    void UpdateClient()
    {
        if (!IsLocalPlayer)
        {
            return;
        }

        //movement
        float inputX = Input.GetAxis("Horizontal");
        float inputY = Input.GetAxis("Vertical");

        //rotation
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mousePosition - m_Rigidbody2D.transform.position;

        //if sth changed
        if (m_OldDirection != direction || m_OldInputX != inputX || m_OldInputY != inputY)
        {
            UpdateServerRpc(direction, inputX, inputY);
            m_OldDirection = direction;
            m_OldInputX = inputX;
            m_OldInputY = inputY;
        }

        // fire
        if (Input.GetMouseButton(0) && Time.time > nextFire)
        {
            nextFire = Time.time + fireRate;
            FireServerRpc();
        }
    }


    // done on server
    [ServerRpc]
    public void UpdateServerRpc(Vector2 direction, float inputX, float inputY)
    {
        m_Direction = direction;
        m_InputX = inputX;
        m_InputY = inputY;
    }

    [ServerRpc]
    public void FireServerRpc()
    {
        GameObject bullet = m_ObjectPool.GetNetworkObject(BulletPrefab).gameObject;
        bullet.transform.position = transform.position;
        bullet.transform.rotation = transform.rotation;
    //    Debug.Log(transform.rotation);
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        rb.AddForce(transform.up * bulletForce, ForceMode2D.Impulse);
        Physics2D.IgnoreCollision(bullet.GetComponent<Collider2D>(), GetComponent<Collider2D>());

        bullet.GetComponent<NetworkObject>().Spawn(true);
    }

    void OnGUI()
    {
        Vector3 pos = Camera.main.WorldToScreenPoint(transform.position);

        GUI.color = Color.black;
        GUI.Label(new Rect(pos.x - 20, Screen.height - pos.y - 30, 400, 30), PlayerName.Value.Value);

        GUI.color = Color.white;

        GUI.Label(new Rect(pos.x - 21, Screen.height - pos.y - 31, 400, 30), PlayerName.Value.Value);
    }

    [ServerRpc]
    public void SetNameServerRpc(string name)
    {
        PlayerName.Value = name;
    }


    void OnTriggerEnter2D(Collider2D other)
    {
        if (!NetworkObject.IsSpawned || !NetworkManager.Singleton.IsServer 
            || !other.gameObject.CompareTag("EnemyBullet")){return; }

        if (health.Value <= 0)
        {
            //TODO
            //GameOver
        }
        else
        {
            DecreaseHpServerRpc();
        }
    }

    [ServerRpc]
    void SetHpServerRpc()
    {
        health.Value = maxHealth;
    }

    [ServerRpc(RequireOwnership = false)]
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


}