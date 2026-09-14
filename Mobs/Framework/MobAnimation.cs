using Godot;

// =============================================================
// MOB ANIMATION
// =============================================================
//
// Handles procedural visual animation for mobs.
//
// Current animations:
// - Walking
// - Sleeping
// - Waking
//
// Sleep is a procedural flat "star/sprawl" pose.
// No GLTF animation clips are required.
// =============================================================

public class MobAnimation
{
    private Node3D _mobModel;

    private Node3D _frontRightLeg;
    private Node3D _frontLeftLeg;
    private Node3D _backRightLeg;
    private Node3D _backLeftLeg;

    private Node3D _body;
    private Node3D _head;

    private float _walkAnimationTime = 0f;
    private float _moveSpeed = 2.5f;

    // ---------------------------------------------------------
    // SLEEP
    // ---------------------------------------------------------

    private bool _sleeping = false;

    private float _sleepTransition = 0f;

    private bool _sleepGroundPositionCalculated = false;

    private const float SleepTransitionSpeed = 3f;

    private Vector3 _modelStandingPosition;

    private Vector3 _bodyStandingRotation;
    private Vector3 _headStandingRotation;

    private Vector3 _frontRightStandingRotation;
    private Vector3 _frontLeftStandingRotation;
    private Vector3 _backRightStandingRotation;
    private Vector3 _backLeftStandingRotation;

    private Vector3 _frontRightSleepRotation;
    private Vector3 _frontLeftSleepRotation;
    private Vector3 _backRightSleepRotation;
    private Vector3 _backLeftSleepRotation;

    private Vector3 _bodySleepRotation;
    private Vector3 _headSleepRotation;

    private Vector3 _modelSleepPosition;


    // =========================================================
    // SETUP
    // =========================================================

    public void Setup(
        Node3D mobModel,
        float moveSpeed)
    {
        _mobModel =
            mobModel;

        _moveSpeed =
            Mathf.Max(
                moveSpeed,
                0.01f
            );

        _walkAnimationTime =
            0f;

        _sleeping =
            false;

        _sleepTransition =
            0f;

        _sleepGroundPositionCalculated =
            false;

        FindNodes();

        CaptureStandingPose();

        BuildSleepPose();
    }


    // =========================================================
    // FIND MODEL NODES
    // =========================================================

    private void FindNodes()
    {
        _frontRightLeg =
            _mobModel?.FindChild(
                "front_right",
                true,
                false
            ) as Node3D;

        _frontLeftLeg =
            _mobModel?.FindChild(
                "front_left",
                true,
                false
            ) as Node3D;

        _backRightLeg =
            _mobModel?.FindChild(
                "back_right",
                true,
                false
            ) as Node3D;

        _backLeftLeg =
            _mobModel?.FindChild(
                "back_left",
                true,
                false
            ) as Node3D;

        _body =
            _mobModel?.FindChild(
                "body",
                true,
                false
            ) as Node3D;

        _head =
            _mobModel?.FindChild(
                "head",
                true,
                false
            ) as Node3D;

        if (_frontRightLeg == null ||
            _frontLeftLeg == null ||
            _backRightLeg == null ||
            _backLeftLeg == null)
        {
            GD.PrintErr(
                "[MobAnimation] Could not find all four leg nodes."
            );
        }
    }


    // =========================================================
    // CAPTURE STANDING POSE
    // =========================================================

    private void CaptureStandingPose()
    {
        if (_mobModel == null)
            return;

        _modelStandingPosition =
            _mobModel.Position;

        if (_body != null)
        {
            _bodyStandingRotation =
                _body.Rotation;
        }

        if (_head != null)
        {
            _headStandingRotation =
                _head.Rotation;
        }

        if (_frontRightLeg != null)
        {
            _frontRightStandingRotation =
                _frontRightLeg.Rotation;
        }

        if (_frontLeftLeg != null)
        {
            _frontLeftStandingRotation =
                _frontLeftLeg.Rotation;
        }

        if (_backRightLeg != null)
        {
            _backRightStandingRotation =
                _backRightLeg.Rotation;
        }

        if (_backLeftLeg != null)
        {
            _backLeftStandingRotation =
                _backLeftLeg.Rotation;
        }
    }


    // =========================================================
    // BUILD SLEEP POSE
    // =========================================================
    //
    // The mob sprawls outward like a four-point star.
    //
    // Front legs:
    //   forward + outward
    //
    // Back legs:
    //   backward + outward
    //
    // The whole model is NOT manually translated downward.
    // Its final ground position is calculated from its mesh.
    // =========================================================

