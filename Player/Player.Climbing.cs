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
        _stats.SetStaminaActivity(true);

        // Face the surface so left/right movement follows its plane.
        LookAt(GlobalPosition - normal, Vector3.Up);
        Velocity = Vector3.Zero;
        return true;
    }

    private bool TryGetClimbSurface(out Vector3 wallNormal, Vector3 preferredDirection = default)
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

        // When the player moves sideways, probe around the leading shoulder
        // as well as straight ahead. This lets the grab transfer to the next
        // face of a block instead of treating every corner as a drop-off.
        var directions = new System.Collections.Generic.List<Vector3>();
        if (preferredDirection.LengthSquared() > 0.01f)
        {
            preferredDirection.Y = 0f;
            preferredDirection = preferredDirection.Normalized();
            directions.Add((forward + preferredDirection * 1.35f).Normalized());
            directions.Add(preferredDirection);
            directions.Add(forward);
        }
        else
        {
            directions.Add(forward);
        }

        float[] probeHeights = { 0.15f, 0.65f, 1.05f };
        foreach (Vector3 probeDirection in directions)
        {
            foreach (float height in probeHeights)
            {
                Vector3 from = GlobalPosition + Vector3.Up * height;
                Vector3 to = from + probeDirection * ClimbGrabDistance;

                var query = PhysicsRayQueryParameters3D.Create(
                    from, to, CollisionMask);
                query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

                Godot.Collections.Dictionary hit = space.IntersectRay(query);
                if (hit.Count == 0)
                    continue;

                Vector3 normal = hit["normal"].AsVector3();
                if (Mathf.Abs(normal.Y) > 0.55f)
                    continue;

                wallNormal = normal.Normalized();
                return true;
            }
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

        if (Input.IsActionJustPressed("crouch") || (Input.IsKeyPressed(Key.Shift) || Input.IsPhysicalKeyPressed(Key.Shift)))
        {
            ReleaseClimb(false);
            return;
        }

        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_forward", "move_back");
        Vector3 preferredSide = GlobalTransform.Basis.X * Mathf.Sign(input.X);
        if (Mathf.Abs(input.X) < 0.05f)
            preferredSide = Vector3.Zero;

        if (!TryGetClimbSurface(out Vector3 currentNormal, preferredSide))
        {
            // If the player was moving upward and reached the top edge,
            // give a small outward/upward boost to help them get over it.
            Vector2 edgeInput = input;

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

        float vertical = -input.Y;

        // Project world-up onto the wall so movement follows sloped rock
        // faces instead of pushing the player away from the surface.
        Vector3 climbUp =
            Vector3.Up - _climbWallNormal * Vector3.Up.Dot(_climbWallNormal);
        if (climbUp.LengthSquared() > 0.0001f)
            climbUp = climbUp.Normalized();

        Vector3 wallRight = climbUp.Cross(_climbWallNormal);
        if (wallRight.LengthSquared() > 0.0001f)
            wallRight = wallRight.Normalized();

        Vector3 climbDirection =
            climbUp * vertical + wallRight * input.X;

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
