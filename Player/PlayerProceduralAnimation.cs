using Godot;

/// <summary>
/// Procedural player locomotion. Feet follow ground targets and the two-bone
/// leg solver bends the hips and knees to reach those targets. The step cycle
/// is phase-based so the stance portion travels backward relative to the body,
/// creating the impression that the foot is planted while the player advances.
/// </summary>
public partial class Player
{
    private Node3D _proceduralLeftHip;
    private Node3D _proceduralRightHip;
    private Node3D _proceduralLeftKnee;
    private Node3D _proceduralRightKnee;

    private float _proceduralWalkPhase;
    private float _proceduralBaseModelY;
    private bool _proceduralAnimationReady;

    private const float ProceduralUpperLegLength = 0.375f;
    private const float ProceduralLowerLegLength = 0.375f;
    private const float ProceduralHipHalfWidth = 0.125f;

    private void UpdateProceduralWalkAnimation(float delta)
    {
        if (_playerModelParts == null || !IsInstanceValid(_playerModelParts))
            return;

        if (!_proceduralAnimationReady)
        {
            _proceduralLeftHip = _playerModelParts.FindChild("LeftHipPivot", true, false) as Node3D;
            _proceduralRightHip = _playerModelParts.FindChild("RightHipPivot", true, false) as Node3D;
            _proceduralLeftKnee = _playerModelParts.FindChild("LeftKneePivot", true, false) as Node3D;
            _proceduralRightKnee = _playerModelParts.FindChild("RightKneePivot", true, false) as Node3D;
            _proceduralBaseModelY = _playerModelRoot?.Position.Y ?? -0.9f;
            _proceduralAnimationReady = true;
        }

        if (_proceduralLeftHip == null || _proceduralRightHip == null ||
            _proceduralLeftKnee == null || _proceduralRightKnee == null)
        {
            // If the imported model is missing a joint, retain the safe legacy
            // animation rather than leaving its limbs in an arbitrary pose.
            UpdateFirstPersonArms(delta);
            return;
        }

        Vector3 horizontalVelocity = new Vector3(Velocity.X, 0f, Velocity.Z);
        float speed = horizontalVelocity.Length();
        bool moving = speed > 0.12f && IsOnFloor();

        if (!IsOnFloor())
        {
            ResetLimbParts(_leftLegParts, delta);
            ResetLimbParts(_rightLegParts, delta);
            ResetLimbParts(_leftLowerLegParts, delta);
            ResetLimbParts(_rightLowerLegParts, delta);
            ResetLimbParts(_leftArmParts, delta);
            ResetLimbParts(_rightArmParts, delta);
            ResetLimbParts(_leftForearmParts, delta);
            ResetLimbParts(_rightForearmParts, delta);
            UpdateProceduralBodyBob(0f, delta);
            return;
        }

        if (moving)
        {
            // Stride length grows gently with speed. The phase rate is tied to
            // actual travel speed so the planted half of each step moves back
            // relative to the character at approximately the same rate.
            float stride = Mathf.Clamp(speed * 0.07f, 0.24f, 0.46f);
            float phaseRate = speed * Mathf.Pi / (2f * stride);
            _proceduralWalkPhase = Mathf.PosMod(
                _proceduralWalkPhase + phaseRate * delta,
                Mathf.Tau
            );

            Vector3 localMove = _playerModelParts.GlobalTransform.Basis.Inverse() *
                                horizontalVelocity.Normalized();
            float forwardSign = Mathf.Abs(localMove.Z) > 0.2f
                ? Mathf.Sign(-localMove.Z)
                : 1f;

            float leftPhase = _proceduralWalkPhase;
            float rightPhase = Mathf.PosMod(_proceduralWalkPhase + Mathf.Pi, Mathf.Tau);

            Vector3 leftFootTarget = FindProceduralFootTarget(
                -ProceduralHipHalfWidth,
                -Mathf.Cos(leftPhase) * stride * forwardSign,
                Mathf.Max(0f, -Mathf.Sin(leftPhase)) * Mathf.Lerp(0.10f, 0.17f,
                    Mathf.Clamp((speed - WalkSpeed) / Mathf.Max(0.1f, SprintSpeed - WalkSpeed), 0f, 1f))
            );

            Vector3 rightFootTarget = FindProceduralFootTarget(
                ProceduralHipHalfWidth,
                -Mathf.Cos(rightPhase) * stride * forwardSign,
                Mathf.Max(0f, -Mathf.Sin(rightPhase)) * Mathf.Lerp(0.10f, 0.17f,
                    Mathf.Clamp((speed - WalkSpeed) / Mathf.Max(0.1f, SprintSpeed - WalkSpeed), 0f, 1f))
            );

            SolveProceduralLeg(_proceduralLeftHip, _proceduralLeftKnee, leftFootTarget);
            SolveProceduralLeg(_proceduralRightHip, _proceduralRightKnee, rightFootTarget);

            // Arms counter-swing against the legs, with a little elbow softness.
            float armSwing = Mathf.Sin(_proceduralWalkPhase) * 0.24f;
            ApplyLimbSwing(_leftArmParts, armSwing);
            ApplyLimbSwing(_rightArmParts, -armSwing);
            ApplyLimbSwing(_leftForearmParts, -Mathf.Max(0f, armSwing) * 0.22f);
            ApplyLimbSwing(_rightForearmParts, -Mathf.Max(0f, -armSwing) * 0.22f);

            UpdateProceduralBodyBob(_proceduralWalkPhase, delta);
        }
        else
        {
            Vector3 leftFootTarget = FindProceduralFootTarget(-ProceduralHipHalfWidth, 0f, 0f);
            Vector3 rightFootTarget = FindProceduralFootTarget(ProceduralHipHalfWidth, 0f, 0f);

            SolveProceduralLeg(_proceduralLeftHip, _proceduralLeftKnee, leftFootTarget);
            SolveProceduralLeg(_proceduralRightHip, _proceduralRightKnee, rightFootTarget);

            ResetLimbParts(_leftArmParts, delta);
            ResetLimbParts(_rightArmParts, delta);
            ResetLimbParts(_leftForearmParts, delta);
            ResetLimbParts(_rightForearmParts, delta);

            UpdateProceduralBodyBob(0f, delta);
        }
    }

