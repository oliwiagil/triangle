using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

public class TerrainSpawner : NetworkBehaviour{
    private GameObject enemy;
    public float spawnDelay;
    public float spawnTime;
    NetworkObjectPool m_ObjectPool;
    public GameObject EnemyPrefab;
    private float scale = 10f;
    private float range = 256;
    public int initialServerSeed=7312;
    private int seed=-1;
    public GameObject ObstaclePrefab;
    public int roomSize=21;
    public int doorSize=5;
    public int roomsInRow=7;
    public float obstacleProbability=0.57f;
    public float wallProbability=0.75f;
    private Random random=null;
    private Random initRandom;
    private short[,] roomMap;
    private static readonly int[,] cross = new int[,] {{0, 1}, {0, -1}, {1, 0}, {-1, 0}};
    private int testing = 0;
    //(0,0) is one bellow and to the left of bottom-left corner of outer wall,
    //max is roomSize*roomsInRow+1*(roomsInRow+1)+2 = (rooms + walls + outer margin)
    private int mapSize;
    private int mapOffset;

    //for translation from map coordinates to real coordinates
    //cell state
    private const short inactive = 0;
    private const short active = 1;
    private const short emerging = 2;
    private const short wall = 3;

    private List<GameObject> obstacles=new List<GameObject>();

    void Awake()
    {   
        initRandom = new Random(initialServerSeed);
        m_ObjectPool = GameObject.FindWithTag("ObjectPool").GetComponent<NetworkObjectPool>();
    }
    void addEnemyServer()
    {
        if (!NetworkManager.Singleton.IsServer){ return; }

        Random localRand = new Random();
        int x = localRand.Next(0, (int) mapSize);
        int y = localRand.Next(0, (int) mapSize);
        while (roomMap[x, y] == active || roomMap[x, y] == wall)
        {
            x = localRand.Next(0, (int) mapSize);
            y = localRand.Next(0, (int) mapSize);
        }
        GameObject enemy = m_ObjectPool.GetNetworkObject(EnemyPrefab,
            new Vector3(x + mapOffset,y + mapOffset ,0), new Quaternion(0,0,0,0)).gameObject;
        
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        Vector3 v = new Vector3(localRand.Next((int) -range, (int) range) / range * scale,
            localRand.Next((int) -range, (int) range) / range * scale, 0);
        v.Normalize();
        rb.AddForce(v * 1, ForceMode2D.Impulse);
    
        enemy.GetComponent<NetworkObject>().Spawn(true);
    }
    public void onSeedChange(int newSeed)
    {
        //Debug.Log("new seed!!!");
        if (newSeed == seed) {return; }
        seed = newSeed;
        random = new Random(seed);
        destroyObstacles();
        addObstacles();
    }

    [ClientRpc]
    void recieveSeedClientRPC(int newSeed)
    {
        //Debug.Log("Server called");
        onSeedChange(newSeed);
    }
    [ServerRpc(RequireOwnership = false)]
    public void requestSendSeedServerRPC()
    {
        //Debug.Log("player asked");
        recieveSeedClientRPC(seed);
    }
    [ServerRpc(RequireOwnership = false)]
    public void requestSendNewSeedServerRPC()
    {
    
        //if (!NetworkManager.Singleton.IsServer){ return; }
        int newSeed = Math.Abs(initRandom.Next());
        onSeedChange(newSeed);
        recieveSeedClientRPC(newSeed);
        movePlayersToSpawnRoom();
        //Debug.Log("player called");
        //if server is a host it will send to itself, but will ignore, as onSeedChange already processed seed
        //it is a workaround for weird race conditions when client asks for new seed, as server refreshes its obstacles
        //and potentially overwrites new seed with the old one
    }

