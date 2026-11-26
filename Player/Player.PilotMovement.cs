using Godot;

// Pilot movement is intentionally isolated from the main controller so the
// normal movement path remains unchanged when the Pilot Helmet is not worn.
public partial class Player : CharacterBody3D
{
    private bool _isWallRunning;
    private Vector3 _wallRunNormal = Vector3.Zero;
    private float _wallRunTime;
    private const float PilotWallRunMaxTime = 1.65f;
    private const float PilotWallRunSpeed = 7.2f;
    private const float PilotWallJumpOutward = 7.5f;
    private const float PilotWallJumpUpward = 7.2f;

    private bool _isMantling;
    private Vector3 _mantleStart;
    private Vector3 _mantleTarget;
    private float _mantleTime;
    private const float PilotMantleDuration = 0.30f;

    private void ApplyPilotWallRun(ref Vector3 velocity, Vector3 moveDirection, float delta)
    {
        if (!CanWallClimb || _isInWater || IsOnFloor() ||
            moveDirection.LengthSquared() < 0.01f)
        {
            _isWallRunning = false;
            _wallRunTime = 0f;
            return;
        }

        if (_wallRunTime >= PilotWallRunMaxTime ||
            !TryFindPilotWall(out Vector3 normal))
        {
            _isWallRunning = false;
            _wallRunTime = 0f;
            return;
        }

        Vector3 tangent = moveDirection - normal * moveDirection.Dot(normal);
        tangent.Y = 0f;
        if (tangent.LengthSquared() < 0.04f)
        {
            _isWallRunning = false;
            _wallRunTime = 0f;
            return;
        }

        tangent = tangent.Normalized();
        if (tangent.Dot(moveDirection) < 0f)
            tangent = -tangent;

        _isWallRunning = true;
        _wallRunNormal = normal;
        _wallRunTime += delta;

        float currentHorizontalSpeed = new Vector2(velocity.X, velocity.Z).Length();
        float runSpeed = Mathf.Max(currentHorizontalSpeed, PilotWallRunSpeed);
        velocity.X = tangent.X * runSpeed;
        velocity.Z = tangent.Z * runSpeed;

        // Titanfall-inspired wall contact: gravity is strongly reduced, but
        // the player slowly descends instead of sticking indefinitely.
        velocity.Y = Mathf.Max(velocity.Y - 1.8f * delta, -2.25f);
    }

    private bool HandlePilotJump(ref Vector3 velocity)
    {
        if (!CanWallClimb)
            return false;

        if (_isWallRunning)
        {
            Vector3 forward = _playerCamera != null
                ? -_playerCamera.GlobalTransform.Basis.Z
                : -GlobalTransform.Basis.Z;
            forward.Y = 0f;
            if (forward.LengthSquared() > 0.001f)
                forward = forward.Normalized();

            velocity = _wallRunNormal * PilotWallJumpOutward +
                       Vector3.Up * PilotWallJumpUpward +
                       forward * 2.0f;

            _isWallRunning = false;
            _wallRunTime = 0f;
            return true;
        }

        // A jump pressed while facing a climbable ledge starts a short mantle.
        if (!IsOnFloor() &&
            TryFindPilotWall(out Vector3 wallNormal) &&
            TryFindPilotMantleTarget(wallNormal, out Vector3 target))
        {
            _isWallRunning = false;
            _mantleStart = GlobalPosition;
            _mantleTarget = target;
            _mantleTime = 0f;
            _isMantling = true;
            velocity = Vector3.Zero;
            return true;
        }

        return false;
    }

    private bool AdvancePilotMantle(float delta)
    {
        if (!_isMantling)
            return false;

        _mantleTime += delta;
        float t = Mathf.Clamp(_mantleTime / PilotMantleDuration, 0f, 1f);
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        Vector3 position = _mantleStart.Lerp(_mantleTarget, eased);
        position.Y += Mathf.Sin(t * Mathf.Pi) * 0.28f;
        GlobalPosition = position;

        if (t >= 1f)
        {
            _isMantling = false;
            _wallRunTime = 0f;
        }

        return true;
    }

    private bool TryFindPilotWall(out Vector3 wallNormal)
    {
        wallNormal = Vector3.Zero;
        if (GetWorld3D() == null)
            return false;

        Vector3 right = GlobalTransform.Basis.X;
        right.Y = 0f;
        if (right.LengthSquared() < 0.001f)
            return false;
        right = right.Normalized();

        Vector3 origin = GlobalPosition + Vector3.Up * 0.20f;
        Vector3[] directions = { right, -right };

        foreach (Vector3 direction in directions)
        {
            var query = PhysicsRayQueryParameters3D.Create(
                origin,
                origin + direction * 0.82f
            );
            query.CollisionMask = 3;
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count == 0 || !hit.ContainsKey("normal"))
                continue;

            Vector3 normal = hit["normal"].AsVector3();
            normal.Y = 0f;
            if (normal.LengthSquared() < 0.01f)
                continue;

            wallNormal = normal.Normalized();
            return true;
        }

        return false;
    }

    private bool TryFindPilotMantleTarget(Vector3 wallNormal, out Vector3 target)
    {
        target = Vector3.Zero;
        Vector3 towardWall = -wallNormal;
        Vector3 basePosition = GlobalPosition;

        Vector3 lowOrigin = basePosition + Vector3.Up * 0.25f;
        Vector3 highOrigin = basePosition + Vector3.Up * 1.25f;

        var lowQuery = PhysicsRayQueryParameters3D.Create(
            lowOrigin, lowOrigin + towardWall * 1.05f);
        lowQuery.CollisionMask = 3;
        lowQuery.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var highQuery = PhysicsRayQueryParameters3D.Create(
            highOrigin, highOrigin + towardWall * 1.05f);
        highQuery.CollisionMask = 3;
        highQuery.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var lowHit = GetWorld3D().DirectSpaceState.IntersectRay(lowQuery);
        var highHit = GetWorld3D().DirectSpaceState.IntersectRay(highQuery);
        if (lowHit.Count == 0 || highHit.Count > 0)
            return false;

        Vector3 topRayStart = basePosition + towardWall * 0.95f + Vector3.Up * 2.0f;
        var topQuery = PhysicsRayQueryParameters3D.Create(
            topRayStart, topRayStart - Vector3.Up * 2.35f);
        topQuery.CollisionMask = 3;
        topQuery.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var topHit = GetWorld3D().DirectSpaceState.IntersectRay(topQuery);
        if (topHit.Count == 0 || !topHit.ContainsKey("normal"))
            return false;

        Vector3 topNormal = topHit["normal"].AsVector3();
        if (topNormal.Y < 0.65f)
            return false;

        Vector3 topPoint = topHit["position"].AsVector3();
        target = topPoint + Vector3.Up * 0.92f + towardWall * 0.18f;
        return true;
    }
}
