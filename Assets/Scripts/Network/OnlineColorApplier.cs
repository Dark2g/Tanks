using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Applies tank color on all clients after the tank NetworkObject is spawned.
/// Kept as a separate component to keep OnlineGameManager clean.
/// </summary>
public class OnlineColorApplier : NetworkBehaviour
{
    [ClientRpc]
    public void ApplyColorClientRpc(float r, float g, float b)
    {
        Color color = new Color(r, g, b);

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
            renderer.material.color = color;
    }
}