    private void movePlayersToSpawnRoom()
    {
        if (!NetworkManager.Singleton.IsServer){ return; }
        Random localRand = new Random();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        int x;
        int y;
        foreach (GameObject target in players)
        {
            x = localRand.Next((int) -roomSize / 2, (int) roomSize / 2);
            y = localRand.Next((int) -roomSize / 2, (int) roomSize / 2);
            target.transform.position = new Vector3(x, y, 0);
        }
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
                requestSendSeedServerRPC();
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
        coordinates[1, 0] = 2+ x * (roomSize + 1)+width;
        coordinates[1, 1] = 3+ y * (roomSize + 1)+roomSize;
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
        coordinates[1, 0] = 3+ x * (roomSize + 1)+roomSize;
        coordinates[1, 1] = 2+ y * (roomSize + 1)+width;
        return coordinates;
    }

    private void addObstacles(int x, int y)
    {
        //translate map to real coordinates and refer to center of the obstacle, not its bottom-left corner
        float xf = x + mapOffset;
        float yf = y + mapOffset;
        GameObject obstacle = Instantiate(ObstaclePrefab, new Vector3(xf, yf), Quaternion.identity);
        obstacle.gameObject.transform.localScale = new Vector3(1, 1);
        obstacles.Add(obstacle);
    }

