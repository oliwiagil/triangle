using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

public class ObstacleSpawner : NetworkBehaviour{
    public int initialServerSeed;
    private int seed=-1;
    public GameObject ObstaclePrefab;
    public int size;
    private int offset;
    private Random random=null;
    private Random initRandom;

    private List<GameObject> obstacles=new List<GameObject>();
    
    void Awake()
    {   
        offset=-(size-1)/2;
        initRandom = new Random(initialServerSeed);
        Debug.Log("joined"+NetworkManager.Singleton.IsClient+" "+NetworkManager.Singleton.IsServer);
    }

    public void onSeedChange(int newSeed)
    {
        if (newSeed == seed) {return; }
        seed = newSeed;
        random = new Random(seed);
        destroyObstacles();
        addObstacles();
    }

    [ClientRpc]
    void recieveSeedClientRPC(int newSeed)
    {
        onSeedChange(newSeed);
    }
    [ServerRpc(RequireOwnership = false)]
    public void requestSendSeedServerRPC()
    {
        recieveSeedClientRPC(seed);
    }
    [ServerRpc(RequireOwnership = false)]
    public void requestSendNewSeedServerRPC()
    {
            int newSeed = Math.Abs(initRandom.Next());
            onSeedChange(newSeed);
            recieveSeedClientRPC(newSeed);
            //if server is a host it will send to itself, but will ignore, as onSeedChange already processed seed
            //it is a workaround for weird race conditions when client asks for new seed, as server refreshes its obstacles
            //and potentially overwrites new seed with the old one
    }

    void refreshObstacles(string keyBind)
    {
            if (Input.GetKey(keyBind))
            {
                Debug.Log("requsted changing seed"+seed);
                requestSendNewSeedServerRPC();
                Debug.Log("recieved new seed"+seed);
            }
    }

    private void Update()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            if (seed == -1)
            {
                requestSendNewSeedServerRPC();
            }
            refreshObstacles("n");
        }
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            if (seed == -1 && !NetworkManager.Singleton.IsServer)
            {
                Debug.Log("requsted seed"+seed);
                requestSendSeedServerRPC();
                Debug.Log("recieved current seed"+seed);
            }

            refreshObstacles("n");
        }
    }

    void destroyObstacles()
    {
        List<GameObject> toRemove = obstacles;
      foreach (GameObject gameObject in toRemove)
      {
          Destroy(gameObject);
      }

      obstacles = new List<GameObject>();

    }

    void addObstacles()
    {

        for (int x = 0; x < size; ++x)
        {
            for (int y = 0; y < size; ++y)
            {
                if (random.Next()%2==1)
                {
                    obstacles.Add(Instantiate(ObstaclePrefab, new Vector3(x + offset, y + offset, 0), Quaternion.identity));
                }
            }
            
        }

    }
}
