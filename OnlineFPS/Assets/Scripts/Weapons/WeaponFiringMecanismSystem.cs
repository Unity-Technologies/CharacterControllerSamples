using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
[BurstCompile]
public partial struct WeaponFiringMecanismSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    { 
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<StandardWeaponFiringMecanism, WeaponControl>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StandardWeaponFiringMecanismJob standardMecanismJob = new StandardWeaponFiringMecanismJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = standardMecanismJob.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct StandardWeaponFiringMecanismJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(Entity entity, ref StandardWeaponFiringMecanism mecanism, ref WeaponControl weaponControl, in GhostOwner ghostOwner)
        {
            mecanism.ShotsToFire = 0;
            mecanism.ShotTimer += DeltaTime;

            // Detect starting to fire
            if (weaponControl.FirePressed)
            {
                mecanism.IsFiring = true;
            }

            // Handle firing
            if (mecanism.FiringRate > 0f)
            {
                float delayBetweenShots = 1f / mecanism.FiringRate;
                
                // Clamp shot timer in order to shoot at most the maximum amount of shots that can be shot in one frame based on the firing rate.
                // This also prevents needlessly dirtying the timer ghostfield (saves bandwidth).
                mecanism.ShotTimer = math.clamp(mecanism.ShotTimer, 0f, math.max(delayBetweenShots + 0.01f ,DeltaTime)); 
                
                // This loop is done to allow firing rates that would trigger more than one shot per tick
                while (mecanism.IsFiring && mecanism.ShotTimer > delayBetweenShots)
                {
                    mecanism.ShotsToFire++;
                    
                    // Consume shoot time
                    mecanism.ShotTimer -= delayBetweenShots;

                    // Stop firing after initial shot for non-auto fire
                    if (!mecanism.Automatic)
                    {
                        mecanism.IsFiring = false;
                    }
                }
            }

            // Detect stopping fire
            if (!mecanism.Automatic || weaponControl.FireReleased)
            {
                mecanism.IsFiring = false;
            }
        }
    }
}