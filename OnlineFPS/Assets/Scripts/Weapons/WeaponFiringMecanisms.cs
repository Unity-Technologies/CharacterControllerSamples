using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[Serializable]
[GhostComponent(SendTypeOptimization = GhostSendType.OnlyPredictedClients)]
public struct StandardWeaponFiringMecanism : IComponentData, IEnableableComponent
{
    [Serializable]
    public struct Authoring
    {
        public bool AutoFire;
        public float FiringRate;

        public static Authoring GetDefault()
        {
            return new Authoring
            {
                AutoFire = false,
                FiringRate = 1f,
            };
        }
    }

    public StandardWeaponFiringMecanism(Authoring authoring)
    {
        Automatic = authoring.AutoFire;
        FiringRate = authoring.FiringRate;
        ShotTimer = 0f;
        if (FiringRate > 0f)
        {
            ShotTimer = 1f / authoring.FiringRate;
        }
        IsFiring = false;
        ShotsToFire = 0;
    }
    
    public bool Automatic;
    public float FiringRate;

    [GhostField()]
    public float ShotTimer;
    [GhostField()]
    public bool IsFiring;
    public uint ShotsToFire;
    
}

[Serializable]
[GhostComponent()]
public struct WeaponShotVisuals : IComponentData
{
    [GhostField]
    public uint TotalShotsCount;
    public uint LastTotalShotsCount;
    public byte ShotsCountInitialized;
}