    private void BuildSleepPose()
    {
        _modelSleepPosition =
            _modelStandingPosition;


        // -----------------------------------------------------
        // FRONT RIGHT
        // -----------------------------------------------------

        _frontRightSleepRotation =
            _frontRightStandingRotation +
            new Vector3(
                1.55f,
                0f,
                0.9f
            );


        // -----------------------------------------------------
        // FRONT LEFT
        // -----------------------------------------------------

        _frontLeftSleepRotation =
            _frontLeftStandingRotation +
            new Vector3(
                1.55f,
                0f,
                -0.9f
            );


        // -----------------------------------------------------
        // BACK RIGHT
        // -----------------------------------------------------

        _backRightSleepRotation =
            _backRightStandingRotation +
            new Vector3(
                -1.55f,
                0f,
                0.9f
            );


        // -----------------------------------------------------
        // BACK LEFT
        // -----------------------------------------------------

        _backLeftSleepRotation =
            _backLeftStandingRotation +
            new Vector3(
                -1.55f,
                0f,
                -0.9f
            );


        // -----------------------------------------------------
        // BODY
        // -----------------------------------------------------
        //
        // Keep the body straight.
        // Rotating the body was causing the lower body
        // geometry to bend inward.
        // -----------------------------------------------------

        _bodySleepRotation =
            _bodyStandingRotation;


        // -----------------------------------------------------
        // HEAD
        // -----------------------------------------------------

        _headSleepRotation =
            _headStandingRotation +
            new Vector3(
                0.35f,
                0f,
                0f
            );
    }


    // =========================================================
    // SET SLEEPING
    // =========================================================

    public void SetSleeping(
        bool sleeping)
    {
        _sleeping =
            sleeping;

        _sleepGroundPositionCalculated =
            false;

        if (!sleeping &&
            _mobModel != null)
        {
            _mobModel.Position =
                _modelStandingPosition;
        }
    }


    // =========================================================
    // IS SLEEPING
    // =========================================================

    public bool IsSleeping =>
        _sleeping &&
        _sleepTransition >= 0.95f;


    // =========================================================
    // UPDATE
    // =========================================================

    public void Update(
        float dt,
        Vector3 velocity)
    {
        UpdateSleepTransition(
            dt
        );

        if (_sleepTransition > 0.001f)
        {
            UpdateSleepAnimation();

            return;
        }

        UpdateWalkAnimation(
            dt,
            velocity
        );
    }


    // =========================================================
    // SLEEP TRANSITION
    // =========================================================

    private void UpdateSleepTransition(
        float dt)
    {
        float target =
            _sleeping
                ? 1f
                : 0f;

        _sleepTransition =
            Mathf.MoveToward(
                _sleepTransition,
                target,
                dt *
                SleepTransitionSpeed
            );
    }


    // =========================================================
    // SLEEP ANIMATION
    // =========================================================

    private void UpdateSleepAnimation()
    {
        float t =
            Mathf.SmoothStep(
                0f,
                1f,
                _sleepTransition
            );

        if (_mobModel != null)
        {
            _mobModel.Position =
                _modelStandingPosition.Lerp(
                    _modelSleepPosition,
                    t
                );
        }

        LerpRotation(
            _frontRightLeg,
            _frontRightStandingRotation,
            _frontRightSleepRotation,
            t
        );

        LerpRotation(
            _frontLeftLeg,
            _frontLeftStandingRotation,
            _frontLeftSleepRotation,
            t
        );

        LerpRotation(
            _backRightLeg,
            _backRightStandingRotation,
            _backRightSleepRotation,
            t
        );

        LerpRotation(
            _backLeftLeg,
            _backLeftStandingRotation,
            _backLeftSleepRotation,
            t
        );

        LerpRotation(
            _body,
            _bodyStandingRotation,
            _bodySleepRotation,
            t
        );

        LerpRotation(
            _head,
            _headStandingRotation,
            _headSleepRotation,
            t
        );

        // -----------------------------------------------------
        // Once the full sleep pose is reached, calculate where
        // the actual bottom of this specific model is.
        // -----------------------------------------------------

        if (_sleeping &&
            !_sleepGroundPositionCalculated &&
            _sleepTransition >= 0.99f)
        {
            CalculateSleepGroundPosition();
        }
    }


