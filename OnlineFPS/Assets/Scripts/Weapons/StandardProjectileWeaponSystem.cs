using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(WeaponPredictionUpdateGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
public partial struct StandardProjectileWeaponSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkTime>();
        state.RequireForUpdate<GameResources>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StandardProjectileWeaponJob job = new StandardProjectileWeaponJob
        {
            IsServer = state.WorldUnmanaged.IsServer(), 
            NetworkTime = SystemAPI.GetSingleton<NetworkTime>(),
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
            PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
            ProjectileShotVisualsLookup = SystemAPI.GetComponentLookup<ProjectileShotVisuals>(true),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct StandardProjectileWeaponJob : IJobEntity
    {
        public bool IsServer;
        public NetworkTime NetworkTime;
        public EntityCommandBuffer ECB;
        [ReadOnly]
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly]
        public ComponentLookup<Parent> ParentLookup;
        [ReadOnly]
        public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;
        [ReadOnly]
        public ComponentLookup<ProjectileShotVisuals> ProjectileShotVisualsLookup;

        void Execute(
            Entity entity,
            ref StandardProjectileWeapon weapon,
            in StandardWeaponFiringMecanism mecanism,
            in WeaponShotSimulationOriginOverride shotSimulationOriginOverride,
            in GhostOwner ghostOwner,
            in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities)
        {
            if (mecanism.ShotsToFire > 0)
            {
                RigidTransform shotSimulationOrigin = WeaponUtilities.GetShotSimulationOrigin(
                    weapon.ShotOrigin,
                    in shotSimulationOriginOverride,
                    ref LocalTransformLookup,
                    ref ParentLookup,
                    ref PostTransformMatrixLookup);
                TransformHelpers.ComputeWorldTransformMatrix(weapon.ShotOrigin, out float4x4 shotVisualsOrigin, ref LocalTransformLookup, ref ParentLookup, ref PostTransformMatrixLookup);

                for (int i = 0; i < mecanism.ShotsToFire; i++)
                {
                    for (int j = 0; j < weapon.ProjectilesCount; j++)
                    {
                        weapon.SpawnIdCounter++;
                        quaternion shotRotationWithSpread = WeaponUtilities.CalculateSpreadRotation(shotSimulationOrigin.rot, weapon.SpreadRadians, ref weapon.Random);

                        if (NetworkTime.IsFirstTimeFullyPredictingTick)
                        {
                            // Projectile spawn
                            Entity spawnedProjectile = ECB.Instantiate(weapon.ProjectilePrefab);
                            ECB.SetComponent(spawnedProjectile, LocalTransform.FromPositionRotation(shotSimulationOrigin.pos, shotRotationWithSpread));
                            ECB.SetComponent(spawnedProjectile, new ProjectileSpawnId { WeaponEntity = entity, SpawnId = weapon.SpawnIdCounter });
                            for (int k = 0; k < ignoredEntities.Length; k++)
                            {
                                ECB.AppendToBuffer(spawnedProjectile, ignoredEntities[k]);
                            }
                            // Set the visual offset
                            {
                                ProjectileShotVisuals projectileVisuals = ProjectileShotVisualsLookup[weapon.ProjectilePrefab];
                                projectileVisuals.VisualOffset = shotVisualsOrigin.Translation() - shotSimulationOrigin.pos;
                                ECB.SetComponent(spawnedProjectile, projectileVisuals);
                            }
                            // Set owner
                            if (IsServer)
                            {
                                ECB.SetComponent(spawnedProjectile, new GhostOwner { NetworkId = ghostOwner.NetworkId });
                            }
                        }
                    }
                }
            }
        }
    }
}