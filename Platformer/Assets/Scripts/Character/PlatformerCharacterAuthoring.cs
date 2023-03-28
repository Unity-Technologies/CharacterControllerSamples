using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using Unity.CharacterController;
using Unity.Physics;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(PhysicsShapeAuthoring))]
public class PlatformerCharacterAuthoring : MonoBehaviour
{
    public AuthoringKinematicCharacterProperties CharacterProperties = AuthoringKinematicCharacterProperties.GetDefault();
    public PlatformerCharacterComponent Character = default;

    [Header("References")]
    public GameObject MeshPrefab;
    public GameObject DefaultCameraTarget;
    public GameObject SwimmingCameraTarget;
    public GameObject ClimbingCameraTarget;
    public GameObject CrouchingCameraTarget;
    public GameObject MeshRoot;
    public GameObject RollballMesh;
    public GameObject RopePrefab;
    public GameObject SwimmingDetectionPoint;
    public GameObject LedgeDetectionPoint;

    [Header("Debug")]
    public bool DebugStandingGeometry;
    public bool DebugCrouchingGeometry;
    public bool DebugRollingGeometry;
    public bool DebugClimbingGeometry;
    public bool DebugSwimmingGeometry;

    public class Baker : Baker<PlatformerCharacterAuthoring>
    {
        public override void Bake(PlatformerCharacterAuthoring authoring)
        {
            KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterProperties);
            
            authoring.Character.DefaultCameraTargetEntity = GetEntity(authoring.DefaultCameraTarget, TransformUsageFlags.Dynamic);
            authoring.Character.SwimmingCameraTargetEntity = GetEntity(authoring.SwimmingCameraTarget, TransformUsageFlags.Dynamic);
            authoring.Character.ClimbingCameraTargetEntity = GetEntity(authoring.ClimbingCameraTarget, TransformUsageFlags.Dynamic);
            authoring.Character.CrouchingCameraTargetEntity = GetEntity(authoring.CrouchingCameraTarget, TransformUsageFlags.Dynamic);
            authoring.Character.MeshRootEntity = GetEntity(authoring.MeshRoot, TransformUsageFlags.Dynamic);
            authoring.Character.RollballMeshEntity = GetEntity(authoring.RollballMesh, TransformUsageFlags.Dynamic);
            authoring.Character.RopePrefabEntity = GetEntity(authoring.RopePrefab, TransformUsageFlags.Dynamic);
            authoring.Character.LocalSwimmingDetectionPoint = authoring.SwimmingDetectionPoint.transform.localPosition;
            authoring.Character.LocalLedgeDetectionPoint = authoring.LedgeDetectionPoint.transform.localPosition;

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, authoring.Character);
            AddComponent(entity, new PlatformerCharacterControl());
            AddComponent(entity, new PlatformerCharacterStateMachine());
            AddComponentObject(entity, new PlatformerCharacterHybridData { MeshPrefab = authoring.MeshPrefab });
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (DebugStandingGeometry)
        {
            Gizmos.color = Color.cyan;
            DrawCapsuleGizmo(Character.StandingGeometry);
        }
        if (DebugCrouchingGeometry)
        {
            Gizmos.color = Color.cyan;
            DrawCapsuleGizmo(Character.CrouchingGeometry);
        }
        if (DebugRollingGeometry)
        {
            Gizmos.color = Color.cyan;
            DrawCapsuleGizmo(Character.RollingGeometry);
        }
        if (DebugClimbingGeometry)
        {
            Gizmos.color = Color.cyan;
            DrawCapsuleGizmo(Character.ClimbingGeometry);
        }
        if (DebugSwimmingGeometry)
        {
            Gizmos.color = Color.cyan;
            DrawCapsuleGizmo(Character.SwimmingGeometry);
        }
    }

    private void DrawCapsuleGizmo(CapsuleGeometryDefinition capsuleGeo)
    {
        RigidTransform characterTransform = new RigidTransform(transform.rotation, transform.position);
        float3 characterUp = transform.up;
        float3 characterFwd = transform.forward;
        float3 characterRight = transform.right;
        float3 capsuleCenter = math.transform(characterTransform, capsuleGeo.Center);
        float halfHeight = capsuleGeo.Height * 0.5f;

        float3 bottomHemiCenter = capsuleCenter - (characterUp * (halfHeight - capsuleGeo.Radius));
        float3 topHemiCenter = capsuleCenter + (characterUp * (halfHeight - capsuleGeo.Radius));

        Gizmos.DrawWireSphere(bottomHemiCenter, capsuleGeo.Radius);
        Gizmos.DrawWireSphere(topHemiCenter, capsuleGeo.Radius);

        Gizmos.DrawLine(bottomHemiCenter + (characterFwd * capsuleGeo.Radius), topHemiCenter + (characterFwd * capsuleGeo.Radius));
        Gizmos.DrawLine(bottomHemiCenter - (characterFwd * capsuleGeo.Radius), topHemiCenter - (characterFwd * capsuleGeo.Radius));
        Gizmos.DrawLine(bottomHemiCenter + (characterRight * capsuleGeo.Radius), topHemiCenter + (characterRight * capsuleGeo.Radius));
        Gizmos.DrawLine(bottomHemiCenter - (characterRight * capsuleGeo.Radius), topHemiCenter - (characterRight * capsuleGeo.Radius));
    }
}