    // =========================================================
    // CALCULATE SLEEP GROUND POSITION
    // =========================================================
    //
    // Finds the lowest point of the mob's actual meshes and
    // automatically moves the sleeping model so that point
    // rests at ground level.
    //
    // This makes the system universal for different mob sizes.
    // =========================================================

    private void CalculateSleepGroundPosition()
    {
        if (_mobModel == null)
            return;

        float lowestY =
            float.MaxValue;

        Transform3D inverse =
            _mobModel.GlobalTransform.AffineInverse();

        foreach (Node node in
                 _mobModel.FindChildren(
                     "*",
                     "MeshInstance3D",
                     true,
                     false
                 ))
        {
            if (node is not MeshInstance3D mesh)
                continue;

            if (mesh.Mesh == null)
                continue;

            Aabb bounds =
                mesh.Mesh.GetAabb();

            Vector3 min =
                bounds.Position;

            Vector3 max =
                bounds.Position +
                bounds.Size;

            Vector3[] corners =
            {
                new Vector3(min.X, min.Y, min.Z),
                new Vector3(max.X, min.Y, min.Z),
                new Vector3(min.X, max.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z),

                new Vector3(min.X, min.Y, max.Z),
                new Vector3(max.X, min.Y, max.Z),
                new Vector3(min.X, max.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 point =
                    inverse *
                    (mesh.GlobalTransform * corner);

                lowestY =
                    Mathf.Min(
                        lowestY,
                        point.Y
                    );
            }
        }

        if (lowestY == float.MaxValue)
            return;

        _modelSleepPosition =
    _modelStandingPosition +
    new Vector3(
        0f,
        -lowestY - 0.125f,
        0f
    );

        _sleepGroundPositionCalculated =
            true;
    }


    // =========================================================
    // ROTATION LERP
    // =========================================================

    private void LerpRotation(
        Node3D node,
        Vector3 from,
        Vector3 to,
        float t)
    {
        if (node == null)
            return;

        node.Rotation =
            new Vector3(
                Mathf.LerpAngle(
                    from.X,
                    to.X,
                    t
                ),

                Mathf.LerpAngle(
                    from.Y,
                    to.Y,
                    t
                ),

                Mathf.LerpAngle(
                    from.Z,
                    to.Z,
                    t
                )
            );
    }


    // =========================================================
    // WALKING
    // =========================================================

    private void UpdateWalkAnimation(
        float dt,
        Vector3 velocity)
    {
        if (_mobModel == null ||
            _frontRightLeg == null ||
            _frontLeftLeg == null ||
            _backRightLeg == null ||
            _backLeftLeg == null)
        {
            return;
        }

        bool walking =
            Mathf.Abs(velocity.X) > 0.05f ||
            Mathf.Abs(velocity.Z) > 0.05f;

        if (!walking)
        {
            ResetLeg(
                _frontRightLeg,
                dt
            );

            ResetLeg(
                _frontLeftLeg,
                dt
            );

            ResetLeg(
                _backRightLeg,
                dt
            );

            ResetLeg(
                _backLeftLeg,
                dt
            );

            return;
        }

        float speed =
            new Vector2(
                velocity.X,
                velocity.Z
            ).Length();

        float animationSpeed =
            Mathf.Clamp(
                speed /
                _moveSpeed,
                0.5f,
                2f
            );

        _walkAnimationTime +=
            dt *
            animationSpeed *
            7f;

        float swing =
            Mathf.Sin(
                _walkAnimationTime
            ) * 0.45f;

        _frontRightLeg.Rotation =
            new Vector3(
                swing,
                _frontRightLeg.Rotation.Y,
                _frontRightLeg.Rotation.Z
            );

        _backLeftLeg.Rotation =
            new Vector3(
                swing,
                _backLeftLeg.Rotation.Y,
                _backLeftLeg.Rotation.Z
            );

        _frontLeftLeg.Rotation =
            new Vector3(
                -swing,
                _frontLeftLeg.Rotation.Y,
                _frontLeftLeg.Rotation.Z
            );

        _backRightLeg.Rotation =
            new Vector3(
                -swing,
                _backRightLeg.Rotation.Y,
                _backRightLeg.Rotation.Z
            );
    }


    // =========================================================
    // RESET LEG
    // =========================================================

    private void ResetLeg(
        Node3D leg,
        float dt)
    {
        if (leg == null)
            return;

        leg.Rotation =
            new Vector3(
                Mathf.LerpAngle(
                    leg.Rotation.X,
                    0f,
                    dt * 8f
                ),

                leg.Rotation.Y,

                leg.Rotation.Z
            );
    }
}