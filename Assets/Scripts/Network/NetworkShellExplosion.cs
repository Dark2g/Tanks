using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked shell explosion. Runs exclusively on the server:
/// detects tank collisions, deals damage via NetworkTankHealth,
/// then tells all clients to play the visual/audio effects.
/// </summary>
public class NetworkShellExplosion : NetworkBehaviour
{
    public LayerMask m_TankMask;
    public ParticleSystem m_ExplosionParticles;
    public AudioSource m_ExplosionAudio;
    public float m_MaxDamage = 100f;
    public float m_ExplosionForce = 1000f;
    public float m_MaxLifeTime = 2f;
    public float m_ExplosionRadius = 5f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            Invoke(nameof(DestroyShell), m_MaxLifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        Collider[] colliders = Physics.OverlapSphere(transform.position, m_ExplosionRadius, m_TankMask);

        foreach (Collider col in colliders)
        {
            Rigidbody targetRigidbody = col.GetComponent<Rigidbody>();
            if (targetRigidbody == null)
                continue;

            targetRigidbody.AddExplosionForce(m_ExplosionForce, transform.position, m_ExplosionRadius);

            NetworkTankHealth targetHealth = targetRigidbody.GetComponent<NetworkTankHealth>();
            if (targetHealth == null)
                continue;

            float damage = CalculateDamage(targetRigidbody.position);
            targetHealth.TakeDamage(damage);
        }

        ExplodeClientRpc(transform.position);
        DestroyShell();
    }

    [ClientRpc]
    private void ExplodeClientRpc(Vector3 position)
    {
        if (m_ExplosionParticles != null)
        {
            m_ExplosionParticles.transform.parent = null;
            m_ExplosionParticles.transform.position = position;
            m_ExplosionParticles.Play();

            ParticleSystem.MainModule mainModule = m_ExplosionParticles.main;
            Destroy(m_ExplosionParticles.gameObject, mainModule.duration);
        }

        m_ExplosionAudio?.Play();
    }

    private float CalculateDamage(Vector3 targetPosition)
    {
        float explosionDistance = (targetPosition - transform.position).magnitude;
        float relativeDistance = (m_ExplosionRadius - explosionDistance) / m_ExplosionRadius;
        return Mathf.Max(0f, relativeDistance * m_MaxDamage);
    }

    private void DestroyShell()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }
}
