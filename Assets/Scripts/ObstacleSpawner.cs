using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

public class ObstacleSpawner : NetworkBehaviour{
    public int initialSeed;
    private int seed=-1;
    public GameObject ObstaclePrefab;
    public int size;
    private int offset;
    private double time = -1;
    private bool connected = false;
    public int waitTime;
    private Random random=null;
    private Random initRandom;

    private List<GameObject> obstacles=new List<GameObject>();
    
    void Awake()
    {   
        offset=-(size-1)/2;
        initRandom=new Random(initialSeed);
    }

    [ClientRpc]
    public void onSeedChangeClientRPC(int newSeed)
    {
        seed = newSeed;
        random = new Random(seed);
        destroyObstacles();
        addObstacles();
    }

    private void Update()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            if (time == -1)
            {
                time = NetworkManager.Singleton.ServerTime.Time;
                seed=Math.Abs(initRandom.Next());
            }
            else
            {
                if (time + waitTime < NetworkManager.Singleton.ServerTime.Time)
                {
                    time = NetworkManager.Singleton.ServerTime.Time;
                    seed=Math.Abs(initRandom.Next());
                    onSeedChangeClientRPC(seed);
                }
            }
            
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
