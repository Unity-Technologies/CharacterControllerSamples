using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.CharacterController;

[UpdateInGroup(typeof(SimulationSystemGroup))] // update in variable update because the camera can use gravity to adjust its up direction
[UpdateBefore(typeof(PlatformerCharacterVariableUpdateSystem))]
public partial class GravityZonesSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Update transforms so we have the proper interpolated position of our entities to calculate spherical gravities from
        // (without this, we'd see jitter on the planet)
        World.GetOrCreateSystem<TransformSystemGroup>().Update(World.Unmanaged);
        
        ResetGravitiesJob resetGravitiesJob = new ResetGravitiesJob();
        resetGravitiesJob.Schedule();

        SphericalGravityJob sphericalGravityJob = new SphericalGravityJob
        {
            CustomGravityFromEntity = SystemAPI.GetComponentLookup<CustomGravity>(false),
            LocalToWorldFromEntity = SystemAPI.GetComponentLookup<LocalToWorld>(true),
        };
        sphericalGravityJob.Schedule();

        if (SystemAPI.TryGetSingleton(out GlobalGravityZone globalGravityZone))
        {
            GlobalGravityJob globalGravityJob = new GlobalGravityJob
            {
                GlobalGravityZone = globalGravityZone,
            };
            globalGravityJob.Schedule();
        }

        ApplyGravityJob applyGravityJob = new ApplyGravityJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        applyGravityJob.Schedule();
    }
    
    [BurstCompile]
    public partial struct ResetGravitiesJob : IJobEntity
    {
        void Execute(Entity entity, ref CustomGravity customGravity)
        {
            customGravity.LastZoneEntity = customGravity.CurrentZoneEntity;
            customGravity.TouchedByNonGlobalGravity = false;
        }
    }

    [BurstCompile]
    public unsafe partial struct SphericalGravityJob : IJobEntity
    {
        public ComponentLookup<CustomGravity> CustomGravityFromEntity;
        [ReadOnly]
        public ComponentLookup<LocalToWorld> LocalToWorldFromEntity;

        void Execute(Entity entity, in SphericalGravityZone sphericalGravityZone, in PhysicsCollider physicsCollider, in DynamicBuffer<StatefulTriggerEvent> triggerEventsBuffer)
        {
            if (triggerEventsBuffer.Length > 0)
            {
                SphereCollider* sphereCollider = ((SphereCollider*)physicsCollider.ColliderPtr);
                SphereGeometry sphereGeometry = sphereCollider->Geometry;

                for (int i = 0; i < triggerEventsBuffer.Length; i++)
                {
                    StatefulTriggerEvent triggerEvent = triggerEventsBuffer[i];
                    if (triggerEvent.State == StatefulEventState.Stay)
                    {
                        Entity otherEntity = triggerEvent.GetOtherEntity(entity);

                        float3 fromOtherToSelfVector = LocalToWorldFromEntity[entity].Position - LocalToWorldFromEntity[otherEntity].Position;
                        float distanceRatio = math.clamp(math.length(fromOtherToSelfVector) / sphereGeometry.Radius, 0.01f, 0.99f);
                        float3 gravityToApply = ((1f - distanceRatio) * (math.normalizesafe(fromOtherToSelfVector) * sphericalGravityZone.GravityStrengthAtCenter));

                        if (CustomGravityFromEntity.HasComponent(otherEntity))
                        {
                            CustomGravity customGravity = CustomGravityFromEntity[otherEntity];
                            customGravity.Gravity = gravityToApply * customGravity.GravityMultiplier;
                            customGravity.TouchedByNonGlobalGravity = true;
                            customGravity.CurrentZoneEntity = entity;
                            CustomGravityFromEntity[otherEntity] = customGravity;
                        }
                    }
                }
            }
        }
    }

    [BurstCompile]
    public partial struct GlobalGravityJob : IJobEntity
    {
        public GlobalGravityZone GlobalGravityZone;

        void Execute(Entity entity, ref CustomGravity customGravity)
        {
            if (!customGravity.TouchedByNonGlobalGravity)
            {
                customGravity.Gravity = GlobalGravityZone.Gravity * customGravity.GravityMultiplier;
                customGravity.CurrentZoneEntity = Entity.Null;
            }
        }
    }

    [BurstCompile]
    public partial struct ApplyGravityJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(Entity entity, ref PhysicsVelocity physicsVelocity, in PhysicsMass physicsMass, in CustomGravity customGravity)
        {
            if (physicsMass.InverseMass > 0f)
            {
                CharacterControlUtilities.AccelerateVelocity(ref physicsVelocity.Linear, customGravity.Gravity, DeltaTime);
            }
        }
    }
}