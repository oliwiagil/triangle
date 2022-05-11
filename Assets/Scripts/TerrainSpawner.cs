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
    public float wallProbability=0.8f;
    private Random random=null;
    private Random initRandom;
    private short[,] roomMap;
    //(0,0) is one bellow and to the left of bottom-left corner of outer wall,
    //max is roomSize*roomsInRow+1*(roomsInRow+1)+2 = (rooms + walls + outer margin)
    private int mapSize;
    private int mapOffset;
    //for translation from map coordinates to real coordinates
    //cell state
    private const short inactive = 0;
    private const short active = 1;
    private const short emerging = 2;
    private const short fadeing = 3;
    private const short wall = 4;

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
                onStartUp();
                requestSendNewSeedServerRPC();
            }
            refreshObstacles("n");
        }
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            if (seed == -1 && !NetworkManager.Singleton.IsServer)
            {
                onStartUp();
                requestSendNewSeedServerRPC();
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
    private int[,] addMargin(int[,] coordinates,int scale)
    {
        coordinates[0, 0] -= scale;
        coordinates[0, 1] -= scale;
        coordinates[1, 0] += scale;
        coordinates[1, 1] += scale;
        return coordinates;
    }
    
    //x and y are in [0,roomsInRow-1]
    //return is int[2,2] x and y of bottom-left and upper-right corner
    private int[,] getRoomMapCoordinates(int x, int y)
    {
        if (x >= roomsInRow || x < 0 || y >= roomsInRow || y < 0)
        {
            throw new ArithmeticException("room x and y must be within [0,roomsInRow-1]");
        }
        int[,] coordinates = new int[2, 2];
        coordinates[0, 0] = 2 + x * (roomSize + 1);
        coordinates[0, 1] = 2 + y * (roomSize + 1);
        coordinates[1, 0] = 2 + x * (roomSize + 1)+roomSize;
        coordinates[1, 1] = 2 + y * (roomSize + 1)+roomSize;
        return coordinates;
    }

    private float[,] toRealCoordinates(int[,] mapCoordinates)
    {
        float[,] coordinates = new float[2, 2];
        coordinates[0, 0] = mapCoordinates[0, 0] + mapOffset;
        coordinates[0, 1] = mapCoordinates[0, 1] + mapOffset;
        coordinates[1, 0] = mapCoordinates[1, 0] + mapOffset;
        coordinates[1, 1] = mapCoordinates[1, 1] + mapOffset;
        return coordinates;
    }
    //x and y are in [0,roomsInRow-1]
    //return is int[2,2] x and y of bottom-left and upper-right corner
    //including margin of width 1 around the wall
    private int[,] getLeftWallCoordinates(int x, int y,int width=0)
    {
        if (x >= roomsInRow || x < 0 || y >= roomsInRow || y < 0)
        {
            throw new ArithmeticException("room x and y must be within [0,roomsInRow-1]");
        }
        int[,] coordinates = new int[2, 2];
        coordinates[0, 0] = 1+ x * (roomSize + 1)-width;
        coordinates[0, 1] = 1+ y * (roomSize + 1);
        coordinates[1, 0] = 1+ x * (roomSize + 1)+width;
        coordinates[1, 1] = 1+ y * (roomSize + 1)+roomSize;
        return coordinates;
    }
    
    private int[,] getBottomWallCoordinates(int x, int y,int width=0)
    {
        if (x >= roomsInRow || x < 0 || y >= roomsInRow || y < 0)
        {
            throw new ArithmeticException("room x and y must be within [0,roomsInRow-1]");
        }
        int[,] coordinates = new int[2, 2];
        coordinates[0, 0] = 1+ x * (roomSize + 1);
        coordinates[0, 1] = 1+ y * (roomSize + 1)-width;
        coordinates[1, 0] = 1+ x * (roomSize + 1)+roomSize;
        coordinates[1, 1] = 1+ y * (roomSize + 1)+width;
        return coordinates;
    }

    private void addObstacles(int x, int y)
    {
        //translate map to real coordinates and refer to center of the obstacle, not its bottom-left corner
        float xf = x + mapOffset + 0.5f;
        float yf = y + mapOffset + 0.5f;
        //Debug.Log(xf+" "+yf+" --- "+mapOffset+" --- "+x+" "+y);
        obstacles.Add(Instantiate(ObstaclePrefab, new Vector3(xf , yf ), Quaternion.identity));
        roomMap[x, y] = active;
    }

    private void populateArea(int[,] coordinates)
    {
        for (int i = coordinates[0, 0]; i < coordinates[1, 0]; ++i) {
            for (int j = coordinates[0, 1]; j < coordinates[1, 1]; ++j) {
                if (random.NextDouble() <= obstacleProbability)
                {
                    addObstacles(i,j);
                }
            }
        }   
        
    }
    private void setArea(int[,] coordinates,short v)
    { // add translation from map to real coordinates and create alternative "get coordinates"
        
        for (int i = coordinates[0, 0]; i < coordinates[1, 0]; ++i) {
            for (int j = coordinates[0, 1]; j < coordinates[1, 1]; ++j) {
                roomMap[i, j] = v;
            }
        }   
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
        mapOffset = -(roomSize / 2 + (roomSize + 1) * (roomsInRow - 1) / 2 + 2);
        mapSize = roomSize * roomsInRow +  (roomsInRow + 1) + 2;
        roomMap = new short[mapSize,mapSize];
        setArea(new int[,]{{0, 0}, {mapSize, mapSize}},wall);
        setArea(addMargin(new int[,]{{0, 0}, {mapSize, mapSize}},-3),inactive);
        //creates outer wall ring and sets inside as inactive
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
        

    }

    private void buildRoomsWalls()
    {
        float shift = (roomSize + 1);
        float innerWallDist = (roomSize + 1f) / 2f;
        float innerWallLength = (roomSize - doorSize) / 2f + 1f;
        float innerWallOffset = (roomSize - innerWallLength) / 2f + 1f;
        int roomOffset = (roomsInRow - 1) / 2;
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
                if (!(i == 0 && (j == 0 || j == 1)) && j != -(roomsInRow - 1) / 2)
                    //would overlap with inner walls or outer bottom
                {
                    if (random.NextDouble() <= wallProbability)
                    {
                        GameObject wallHL = Instantiate(ObstaclePrefab,
                            new Vector3(innerWallOffset + (i * shift), (-innerWallDist + shift * j)),
                            Quaternion.identity);
                        wallHL.gameObject.transform.localScale = new Vector3(innerWallLength, 1);
                        GameObject wallHR = Instantiate(ObstaclePrefab,
                            new Vector3(-innerWallOffset + (i * shift), (-innerWallDist + shift * j)),
                            Quaternion.identity);
                        wallHR.gameObject.transform.localScale = new Vector3(innerWallLength, 1);
                        obstacles.Add(wallHL);
                        obstacles.Add(wallHR);
                        setArea(addMargin(getBottomWallCoordinates(i+roomOffset, j+roomOffset), 1), wall);
                    }//no need to set are inactive if wall is not created, because this is the default
                    else
                    {
                        populateArea(addMargin(getBottomWallCoordinates(i+roomOffset, j+roomOffset,3),-2));
                        //need to remove margin(2) (margin(1)+wall natural width), but still populate margin(1) in non-overlapping part
                        //hence this abomination
                    }
                }

                //vertical walls
                if (!(j == 0 && (i == 0 || i == 1)) &&
                    i != -(roomsInRow - 1) / 2) //would overlap with inner walls or outer left
                {
                    if (random.NextDouble() <= wallProbability)
                    {
                        GameObject wallVU = Instantiate(ObstaclePrefab,
                            new Vector3((-innerWallDist + shift * i), innerWallOffset + (j * shift)),
                            Quaternion.identity);
                        wallVU.gameObject.transform.localScale = new Vector3(1, innerWallLength);
                        GameObject wallVB = Instantiate(ObstaclePrefab,
                            new Vector3((-innerWallDist + shift * i), -innerWallOffset + (j * shift)),
                            Quaternion.identity);
                        wallVB.gameObject.transform.localScale = new Vector3(1, innerWallLength);
                        obstacles.Add(wallVU);
                        obstacles.Add(wallVB);
                        setArea(addMargin(getLeftWallCoordinates(i + roomOffset, j + roomOffset), 1), wall);
                    }
                    else
                    {
                        populateArea(addMargin(getLeftWallCoordinates(i+roomOffset, j+roomOffset,3),-2));
                    }
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
        setArea(addMargin(new int[,]{{0,0},{mapSize,mapSize}},-3),inactive);
        //margin to not disable outer walls 
    }

    void addObstacles()
    {
        buildRoomsWalls();
        for (int x = 0; x < roomsInRow; ++x) {
            for (int y = 0; y < roomsInRow; ++y) {
                if (x != (roomsInRow - 1) / 2 || y != (roomsInRow - 1) / 2) {
                    populateArea(addMargin(getRoomMapCoordinates(x, y),-1));
                    //remove one margin to leave space between obstacles and room wall
                }
            }
        }
        setArea(addMargin(new int[,]{{2+(roomSize+1)*(roomsInRow-1)/2,2+(roomSize+1)*(roomsInRow-1)/2},
            {2+(roomSize+1)*(roomsInRow-1)/2+roomSize,2+(roomSize+1)*(roomsInRow-1)/2+roomSize}},2),wall);
        //set spawn area (with margin) as a wall  
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