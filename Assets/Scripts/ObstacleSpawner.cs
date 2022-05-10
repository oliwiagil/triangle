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
    public int roomSize;
    public int doorSize;
    public int roomsInRow;
    private Random random=null;
    private Random initRandom;

    private List<GameObject> obstacles=new List<GameObject>();

    void Awake()
    {   
        offset=-(size-1)/2;
        initRandom = new Random(initialServerSeed);
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
                requestSendNewSeedServerRPC();
                
            }
    }

    private void Update()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            if (seed == -1)
            {
                requestSendNewSeedServerRPC();
                onStatup();
            }
            refreshObstacles("n");
        }
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            if (seed == -1 && !NetworkManager.Singleton.IsServer)
            {
                requestSendSeedServerRPC();
                onStatup();
            }

            refreshObstacles("n");
        }
    }

    private void onStatup()
    {
        if (doorSize >= roomSize)
        {
            throw new ArithmeticException("doorSize MUST be lesser than roomSize");
        }
        if (roomsInRow <= 1 || roomsInRow%2==0)
        {
            throw new ArithmeticException("roomsInRow MUST be an odd integer greater than 2");
        }
        float innerWallDist = (roomSize + 1f) / 2f;
        float innerWallLength = (roomSize - doorSize) / 2f + 1f;
        float innerWallOffset = (roomSize - innerWallLength) / 2f + 1f;
        //Left inner wall
        GameObject innerWallLU = Instantiate(ObstaclePrefab, new Vector3(-innerWallDist, innerWallOffset), Quaternion.identity);
        innerWallLU.gameObject.transform.localScale = new Vector3(1, innerWallLength);
        GameObject innerWallLB = Instantiate(ObstaclePrefab, new Vector3(-innerWallDist, -innerWallOffset), Quaternion.identity);
        innerWallLB.gameObject.transform.localScale = new Vector3(1, innerWallLength);
        //Right inner wall
        GameObject innerWallRU = Instantiate(ObstaclePrefab, new Vector3(innerWallDist, innerWallOffset), Quaternion.identity);
        innerWallRU.gameObject.transform.localScale = new Vector3(1, innerWallLength);
        GameObject innerWallRB = Instantiate(ObstaclePrefab, new Vector3(innerWallDist, -innerWallOffset), Quaternion.identity);
        innerWallRB.gameObject.transform.localScale = new Vector3(1, innerWallLength);
        //Upper inner wall
        GameObject innerWallUR = Instantiate(ObstaclePrefab, new Vector3(innerWallOffset, innerWallDist), Quaternion.identity);
        innerWallUR.gameObject.transform.localScale = new Vector3(innerWallLength, 1);
        GameObject innerWallUL = Instantiate(ObstaclePrefab, new Vector3(-innerWallOffset, innerWallDist), Quaternion.identity);
        innerWallUL.gameObject.transform.localScale = new Vector3( innerWallLength, 1);
        //Bottom inner wall
        GameObject innerWallBR = Instantiate(ObstaclePrefab, new Vector3(innerWallOffset, -innerWallDist), Quaternion.identity);
        innerWallBR.gameObject.transform.localScale = new Vector3(innerWallLength, 1);
        GameObject innerWallBL = Instantiate(ObstaclePrefab, new Vector3(-innerWallOffset, -innerWallDist), Quaternion.identity);
        innerWallBL.gameObject.transform.localScale = new Vector3( innerWallLength, 1);

        float outerWallDist = (roomSize + 1f) * roomsInRow / 2f;
        float outerWallLength = (roomSize + 1) * roomsInRow * 1f+1f;
        //Left outer wall
        GameObject outerWallL = Instantiate(ObstaclePrefab, new Vector3(-outerWallDist, 0f), Quaternion.identity);
        outerWallL.gameObject.transform.localScale = new Vector3(1, outerWallLength);
        //Right outer wall
        GameObject outerWallR = Instantiate(ObstaclePrefab, new Vector3(outerWallDist, 0f), Quaternion.identity);
        outerWallR.gameObject.transform.localScale = new Vector3(1, outerWallLength);
        //Upper outer wall
        GameObject outerWallU = Instantiate(ObstaclePrefab, new Vector3(0f,outerWallDist), Quaternion.identity);
        outerWallU.gameObject.transform.localScale = new Vector3( outerWallLength,1);
        //Bottom outer wall
        GameObject outerWallB = Instantiate(ObstaclePrefab, new Vector3(0f,-outerWallDist), Quaternion.identity);
        outerWallB.gameObject.transform.localScale = new Vector3( outerWallLength,1);
        float shift = (roomSize + 1);
        for (int i = -(roomsInRow - 1) / 2; i <= (roomsInRow - 1) / 2; ++i)
        {
            for (int j = -(roomsInRow - 1) / 2; j <= (roomsInRow - 1) / 2; ++j)//generates upper and right wall for room (i,j)
            {
                //horizontal walls
                if (!(i == 0 && (j == 0 || j == -1)) && j != (roomsInRow-1)/2)//would overlap with inner walls or outer upper
                {
                    GameObject wallHL = Instantiate(ObstaclePrefab,
                        new Vector3(innerWallOffset + (i * shift), (innerWallDist + shift * j)), Quaternion.identity);
                    wallHL.gameObject.transform.localScale = new Vector3(innerWallLength, 1);
                    GameObject wallHR = Instantiate(ObstaclePrefab,
                        new Vector3(-innerWallOffset + (i * shift), (innerWallDist + shift * j)), Quaternion.identity);
                    wallHR.gameObject.transform.localScale = new Vector3(innerWallLength, 1);
                }
                //vertical walls
                if (!(j == 0 && (i == 0 || i == -1)) && i != (roomsInRow-1)/2)//would overlap with inner walls or outer right
                {
                    GameObject wallVU = Instantiate(ObstaclePrefab,
                        new Vector3( (innerWallDist + shift * i),innerWallOffset + (j * shift)), Quaternion.identity);
                    wallVU.gameObject.transform.localScale = new Vector3( 1,innerWallLength);
                    GameObject wallVB = Instantiate(ObstaclePrefab,
                        new Vector3((innerWallDist + shift * i),-innerWallOffset + (j * shift) ), Quaternion.identity);
                    wallVB.gameObject.transform.localScale = new Vector3( 1,innerWallLength);
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
/*
 n*m (nieparzyste)
-------------------------
|   ||   ||   ||   ||   |
-------------------------
|   ||   ||   ||   ||   |
-------------------------
|   ||   |Start|   ||   |
-------------------------
|   ||   ||   ||   ||   |
-------------------------
|   ||   ||   ||   ||   |
-------------------------
<-- TODO
- randomly generated rooms (mergeable) (from random with some automata/algorithm) 
- k collectibles (up to 1 per room) for k players to be returned to starting room
- each player collects ONE
- scaling difficulty
<-- TODO
- minimap
<-- TODO
- hud with player name + healtbar + some stats?<-- integrate with player name (and class or some) (corner of the screen)
- enemy "patrols" room until player enters room, than does something
<-- TODO
- healthbars for everyone  (under objects)

-----------------------
|     ...|            |
|---------            |
|                     |
|                     |
|                     |
|            |--------|
|            | minimap|
|            |        |
|            |        |
-----------------------
*/