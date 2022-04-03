using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

public class EnemySpawnerMul : NetworkBehaviour{
    private GameObject enemy;
    public float spawnDelay;
    public float spawnTime;
    NetworkManager m_NetworkManager;
    NetworkObjectPool m_ObjectPool;
    public GameObject EnemyPrefab;
    private Random random;
    private float scale = 10f;
    private float range = 256;
    void Awake(){
        random = new Random();
        m_ObjectPool = GameObject.FindWithTag("ObjectPool").GetComponent<NetworkObjectPool>();
        InvokeRepeating ("addEnemyServerRpc", spawnDelay, spawnTime);
        //Assert.IsNotNull(m_ObjectPool, $"{nameof(NetworkObjectPool)} not found in scene. Did you apply the {s_ObjectPoolTag} to the GameObject?");
    }

    [ServerRpc]
    void addEnemyServerRpc()
    {
        GameObject enemy = m_ObjectPool.GetNetworkObject(EnemyPrefab,
            new Vector3(random.Next((int) -range,(int) range)/range*scale,random.Next((int) -range,(int) range)/range*scale,0), new Quaternion(0,0,0,0)).gameObject;
        
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        rb.AddForce(transform.up * 1, ForceMode2D.Impulse);

        enemy.GetComponent<NetworkObject>().Spawn(true);
    }
}