    private Vector3 FindProceduralFootTarget(float sideOffset, float forwardOffset, float lift)
    {
        Vector3 localFoot = new Vector3(sideOffset, 0f, forwardOffset);
        Vector3 desiredWorld = _playerModelParts.ToGlobal(localFoot);
        Vector3 rayStart = desiredWorld + Vector3.Up * 1.25f;
        Vector3 rayEnd = desiredWorld - Vector3.Up * 1.25f;

        World3D world = GetWorld3D();
        if (world != null)
        {
            PhysicsRayQueryParameters3D query =
                PhysicsRayQueryParameters3D.Create(rayStart, rayEnd);
            query.CollisionMask = uint.MaxValue;
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

            Godot.Collections.Dictionary hit =
                world.DirectSpaceState.IntersectRay(query);

            if (hit.Count > 0 && hit.ContainsKey("position"))
                desiredWorld = hit["position"].AsVector3();
        }

        return desiredWorld + Vector3.Up * lift;
    }

    private void SolveProceduralLeg(Node3D hip, Node3D knee, Vector3 footTargetWorld)
    {
        if (hip == null || knee == null ||
            !IsInstanceValid(hip) || !IsInstanceValid(knee))
            return;

        Vector3 hipLocal = _playerModelParts.ToLocal(hip.GlobalPosition);
        Vector3 footLocal = _playerModelParts.ToLocal(footTargetWorld);
        Vector3 offset = footLocal - hipLocal;

        // The model's leg chain is aligned in the vertical Y/Z plane. Ignore
        // tiny lateral offsets so the knee does not twist sideways.
        float vertical = offset.Y;
        float forward = offset.Z;
        float distance = Mathf.Sqrt(vertical * vertical + forward * forward);

        float upper = ProceduralUpperLegLength;
        float lower = ProceduralLowerLegLength;
        float maximumReach = upper + lower - 0.006f;
        float minimumReach = Mathf.Abs(upper - lower) + 0.006f;
        distance = Mathf.Clamp(distance, minimumReach, maximumReach);

        float directionAngle = Mathf.Atan2(-forward, -vertical);
        float cosine = (upper * upper + distance * distance - lower * lower) /
                       (2f * upper * distance);
        float triangleAngle = Mathf.Acos(Mathf.Clamp(cosine, -1f, 1f));

        // Positive X rotation bends the knee toward the character's forward
        // direction (-Z), matching the orientation of the imported model.
        float hipAngle = directionAngle - triangleAngle;
        float kneeAngle = triangleAngle * 2f;

        hip.Rotation = new Vector3(hipAngle, 0f, 0f);
        knee.Rotation = new Vector3(kneeAngle, 0f, 0f);
    }

    private void UpdateProceduralBodyBob(float phase, float delta)
    {
        if (_playerModelRoot == null || !IsInstanceValid(_playerModelRoot))
            return;

        float bob = phase == 0f ? 0f : Mathf.Abs(Mathf.Sin(phase * 2f)) * 0.018f;
        float targetY = _proceduralBaseModelY + bob;
        Vector3 position = _playerModelRoot.Position;
        position.Y = Mathf.Lerp(position.Y, targetY, Mathf.Clamp(delta * 12f, 0f, 1f));
        _playerModelRoot.Position = position;
    }
}
