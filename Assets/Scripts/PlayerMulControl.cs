using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;

public class PlayerMulControl : NetworkBehaviour{
    static string s_ObjectPoolTag = "ObjectPool";

    NetworkObjectPool m_ObjectPool;

    public Vector2 speed=new Vector2(5,5);
    float m_InputX;
    float m_InputY;
    float m_OldInputX;
    float m_OldInputY;
    Vector2 m_Direction;
    Vector2 m_OldDirection=new Vector2(0,0);

    Rigidbody2D m_Rigidbody2D;

    void Awake()
    {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
        m_ObjectPool = GameObject.FindWithTag(s_ObjectPoolTag).GetComponent<NetworkObjectPool>();
        Assert.IsNotNull(m_ObjectPool, $"{nameof(NetworkObjectPool)} not found in scene. Did you apply the {s_ObjectPoolTag} to the GameObject?");
    }
    
    void Start(){
        DontDestroyOnLoad(gameObject);
    }

    void Update(){
        if (IsServer){ UpdateServer();}
        if (IsClient){UpdateClient();}
    }

    void LateUpdate(){
        if (IsLocalPlayer){
            // center camera
            Vector3 pos = transform.position;
            pos.z = -50;
            Camera.main.transform.position = pos;
        }
    }

    void UpdateServer()
    {
        Vector3 movement = new Vector3(speed.x * m_InputX, speed.y * m_InputY, 0);
        movement *= Time.deltaTime;
        m_Rigidbody2D.transform.Translate(movement);

        float angle = Vector2.SignedAngle(Vector2.up, m_Direction);
        m_Rigidbody2D.transform.eulerAngles = new Vector3 (0, 0, angle);

    }

    void UpdateClient(){
        if (!IsLocalPlayer){ return;}

        float inputX = Input.GetAxis("Horizontal");
        float inputY = Input.GetAxis("Vertical");

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mousePosition - m_Rigidbody2D.transform.position;

        if (m_OldDirection != direction || m_OldInputX!=inputX || m_OldInputY!=inputY) {
            UpdateServerRpc(direction, inputX, inputY);
            m_OldDirection = direction;
            m_OldInputX = inputX;
            m_OldInputY = inputY;
        }
        
    }

    // --- ServerRPCs ---

    [ServerRpc]
    public void UpdateServerRpc(Vector2 direction, float inputX, float inputY)
    {
        m_Direction = direction;
        m_InputX=inputX;
        m_InputY=inputY;
    }

}
