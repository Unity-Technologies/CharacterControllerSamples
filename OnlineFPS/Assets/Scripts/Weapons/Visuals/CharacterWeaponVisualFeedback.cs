using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct CharacterWeaponVisualFeedback : IComponentData
{
    public float3 WeaponLocalPosBob;
    public float3 WeaponLocalPosRecoil;
    
    public float CurrentRecoil;
    
    public float TargetRecoilFOVKick;
    public float CurrentRecoilFOVKick;
}
