using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner2 : MonoBehaviour{
    public Transform[] spawnPoints;
    public GameObject enemy;
    public float spawnDelay; 
    public float spawnTime;

    void Start(){
        InvokeRepeating ("addEnemy", spawnDelay, spawnTime);
    }

    void addEnemy() {
        int index = Random.Range(0, spawnPoints.Length);
        Instantiate (enemy, spawnPoints[index].position, spawnPoints[index].rotation);
    }

}
