using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

public class ObstacleSpawner : NetworkBehaviour{
    public int seed;
    public GameObject ObstaclePrefab;
    public int size;
    private int offset;
    private double time = -1;
    public int waitTime;
    private Random random;

    private List<GameObject> obstacles=new List<GameObject>();

    void Awake()
    {   
        offset=-(size-1)/2;
        random=new Random(seed);
        addObstacles();
    }

    private void Update()
    {
        if(!NetworkManager.Singleton.IsServer){return;}

        if (time == -1)
        {
            time = NetworkManager.Singleton.ServerTime.Time;
        }
        else
        {
            if (time + waitTime < NetworkManager.Singleton.ServerTime.Time)
            {
                destroyObstacles();
                time = NetworkManager.Singleton.ServerTime.Time;
                addObstacles();
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
