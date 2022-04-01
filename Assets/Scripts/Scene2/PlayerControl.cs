using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControl : MonoBehaviour
{
    public Vector2 speed;
    public GameObject bulletPrefab;
    public float bulletForce;
    public float fireRate;
    public int health;

    private float nextFire = 0;
    private bool alive=true;

    void Update()
    {
        if(alive){
            //movement
            float inputX = Input.GetAxis("Horizontal");
            float inputY = Input.GetAxis("Vertical");
            Vector3 movement = new Vector3(speed.x * inputX, speed.y * inputY, 0);
            movement *= Time.deltaTime;
            transform.Translate(movement);

            //rotation
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = mousePosition - transform.position;
            float angle = Vector2.SignedAngle(Vector2.up, direction);
            transform.eulerAngles = new Vector3 (0, 0, angle);

            //shooting
            if (Input.GetMouseButton(0) && Time.time > nextFire){
                nextFire = Time.time + fireRate;
                GameObject bullet = Instantiate(bulletPrefab, transform.position, transform.rotation);
                Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                rb.AddForce(transform.up * bulletForce, ForceMode2D.Impulse);
                Physics2D.IgnoreCollision(bullet.GetComponent<Collider2D>(),  GetComponent<Collider2D>());
                GetComponent<AudioSource>().Play();
            }
        }
    }


public void Hit(int damage){
    health-=damage;
    if(health<=0){
        Time.timeScale=0f;
        alive=false;
    }
}

}
