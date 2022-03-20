using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private GameObject player;
    public GameObject bulletPrefab;
    public float bulletForce;
    public float fireRate;
    public int health;

    private float nextFire = 0;


    void Start(){
        player = GameObject.FindGameObjectWithTag("Player");
        InvokeRepeating("LaunchBullet", fireRate, fireRate);
    }

    void LaunchBullet(){
        GameObject bullet = Instantiate(bulletPrefab, transform.position, transform.rotation);
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        rb.AddForce(transform.up * bulletForce, ForceMode2D.Impulse);
        Physics2D.IgnoreCollision(bullet.GetComponent<Collider2D>(),  GetComponent<Collider2D>());
    }

    void Update(){
        Vector2 direction = player.transform.position - transform.position;
        float angle = Vector2.SignedAngle(Vector2.up, direction);
        transform.eulerAngles = new Vector3 (0, 0, angle);
    }

    public void Hit(int damage){
        health-=damage;
        if(health<=0){
            Destroy(gameObject);
        }
    }
}
