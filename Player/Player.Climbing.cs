using Godot;

/// <summary>
/// Breath-of-the-Wild-inspired free climbing.
/// Jump toward a solid, mostly vertical surface to grab it. Use movement
/// controls to climb up/down/across. Stamina drains while moving; empty
/// stamina makes the player let go and fall.
/// </summary>
public partial class Player
{
    private bool _isClimbing = false;
    private Vector3 _climbWallNormal = Vector3.Zero;

    private const float ClimbGrabDistance = 1.05f;
    private const float ClimbSpeed = 2.15f;
    private const float ClimbStaminaDrain = 2.0f;
    private const float ClimbSidewaysStaminaDrain = 1.5f;
    private const float ClimbFallSpeed = 1.8f;

    private bool TryStartClimbing()
    {
        if (_isFlying || IsMounted || _isInWater ||
            _stats == null || _stats.Stamina <= 0.001f)
            return false;

        if (!TryGetClimbSurface(out Vector3 normal))
            return false;

        _isClimbing = true;
        _climbWallNormal = normal;

        // Face the surface so left/right movement follows its plane.
        LookAt(GlobalPosition - normal, Vector3.Up);
        Velocity = Vector3.Zero;
        return true;
    }

    private bool TryGetClimbSurface(out Vector3 wallNormal)
    {
        wallNormal = Vector3.Zero;

        PhysicsDirectSpaceState3D space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return false;

        Vector3 forward = -GlobalTransform.Basis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 0.0001f)
            return false;
        forward = forward.Normalized();

        // Probe at the torso, shoulders and upper body. A valid climbing
        // surface must be close and close to vertical, not a floor or ceiling.
        float[] probeHeights = { 0.15f, 0.65f, 1.05f };
        foreach (float height in probeHeights)
        {
            Vector3 from = GlobalPosition + Vector3.Up * height;
            Vector3 to = from + forward * ClimbGrabDistance;

            var query = PhysicsRayQueryParameters3D.Create(
                from, to, CollisionMask);
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

            Godot.Collections.Dictionary hit = space.IntersectRay(query);
            if (hit.Count == 0)
                continue;

            Vector3 normal = hit["normal"].AsVector3();
            if (Mathf.Abs(normal.Y) > 0.25f)
                continue;

            wallNormal = normal.Normalized();
            return true;
        }

        return false;
    }

    private void HandleClimbing(float delta)
    {
        // Jump kicks away from the wall; crouch deliberately lets go.
        if (Input.IsActionJustPressed("jump"))
        {
            ReleaseClimb(true);
            return;
        }

        if (Input.IsActionJustPressed("crouch"))
        {
            ReleaseClimb(false);
            return;
        }

        if (!TryGetClimbSurface(out Vector3 currentNormal))
        {
            // If the player was moving upward and reached the top edge,
            // give a small outward/upward boost to help them get over it.
            Vector2 edgeInput = Input.GetVector(
                "move_left", "move_right", "move_forward", "move_back");

            _isClimbing = false;
            if (-edgeInput.Y > 0.1f)
            {
                Velocity = -_climbWallNormal * 2.0f + Vector3.Up * 2.3f;
            }
            else
            {
                Velocity = Vector3.Down * ClimbFallSpeed;
            }

            MoveAndSlide();
            return;
        }

        _climbWallNormal = currentNormal;
        LookAt(GlobalPosition - _climbWallNormal, Vector3.Up);

        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_forward", "move_back");

        float vertical = -input.Y;
        Vector3 wallRight = Vector3.Up.Cross(_climbWallNormal);
        if (wallRight.LengthSquared() > 0.0001f)
            wallRight = wallRight.Normalized();

        Vector3 climbDirection =
            Vector3.Up * vertical + wallRight * input.X;

        if (climbDirection.LengthSquared() > 1f)
            climbDirection = climbDirection.Normalized();

        if (climbDirection.LengthSquared() > 0.001f)
        {
            float drainRate = Mathf.Abs(input.X) > 0.05f &&
                              Mathf.Abs(vertical) < 0.05f
                ? ClimbSidewaysStaminaDrain
                : ClimbStaminaDrain;

            if (_stats == null ||
                !_stats.DrainStamina(drainRate * delta))
            {
                // Empty stamina means an immediate release, just like BOTW.
                _isClimbing = false;
                Velocity = Vector3.Down * ClimbFallSpeed;
                MoveAndSlide();
                return;
            }
        }

        Velocity = climbDirection * ClimbSpeed;
        MoveAndSlide();
    }

    private void ReleaseClimb(bool jumpAway)
    {
        _isClimbing = false;

        if (jumpAway)
        {
            Velocity =
                _climbWallNormal * 2.0f +
                Vector3.Up * (JumpVelocity * 0.78f);
        }
        else
        {
            Velocity = Vector3.Down * ClimbFallSpeed;
        }

        MoveAndSlide();
    }
}
