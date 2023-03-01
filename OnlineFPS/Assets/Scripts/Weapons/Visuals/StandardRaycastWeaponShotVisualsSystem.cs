using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(WeaponShotVisualsGroup))]
[UpdateBefore(typeof(WeaponShotVisualsSpawnECBSystem))]
public partial struct StandardRaycastWeaponShotVisualsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StandardRaycastWeaponShotVisualsJob visualsJob = new StandardRaycastWeaponShotVisualsJob
        {
            ECB = SystemAPI.GetSingletonRW<WeaponShotVisualsSpawnECBSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            CharacterWeaponVisualFeedbackLookup = SystemAPI.GetComponentLookup<CharacterWeaponVisualFeedback>(false),
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
        };
        visualsJob.Schedule();
    }
    
    
    [BurstCompile]
    public partial struct StandardRaycastWeaponShotVisualsJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public ComponentLookup<CharacterWeaponVisualFeedback> CharacterWeaponVisualFeedbackLookup;
        [ReadOnly]
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;

        void Execute(
            Entity entity, 
            ref StandardRaycastWeapon weapon, 
            ref WeaponVisualFeedback weaponFeedback,
            ref DynamicBuffer<StandardRaycastWeaponShotVFXRequest> shotVFXRequestsBuffer,
            in WeaponOwner owner)
        {
            // Shot VFX
            for (int i = 0; i < shotVFXRequestsBuffer.Length; i++)
            {
                StandardRaycastWeaponShotVisualsData shotVisualsData = shotVFXRequestsBuffer[i].ShotVisualsData;

                if (LocalToWorldLookup.TryGetComponent(shotVisualsData.VisualOriginEntity, out LocalToWorld originLtW))
                {
                    shotVisualsData.SolvedVisualOrigin = originLtW.Position;
                    shotVisualsData.SolvedVisualOriginToHit = (shotVisualsData.SimulationOrigin + (shotVisualsData.SimulationDirection * shotVisualsData.SimulationHitDistance)) - originLtW.Position;
                    
                    Entity shotVisualsEntity = ECB.Instantiate(weapon.ProjectileVisualPrefab);
                    ECB.SetComponent(shotVisualsEntity, LocalTransform.FromPositionRotation(shotVisualsData.SolvedVisualOrigin, quaternion.LookRotationSafe(shotVisualsData.SimulationDirection, shotVisualsData.SimulationUp)));
                    ECB.AddComponent(shotVisualsEntity, shotVisualsData);
                }
            }
            shotVFXRequestsBuffer.Clear();

            // Shot feedback
            for (int i = 0; i < weaponFeedback.ShotFeedbackRequests; i++)
            {
                if (CharacterWeaponVisualFeedbackLookup.TryGetComponent(owner.Entity, out CharacterWeaponVisualFeedback characterFeedback))
                {
                    characterFeedback.CurrentRecoil += weaponFeedback.RecoilStrength;
                    characterFeedback.TargetRecoilFOVKick += weaponFeedback.RecoilFOVKick;

                    CharacterWeaponVisualFeedbackLookup[owner.Entity] = characterFeedback;
                }
            }
            weaponFeedback.ShotFeedbackRequests = 0;
        }
    }
}
