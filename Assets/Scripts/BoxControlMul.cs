using System;
using Unity.Netcode;
using UnityEngine;

public class BoxControlMul : NetworkBehaviour
{

    private int[] colors ={0x228b22, 0xff8c00,0x00ffff,0xff69b4,0xffdab9,0x00ff00};
    public Color getColor(ulong Id)
    {
        int myId = (int) Id;
        Color myColor = new Color();
        myColor.b = (colors[myId % colors.Length] % 0x100) / 255f;
        myColor.g = ((colors[myId % colors.Length] / 0x100) % 0x100) / 255f;
        myColor.r = ((colors[myId % colors.Length] / 0x10000) % 0x100) / 255f;
        myColor.a = 1;
        return myColor;
    }
    [SerializeField]
    public NetworkVariable<ulong> color = new NetworkVariable<ulong>();

    private SpriteRenderer m_Sprite;

    private void OnEnable()
    {
        m_Sprite = GetComponentInParent<SpriteRenderer>();
        color.OnValueChanged += onColorChange;
    }
    void onColorChange(ulong oldCol, ulong newCol)
    {
        setSpriteColor(newCol);
    }
    
    private void setSpriteColor(ulong newCol)
    {
        m_Sprite.color=getColor(newCol);
        m_Sprite.UpdateGIMaterials();
    }
    private void refreshSpriteColor()
    {
        m_Sprite.color = getColor(color.Value);
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
        
        if (other.gameObject.CompareTag("Player") )
        {
            if (other.gameObject.GetComponentInParent<PlayerControlMul>().playerId.Value == color.Value)
            {
                PickBox();
            }
        }
    }
}
