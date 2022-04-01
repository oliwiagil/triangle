using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(NetworkManager))]
public class RandomPositionPlayerSpawner: MonoBehaviour
{
    NetworkManager m_NetworkManager;

    int m_RoundRobinIndex = 0;
    
    [SerializeField]
    List<Vector3> m_SpawnPositions = new List<Vector3>() { Vector3.zero };

    public Vector3 GetNextSpawnPosition(){
        m_RoundRobinIndex = (m_RoundRobinIndex + 1) % m_SpawnPositions.Count;
        return m_SpawnPositions[m_RoundRobinIndex];
    }
    
    private void Awake()
    {
        var networkManager = gameObject.GetComponent<NetworkManager>();
        networkManager.ConnectionApprovalCallback += ConnectionApprovalWithRandomSpawnPos;
    }

    void ConnectionApprovalWithRandomSpawnPos(byte[] payload, ulong clientId, NetworkManager.ConnectionApprovedDelegate callback)
    {
        //https://docs-multiplayer.unity3d.com/docs/getting-started/connection-approval/index.html
        //callback(createPlayerObject, prefabHash, approve, positionToSpawnAt, rotationToSpawnWith);
        callback(true, null, true, GetNextSpawnPosition(), Quaternion.identity);
    }
    
}