    private void populateArea(int[,] coordinates,int mode=0)
    {
        for (int i = coordinates[0, 0]; i < coordinates[1, 0]; ++i) {
            for (int j = coordinates[0, 1]; j < coordinates[1, 1]; ++j)
            {
                if (roomMap[i, j] == inactive)
                {
                    if (random.NextDouble() <= obstacleProbability || mode != 0)
                    {
                        roomMap[i, j] = active;
                    }
                }
            }
        }
    }
    private void buildObstacles(int[,] coordinates)
    {
        double count = 0;
        double all = 0;
        for (int i = coordinates[0, 0]; i < coordinates[1, 0]; ++i) {
            for (int j = coordinates[0, 1]; j < coordinates[1, 1]; ++j)
            {
                if (roomMap[i, j] == active)
                {
                    addObstacles(i,j);
                    count += 1;
                }

                all += 1;
            }
        }
        //Debug.Log("Filled: "+(100*count/all)+"%");
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
        if (roomSize <3 || roomSize % 2 == 0)
        {
            throw new ArithmeticException("roomSize needs to be an odd integer greater than 2");
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
        
        InvokeRepeating ("addEnemyServer", spawnDelay, spawnTime);
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
                    }//no need to set area inactive if wall is not created, that is the default
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

    int[,] wholeMap()
    {
        return new int[,] {{0, 0}, {mapSize, mapSize}};
    }
    void addObstacles()
    {
        int[,] area = addMargin(wholeMap(), -3);
        buildRoomsWalls();
        setArea(addMargin(getRoomMapCoordinates((roomsInRow-1)/2,(roomsInRow-1)/2) ,2),wall);
        //set spawn area (with margin) as a wall  
        populateArea(area);

        while(automataStep(area)){};
        heuristic(area);
        while(automataStep(area)){};
        testing = 0;
        dfs(wholeMap());
        buildObstacles(area);
    }

    void printMap(int[,] coordinates)
    {
        string outVar = "";
        for (int x = coordinates[0, 0]; x < coordinates[1, 0]; ++x) {
            for (int y = coordinates[0, 1]; y < coordinates[1, 1]; ++y)
            {
                outVar += roomMap[x, y] + " ";
            }

            outVar += "\n";
        }
        //Debug.Log(outVar);
    }

    bool within(int x, int y,int[,] coordinates)
    {
        return !(x < coordinates[0,0] || x >= coordinates[1,0] || y < coordinates[0,1] || y >= coordinates[1,1]);
    }
    void dfs(int x, int y, int[,] coordinates,bool[,] visited)
    {
        visited[x, y] = true;
        for (int i = 0; i < 4; ++i)
        {
            if (within(x + cross[i, 0], y + cross[i, 1], coordinates))
            {
                if (!visited[x + cross[i, 0], y + cross[i, 1]] &&
                    (roomMap[x + cross[i, 0], y + cross[i, 1]] == inactive ||
                     roomMap[x + cross[i, 0], y + cross[i, 1]] == wall))
                {
                    dfs(x + cross[i, 0], y + cross[i, 1], coordinates, visited);
                }
            }
        }
    }
    void dfs(int[,] coordinates)
    {
        bool[,] visited = new bool[mapSize, mapSize]; //false by default
        dfs(0,0,coordinates,visited);//(0,0) ALWAYS a wall 
        for(int i=0;i<mapSize;++i)
            for(int j=0;j<mapSize;++j)
                if (!visited[i, j] && roomMap[i,j]==inactive)
                {
                    roomMap[i, j] = active;
                }
    }

    void heuristic(int[,] coordinates)
    {
        int count = 0;
        short[,] tmp = roomMap.Clone() as short[,];
        for (int x = coordinates[0, 0]; x < coordinates[1, 0]; ++x)
        {
            for (int y = coordinates[0, 1]; y < coordinates[1, 1]; ++y)
            {
                if (roomMap[x, y] == active)
                {
                    count = 0;
                    for (int i = 0; i < 4; ++i)
                    {
                        for (int j = i + 1; j < 4; ++j)
                        {
                            if (roomMap[x + cross[i, 0], y + cross[i, 1]] == active &&
                                roomMap[x + cross[j, 0], y + cross[j, 1]] == 1)
                            {
                                count += 1;
                            }
                        }
                    }
                    if (count<2)
                    {
                        tmp[x, y] = inactive;
                    }
                }
            }
        }
        roomMap = tmp.Clone() as short[,];
    }

    bool automataStep(int[,] coordinates)
    {
        //do not ask how it works, because it objectively shouldn't
        //but it is a heuristic so it doesn't care about my opinion
        short[,] tmp = roomMap.Clone() as short[,];
        for (int x = coordinates[0, 0]; x < coordinates[1, 0]; ++x) {
            for (int y = coordinates[0, 1]; y < coordinates[1, 1]; ++y)
            {
                if (tmp[x, y] == active || tmp[x, y] == inactive)
                {
                    int count = 0;
                    for (int i = 0; i < 4; ++i)
                    {
                        if (tmp[x + cross[i, 0], y + cross[i, 1]] == 1) count++;
                    }

                    if (tmp[x, y] == active && count < 2)
                    {
                        tmp[x, y] = inactive;
                    }
                    else if(tmp[x,y]==inactive && count>=2)
                    {
                        tmp[x, y] = emerging;
                    }
                }

            }
        }
        for (int x = coordinates[0, 0]; x < coordinates[1, 0]; ++x) {
            for (int y = coordinates[0, 1]; y < coordinates[1, 1]; ++y)
            {
                if (tmp[x, y] == emerging)
                {
                    int count = 0;
                    for (int i = 0; i < 4; ++i)
                    {
                        if (tmp[x + cross[i, 0], y + cross[i, 1]] == 1) count++;
                    }

                    if (count > 2)
                    {
                        tmp[x, y] = active;
                    }
                    else
                    {
                        tmp[x, y] = inactive;
                    }
                }

            }
        }
        bool outVar=false;
        for (int x = coordinates[0, 0]; x < coordinates[1, 0] && !outVar; ++x)
        {
            for (int y = coordinates[0, 1]; y < coordinates[1, 1] && !outVar; ++y)
            {
                if (tmp[x, y] != roomMap[x, y])
                {
                    outVar = true;
                }
            }
        }

        roomMap = tmp.Clone() as short[,];
        return outVar;
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
-- spawn enemies in room with a player (or adjacent)
    TODO --> add objectives
- k collectibles (up to 1 per room) for k players to be returned to starting room (in outer most ring of rooms) 
- each player collects ONE
- scaling difficulty?
- power-ups on advancing a level
<-- TODO
- hud with player name + health bar + some stats?<-- integrate with player name (and class or some) (corner of the screen)
- enemy "patrols" room until player enters room, than does something
<-- TODO
- health bars for everyone  (under objects)

<-- TODO
- optimize obstacles CUDA/multi-threading + merge obstacles if possible

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