using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;


namespace OnlineFPS
{
    [GhostComponent]
    [WriteGroup(typeof(LocalToWorld))]
    public struct PrefabProjectile : IComponentData
    {
        public float Speed;
        public float Gravity;
        public float MaxLifetime;
        public float VisualOffsetCorrectionDuration;

        [GhostField] public float3 Velocity;
        [GhostField] public float LifetimeCounter;
        [GhostField] public byte HasHit;
        public Entity HitEntity;
        [GhostField] public float3 VisualOffset;
    }

    public struct RaycastProjectile : IComponentData
    {
        public float Range;
        public float Damage;
    }

    [Serializable]
    public struct RaycastVisualProjectile : IComponentData
    {
        public byte DidHit;
        public float3 StartPoint;
        public float3 EndPoint;
        public float3 HitNormal;

        public float GetLengthOfTrajectory()
        {
            return math.length(EndPoint - StartPoint);
        }

        public float3 GetDirection()
        {
            return math.normalizesafe(EndPoint - StartPoint);
        }
    }

    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct ProjectileSpawnId : IComponentData
    {
        [GhostField] public Entity WeaponEntity;
        [GhostField] public uint SpawnId;

        public bool IsSame(ProjectileSpawnId other)
        {
            return WeaponEntity == other.WeaponEntity && SpawnId == other.SpawnId;
        }
    }
}
