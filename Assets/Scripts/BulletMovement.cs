using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletMovement : MonoBehaviour
{
    public int damage;
    public GameObject gameOverText;

    void OnTriggerEnter2D(Collider2D other){
        if(other.gameObject.CompareTag("Enemy")){
            other.gameObject.GetComponent<Enemy>().Hit(damage);
        }
        if(other.gameObject.CompareTag("Player")){
            other.gameObject.GetComponent<PlayerControl>().Hit(damage);
        }
        if (!other.gameObject.CompareTag("Bullet")) {
            Destroy(gameObject);
        }
    }

}
