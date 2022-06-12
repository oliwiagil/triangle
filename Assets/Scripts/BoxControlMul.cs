using System;
using Unity.Netcode;
using UnityEngine;

public class BoxControlMul : NetworkBehaviour
{
    [SerializeField]
    public NetworkVariable<Color> color = new NetworkVariable<Color>();

    private SpriteRenderer m_Sprite;

    private void OnEnable()
    {
        m_Sprite = GetComponentInParent<SpriteRenderer>();
        color.OnValueChanged += onColorChange;
    }
    void onColorChange(Color oldCol, Color newCol)
    {
        setSpriteColor(newCol);
    }
    
    private void setSpriteColor(Color newCol)
    {
        m_Sprite.color=newCol;
        m_Sprite.UpdateGIMaterials();
    }
    private void refreshSpriteColor()
    {
        m_Sprite.color = color.Value;
        m_Sprite.UpdateGIMaterials();
    }

    private void Update()
    {
        refreshSpriteColor();
    }

    private void PickBox()
    {
        if (!NetworkObject.IsSpawned){ return;}
        NetworkObject.Despawn();
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (!NetworkManager.Singleton.IsServer || !NetworkObject.IsSpawned){return; }
        
        if (other.gameObject.CompareTag("Player"))
        {
            PickBox();
        }
    }
}
