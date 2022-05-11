using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

public class TerrainSpawner : NetworkBehaviour{
    public int initialServerSeed;
    private int seed=-1;
    public GameObject ObstaclePrefab;
    public int roomSize=25;
    public int doorSize=5;
    public int roomsInRow=5;
    public float obstacleProbability=0.5f;
    private Random random=null;
    private Random initRandom;
    private short[,] roomMap;
    //(0,0) is one bellow and to the left of bottom-left corner of outer wall,
    //max is roomSize*roomsInRow+1*(roomsInRow+1)+2 = (rooms + walls + outer margin)
    private int mapSize;
    private int mapOffset;
    //for translation from map coordinates to real coordinates

    private List<GameObject> obstacles=new List<GameObject>();

    void Awake()
    {   
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
                onStartUp();
            }
            refreshObstacles("n");
        }
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            if (seed == -1 && !NetworkManager.Singleton.IsServer)
            {
                requestSendSeedServerRPC();
                onStartUp();
            }

            refreshObstacles("n");
        }
    }

    private float[,] translateCoordinates(float[,] coordinates,float x,float y)
    {
        coordinates[0, 0] += x;
        coordinates[0, 1] += y;
        coordinates[1, 0] += x;
        coordinates[1, 1] += y;
        return coordinates;
    }
    private float[,] addMargin(float[,] coordinates,float scale)
    {
        coordinates[0, 0] -= scale;
        coordinates[0, 1] -= scale;
        coordinates[1, 0] += scale;
        coordinates[1, 1] += scale;
        return coordinates;
    }
    
    //x and y are in [0,roomsInRow-1]
    //return is int[2,2] x and y of bottom-left and upper-right corner
    private float[,] getRoomCoordinates(int x, int y)
    {
        if (x >= roomsInRow || x < 0 || y >= roomsInRow || y < 0)
        {
            throw new ArithmeticException("room x and y must be within [0,roomsInRow-1]");
        }
        float[,] coordinates = new float[2, 2];
        x -= (roomsInRow - 1) / 2;
        y -= (roomsInRow - 1) / 2;
        coordinates[0, 0] = -roomSize / 2 + x * (roomSize + 1);
        coordinates[0, 1] = -roomSize / 2 + y * (roomSize + 1);
        coordinates[1, 0] = +roomSize / 2 + x * (roomSize + 1);
        coordinates[1, 1] = +roomSize / 2 + y * (roomSize + 1);
        return translateCoordinates(coordinates,0.5f,0.5f);//to accomodate offset of the center point of the obstacle
    }
    //x and y are in [0,roomsInRow-1]
    //return is int[2,2] x and y of bottom-left and upper-right corner
    //including margin of width 1 around the wall
    private float[,] getLeftWallCoordinates(int x, int y)
    {
        if (x >= roomsInRow || x < 0 || y >= roomsInRow || y < 0)
        {
            throw new ArithmeticException("room x and y must be within [0,roomsInRow-1]");
        }
        float[,] coordinates = new float[2, 2];
        x -= (roomsInRow - 1) / 2;
        y -= (roomsInRow - 1) / 2;
        coordinates[0, 0] = -roomSize / 2 + x * (roomSize + 1)-1;
        coordinates[0, 1] = -roomSize / 2 + y * (roomSize + 1)-1;
        coordinates[1, 0] = -roomSize / 2 + x * (roomSize + 1)+1;
        coordinates[1, 1] = +roomSize / 2 + y * (roomSize + 1)+1;
        return translateCoordinates(coordinates,0.5f,0.5f);//to accomodate offset of the center point of the obstacle
    }
    private float[,] getBottomWallCoordinates(int x, int y)
    {
        if (x >= roomsInRow || x < 0 || y >= roomsInRow || y < 0)
        {
            throw new ArithmeticException("room x and y must be within [0,roomsInRow-1]");
        }
        float[,] coordinates = new float[2, 2];
        x -= (roomsInRow - 1) / 2;
        y -= (roomsInRow - 1) / 2;
        coordinates[0, 0] = -roomSize / 2 + x * (roomSize + 1)-1;
        coordinates[0, 1] = -roomSize / 2 + y * (roomSize + 1)-1;
        coordinates[1, 0] = +roomSize / 2 + x * (roomSize + 1)+1;
        coordinates[1, 1] = -roomSize / 2 + y * (roomSize + 1)+1;
        return translateCoordinates(coordinates,0.5f,0.5f);//to accomodate offset of the center point of the obstacle
    }

    private void populateArea(float[,] coordinates)
    {
        Debug.Log(coordinates[0,0]+" "+coordinates[0,1]+" : "+coordinates[1,0]+" "+coordinates[1,1]+" ");
        for (float i = coordinates[0, 0]; i < coordinates[1, 0]; ++i) {
            for (float j = coordinates[0, 1]; j < coordinates[1, 1]; ++j) {
                if (random.NextDouble() <= obstacleProbability)
                {
                    obstacles.Add(Instantiate(ObstaclePrefab, new Vector3(i , j ), Quaternion.identity));
                }
            }
        }   
        
    }
    private void setArea(int[,] coordinates,short v)
    { // add translation from map to real coordinates and create alternative "get coordinates"
        
      //for (int i = coordinates[0, 0]; i < coordinates[1, 0]; ++i) {
      //    for (int j = coordinates[0, 1]; j < coordinates[1, 1]; ++j) {
      //        roomMap[i, j] = v;
      //    }
      //}   
    }

    private void onStartUp()
    {
        if (doorSize >= roomSize)
        {
            throw new ArithmeticException("doorSize MUST be lesser than roomSize");
        }
        if (roomsInRow <= 1 || roomsInRow%2==0)
        {
            throw new ArithmeticException("roomsInRow MUST be an odd integer greater than 2");
        }
        if (obstacleProbability > 1 || obstacleProbability<0)
        {
            throw new ArithmeticException("obstacleProbability needs to be within range [0:1]");
        }

        mapSize = roomSize * roomsInRow +  (roomsInRow + 1) + 2;
        roomMap = new short[mapSize,mapSize];
        mapOffset = -(roomSize / 2 + (roomSize + 1) * (roomsInRow - 1) / 2 + 2);
        setArea(new int[,]{{0, 0}, {mapSize, mapSize}},0);
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
        

        buildRoomsWalls();
    }

    private void buildRoomsWalls()
    {
        float shift = (roomSize + 1);
        float innerWallDist = (roomSize + 1f) / 2f;
        float innerWallLength = (roomSize - doorSize) / 2f + 1f;
        float innerWallOffset = (roomSize - innerWallLength) / 2f + 1f;

        for (int i = -(roomsInRow - 1) / 2; i <= (roomsInRow - 1) / 2; ++i)
        {
            //generates bottom and left wall for room (i,j)
            for (int j = -(roomsInRow - 1) / 2; j <= (roomsInRow - 1) / 2; ++j) 
            {
                /*TODO
                 here you can omit generating left/bottom wall,
                 and cover it (and margins on both sides) with obstacle for cellular automata
                 
                 populating room with obstacle will be done elsewhere
                */

                //horizontal walls
                if (!(i == 0 && (j == 0 || j == 1)) &&
                    j != -(roomsInRow - 1) / 2) //would overlap with inner walls or outer bottom
                {
                    GameObject wallHL = Instantiate(ObstaclePrefab,
                        new Vector3(innerWallOffset + (i * shift), (-innerWallDist + shift * j)), Quaternion.identity);
                    wallHL.gameObject.transform.localScale = new Vector3(innerWallLength, 1);
                    GameObject wallHR = Instantiate(ObstaclePrefab,
                        new Vector3(-innerWallOffset + (i * shift), (-innerWallDist + shift * j)), Quaternion.identity);
                    wallHR.gameObject.transform.localScale = new Vector3(innerWallLength, 1);
                }

                //vertical walls
                if (!(j == 0 && (i == 0 || i == 1)) &&
                    i != -(roomsInRow - 1) / 2) //would overlap with inner walls or outer left
                {
                    GameObject wallVU = Instantiate(ObstaclePrefab,
                        new Vector3((-innerWallDist + shift * i), innerWallOffset + (j * shift)), Quaternion.identity);
                    wallVU.gameObject.transform.localScale = new Vector3(1, innerWallLength);
                    GameObject wallVB = Instantiate(ObstaclePrefab,
                        new Vector3((-innerWallDist + shift * i), -innerWallOffset + (j * shift)), Quaternion.identity);
                    wallVB.gameObject.transform.localScale = new Vector3(1, innerWallLength);
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
        for (int x = 0; x < roomsInRow; ++x) {
            for (int y = 0; y < roomsInRow; ++y) {
                if (x != (roomsInRow - 1) / 2 || y != (roomsInRow - 1) / 2) {
                    populateArea(addMargin(getRoomCoordinates(x, y), -1));
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
- hud with player name + health bar + some stats?<-- integrate with player name (and class or some) (corner of the screen)
- enemy "patrols" room until player enters room, than does something
<-- TODO
- health bars for everyone  (under objects)

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