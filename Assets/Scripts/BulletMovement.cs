using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletMovement : MonoBehaviour
{

    void OnCollisionEnter2D(Collision2D collision){
        if (!collision.gameObject.CompareTag("Bullet")){
            Destroy(this.gameObject);
        }
    }

}
