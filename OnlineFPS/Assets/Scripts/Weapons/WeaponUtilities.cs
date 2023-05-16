using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

public static class WeaponUtilities 
{
    public static void AddBasicWeaponBakingComponents<T>(Baker<T> baker) where T : MonoBehaviour
    {
        Entity entity = baker.GetEntity(TransformUsageFlags.Dynamic);
        baker.AddComponent(entity, new WeaponControl());
        baker.AddComponent(entity, new WeaponOwner());
        baker.AddComponent(entity, new WeaponShotSimulationOriginOverride());
        baker.AddBuffer<WeaponShotIgnoredEntity>(entity);
    }

    public static bool GetClosestValidWeaponRaycastHit(
        in NativeList<RaycastHit> hits, 
        in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities,
        out RaycastHit closestValidHit)
    {
        closestValidHit = default;
        closestValidHit.Fraction = float.MaxValue;
        for (int j = 0; j < hits.Length; j++)
        {
            RaycastHit tmpHit = hits[j];

            // Check closest so far
            if (tmpHit.Fraction < closestValidHit.Fraction)
            {
                // Check collidable
                if (PhysicsUtilities.IsCollidable(tmpHit.Material))
                {
                    // Check entity ignore
                    bool entityValid = true;
                    for (int k = 0; k < ignoredEntities.Length; k++)
                    {
                        if (tmpHit.Entity == ignoredEntities[k].Entity)
                        {
                            entityValid = false;
                            break;
                        }
                    }

                    // Final hit
                    if (entityValid)
                    {
                        closestValidHit = tmpHit;
                    }
                }
            }
        }

        return closestValidHit.Entity != Entity.Null;
    }

    public static void ComputeShotDetails(
        ref StandardRaycastWeapon weapon,
        in WeaponShotSimulationOriginOverride shotSimulationOriginOverride,
        in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities,
        ref NativeList<RaycastHit> Hits,
        ref ComponentLookup<LocalTransform> localTransformLookup,
        ref ComponentLookup<Parent> parentLookup,
        ref ComponentLookup<PostTransformMatrix> postTransformMatrixLookup,
        in CollisionWorld CollisionWorld,
        bool computeShotVisuals,
        out bool hitFound,
        out RaycastHit closestValidHit,
        out StandardRaycastWeaponShotVisualsData shotVisualsData)
    {
        hitFound = default;
        closestValidHit = default;
        shotVisualsData = default;
        
        // In a FPS game, it is often desirable for the weapon shot raycast to start from the camera (screen center) rather than from the actual barrel of the weapon mesh.
        // This is because it will precisely match the crosshair at the center of the screen.
        // The shot "Simulation" represents the camera point for the raycast, while the shot "Visual" represents the point where the shot mesh is spawned. 
        Entity shotSimulationOriginEntity = localTransformLookup.HasComponent(shotSimulationOriginOverride.Entity) ? shotSimulationOriginOverride.Entity : weapon.ShotOrigin;
        TransformHelpers.ComputeWorldTransformMatrix(shotSimulationOriginEntity, out float4x4 shotSimulationOriginTransform, ref localTransformLookup, ref parentLookup, ref postTransformMatrixLookup);
        float3 shotSimulationOriginPosition = shotSimulationOriginTransform.Translation();
    
        // Allow firing multiple projectiles per shot
        for (int s = 0; s < weapon.ProjectilesCount; s++)
        {
            // Calculate spread
            quaternion shotSpreadRotation = quaternion.identity;
            if (weapon.SpreadRadians > 0f)
            {
                shotSpreadRotation = math.slerp(weapon.Random.NextQuaternionRotation(), quaternion.identity, (math.PI - math.clamp(weapon.SpreadRadians, 0f, math.PI)) / math.PI);
            }
            float3 finalShotSimulationDirection = math.rotate(shotSpreadRotation, shotSimulationOriginTransform.Forward());
    
            // Hit detection
            Hits.Clear();
            RaycastInput rayInput = new RaycastInput
            {
                Start = shotSimulationOriginPosition,
                End = shotSimulationOriginPosition + (finalShotSimulationDirection * weapon.Range),
                Filter = weapon.HitCollisionFilter,
            };
            CollisionWorld.CastRay(rayInput, ref Hits);
            hitFound = WeaponUtilities.GetClosestValidWeaponRaycastHit(in Hits, in ignoredEntities, out closestValidHit);
    
            // Hit processing
            float hitDistance = weapon.Range;
            if (hitFound)
            {
                hitDistance = closestValidHit.Fraction * weapon.Range;
                hitFound = true;
            }
    
            // Shot visuals
            if(computeShotVisuals)
            {
                shotVisualsData = new StandardRaycastWeaponShotVisualsData
                {
                    VisualOriginEntity = weapon.ShotOrigin,
                    SimulationOrigin = shotSimulationOriginPosition,
                    SimulationDirection = finalShotSimulationDirection,
                    SimulationUp = shotSimulationOriginTransform.Up(),
                    SimulationHitDistance = hitDistance,
                    Hit = closestValidHit,
                };
            }
        }
    }
}
