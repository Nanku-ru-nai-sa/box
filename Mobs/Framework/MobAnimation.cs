using Godot;
using System.Collections.Generic;

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

    // Optional humanoid rig used by the player model and undead.
    private Node3D _humanoidModel;
    private Vector3 _humanoidModelRestRotation;
    private Node3D _lookTarget;
    private Node3D _humanoidHeadPivot;
    private Vector3 _humanoidHeadRestRotation;
    private Node3D _humanoidBodyPivot;
    private Vector3 _humanoidBodyRestRotation;
    private readonly List<(Node3D Node, Vector3 RestRotation)> _humanoidLeftArm = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _humanoidRightArm = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _humanoidLeftForearm = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _humanoidRightForearm = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _humanoidLeftLeg = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _humanoidRightLeg = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _humanoidLeftLowerLeg = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _humanoidRightLowerLeg = new();

    private float _walkAnimationTime = 0f;
    private float _moveSpeed = 2.5f;

    public bool IsCrawler { get; private set; }
    public bool IsFastCrawler { get; private set; }

    public void SetMoveSpeed(float speed)
    {
        _moveSpeed = Mathf.Max(0.1f, speed);
    }

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
        SetupHumanoidRig();

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
            bool hasHumanoidModel =
                _mobModel?.FindChild("player", true, false) != null;

            if (!hasHumanoidModel)
            {
                GD.PrintErr(
                    "[MobAnimation] Could not find all four animal leg nodes."
                );
            }
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

    public void SetLookTarget(Node3D target)
    {
        _lookTarget = target != null && GodotObject.IsInstanceValid(target)
            ? target
            : null;
    }

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
    // HUMANOID / UNDEAD RIG
    // =========================================================
    //
    // The player GLTF stores its body parts as separate meshes.
    // Build the same shoulder, elbow, hip and knee pivots used by
    // the player controller so this model can be animated by mobs.
    // =========================================================

    private void SetupHumanoidRig()
    {
        _humanoidModel =
            _mobModel?.FindChild("player", true, false) as Node3D;

        if (_humanoidModel == null)
            return;

        // Preserve the imported model orientation. Crawlers rotate the
        // complete rig from this root so every body part lies down together.
        _humanoidModelRestRotation = _humanoidModel.Rotation;

        Node3D leftBicep = FindHumanoidPart("left bicep");
        Node3D rightBicep = FindHumanoidPart("right bicep");
        Node3D leftHand = FindHumanoidPart("left hand");
        Node3D rightHand = FindHumanoidPart("right hand");
        Node3D leftLowerLeg = FindHumanoidPart("lower left leg");
        Node3D rightLowerLeg = FindHumanoidPart("lower right leg");

        Node3D leftArmBone = FindBonePart("left arm bone");
        Node3D rightArmBone = FindBonePart("right arm bone");
        Node3D leftHandBone = FindBonePart("left hand bone");
        Node3D rightHandBone = FindBonePart("right hand bone");
        Node3D leftLegBone = FindBonePart("left leg bone");
        Node3D rightLegBone = FindBonePart("right leg bone");
        Node3D leftLowerLegBone = FindBonePart("lower left leg bone");
        Node3D rightLowerLegBone = FindBonePart("lower right leg bone");

        Node3D boneLayer =
            _mobModel?.FindChild("bones", true, false) as Node3D;
        bool showBones = boneLayer != null && boneLayer.Visible;

        foreach (Node3D bonePart in new[]
        {
            leftArmBone, rightArmBone, leftHandBone, rightHandBone,
            leftLegBone, rightLegBone, leftLowerLegBone, rightLowerLegBone,
            FindBonePart("skull"), FindBonePart("ribs")
        })
        {
            if (bonePart != null)
                bonePart.Visible = showBones;
        }

        var leftThighs = new List<Node3D>();
        var rightThighs = new List<Node3D>();

        foreach (Node node in _humanoidModel.FindChildren("*", "Node3D", true, false))
        {
            if (node is not Node3D part)
                continue;

            string partName = part.Name.ToString().ToLowerInvariant();

            if (partName.Contains("left") && partName.Contains("thigh"))
                leftThighs.Add(part);
            else if (partName.Contains("right") && partName.Contains("thigh"))
                rightThighs.Add(part);
        }

        if (leftBicep == null || rightBicep == null ||
            leftHand == null || rightHand == null ||
            leftThighs.Count == 0 || rightThighs.Count == 0 ||
            leftLowerLeg == null || rightLowerLeg == null)
        {
            GD.PrintErr("[MobAnimation] Humanoid model is missing one or more named body parts.");
            _humanoidModel = null;
            return;
        }

        CreateHumanoidPivot("MobLeftShoulder", new Vector3(-0.25f, 1.5f, 0f),
            _humanoidLeftArm, null, leftBicep, leftArmBone);
        CreateHumanoidPivot("MobRightShoulder", new Vector3(0.25f, 1.5f, 0f),
            _humanoidRightArm, null, rightBicep, rightArmBone);

        CreateHumanoidPivot("MobLeftElbow", new Vector3(-0.34375f, 1.125f, 0f),
            _humanoidLeftForearm, _humanoidLeftArm[0].Node, leftHand, leftHandBone);
        CreateHumanoidPivot("MobRightElbow", new Vector3(0.34375f, 1.125f, 0f),
            _humanoidRightForearm, _humanoidRightArm[0].Node, rightHand, rightHandBone);

        leftThighs.Add(leftLegBone);
        rightThighs.Add(rightLegBone);

        CreateHumanoidPivot("MobLeftHip", new Vector3(-0.125f, 0.75f, 0f),
            _humanoidLeftLeg, null, leftThighs.ToArray());
        CreateHumanoidPivot("MobRightHip", new Vector3(0.125f, 0.75f, 0f),
            _humanoidRightLeg, null, rightThighs.ToArray());

        CreateHumanoidPivot("MobLeftKnee", new Vector3(-0.125f, 0.375f, 0f),
            _humanoidLeftLowerLeg, _humanoidLeftLeg[0].Node, leftLowerLeg, leftLowerLegBone);
        CreateHumanoidPivot("MobRightKnee", new Vector3(0.125f, 0.375f, 0f),
            _humanoidRightLowerLeg, _humanoidRightLeg[0].Node, rightLowerLeg, rightLowerLegBone);

        // Give the torso a small forward hunch around its actual centre.
        Node3D body = FindHumanoidPart("body");
        if (body is MeshInstance3D bodyMesh && bodyMesh.Mesh != null)
        {
            Vector3 bodyCenter =
                _humanoidModel.ToLocal(body.ToGlobal(bodyMesh.GetAabb().GetCenter()));

            _humanoidBodyPivot = new Node3D { Name = "MobBodyPivot" };
            _humanoidModel.AddChild(_humanoidBodyPivot);
            _humanoidBodyPivot.Position = bodyCenter;
            body.Reparent(_humanoidBodyPivot, true);
            Node3D ribs = FindBonePart("ribs");
            if (ribs != null)
                ribs.Reparent(_humanoidBodyPivot, true);
            _humanoidBodyRestRotation = _humanoidBodyPivot.Rotation;
        }

        // Keep the face together while giving the undead a slight,
        // permanently forward/downward head posture.
        Node3D head = FindHumanoidPart("head");
        if (head != null)
        {
            Node3D[] faceParts =
            {
                head,
                FindHumanoidPart("left eye"),
                FindHumanoidPart("left pupil"),
                FindHumanoidPart("right eye"),
                FindHumanoidPart("right pupil")
            };

            Vector3 center = Vector3.Zero;
            if (head is MeshInstance3D headMesh && headMesh.Mesh != null)
                center = headMesh.GetAabb().GetCenter();

            Vector3 centerInModel =
                _humanoidModel.ToLocal(head.ToGlobal(center));

            _humanoidHeadPivot = new Node3D { Name = "MobHeadPivot" };
            _humanoidModel.AddChild(_humanoidHeadPivot);
            _humanoidHeadPivot.Position = centerInModel;

            foreach (Node3D facePart in faceParts)
            {
                if (facePart != null && GodotObject.IsInstanceValid(facePart))
                    facePart.Reparent(_humanoidHeadPivot, true);
            }

            Node3D skull = FindBonePart("skull");
            if (skull != null)
                skull.Reparent(_humanoidHeadPivot, true);

            _humanoidHeadRestRotation = _humanoidHeadPivot.Rotation;
        }

        GD.Print("[MobAnimation] Humanoid walking rig created.");
    }

    private Node3D FindHumanoidPart(string partName)
    {
        return _humanoidModel?.FindChild(partName, true, false) as Node3D;
    }

    private Node3D FindBonePart(string partName)
    {
        // Bone meshes are reparented into the same pivots as their
        // matching flesh, so search the complete mob model after setup.
        return _mobModel?.FindChild(partName, true, false) as Node3D;
    }

    // Randomly removes flesh and/or the corresponding bones for one
    // undead instance. Bone meshes remain children of the same animated
    // pivots as the flesh, so every surviving piece stays attached.
    public void ApplyUndeadDamage(RandomNumberGenerator rng)
    {
        if (_humanoidModel == null || rng == null)
            return;

        Node3D leftUpper = FindHumanoidPart("left bicep");
        Node3D rightUpper = FindHumanoidPart("right bicep");
        Node3D leftHand = FindHumanoidPart("left hand");
        Node3D rightHand = FindHumanoidPart("right hand");
        Node3D leftLowerLeg = FindHumanoidPart("lower left leg");
        Node3D rightLowerLeg = FindHumanoidPart("lower right leg");
        Node3D body = FindHumanoidPart("body");

        var leftThighMeshes = new List<Node3D>();
        var rightThighMeshes = new List<Node3D>();
        foreach (Node node in _humanoidModel.FindChildren("*", "Node3D", true, false))
        {
            if (node is not Node3D part)
                continue;

            string partName = part.Name.ToString().ToLowerInvariant();
            if (partName.Contains("thigh") && partName.Contains("left"))
                leftThighMeshes.Add(part);
            else if (partName.Contains("thigh") && partName.Contains("right"))
                rightThighMeshes.Add(part);
        }

        Node3D leftArmBone = FindBonePart("left arm bone");
        Node3D rightArmBone = FindBonePart("right arm bone");
        Node3D leftHandBone = FindBonePart("left hand bone");
        Node3D rightHandBone = FindBonePart("right hand bone");
        Node3D leftLegBone = FindBonePart("left leg bone");
        Node3D rightLegBone = FindBonePart("right leg bone");
        Node3D leftLowerLegBone = FindBonePart("lower left leg bone");
        Node3D rightLowerLegBone = FindBonePart("lower right leg bone");
        Node3D skull = FindBonePart("skull");
        Node3D ribs = FindBonePart("ribs");

        // Small independent wounds, with a lower chance of losing an
        // entire limb. Missing an upper limb section also removes its hand.
        bool loseLeftArm = rng.Randf() < 0.09f;
        bool loseRightArm = rng.Randf() < 0.09f;
        // Each leg has a 10% chance to be completely missing.
        // Losing either leg creates a crawler; losing both creates a fast crawler.
        bool loseLeftLeg = rng.Randf() < 0.10f;
        bool loseRightLeg = rng.Randf() < 0.10f;
        IsCrawler = loseLeftLeg || loseRightLeg;
        IsFastCrawler = loseLeftLeg && loseRightLeg;
        // Give the head more visible variety: normal, skull-only, or
        // completely missing (including the skull).
        bool loseEntireHead = rng.Randf() < 0.04f;
        bool skullOnlyHead = !loseEntireHead && rng.Randf() < 0.09f;
        bool loseTorsoFlesh = rng.Randf() < 0.12f;

        bool leftArmFlesh = !loseLeftArm && rng.Randf() >= 0.24f;
        bool rightArmFlesh = !loseRightArm && rng.Randf() >= 0.24f;
        SetVisible(leftUpper, leftArmFlesh);
        SetVisible(leftHand, leftArmFlesh && rng.Randf() >= 0.28f);
        SetVisible(rightUpper, rightArmFlesh);
        SetVisible(rightHand, rightArmFlesh && rng.Randf() >= 0.28f);

        bool leftLegFlesh = !loseLeftLeg && rng.Randf() >= 0.16f;
        bool rightLegFlesh = !loseRightLeg && rng.Randf() >= 0.16f;
        foreach (Node3D thigh in leftThighMeshes)
            SetVisible(thigh, leftLegFlesh);
        foreach (Node3D thigh in rightThighMeshes)
            SetVisible(thigh, rightLegFlesh);
        SetVisible(leftLowerLeg, leftLegFlesh && rng.Randf() >= 0.20f);
        SetVisible(rightLowerLeg, rightLegFlesh && rng.Randf() >= 0.20f);

        // Bone hands and lower legs remain attached only when their
        // corresponding upper bone section exists.
        bool leftArmBoneVisible = !loseLeftArm && rng.Randf() >= 0.18f;
        bool rightArmBoneVisible = !loseRightArm && rng.Randf() >= 0.18f;
        bool leftLegBoneVisible = !loseLeftLeg && rng.Randf() >= 0.18f;
        bool rightLegBoneVisible = !loseRightLeg && rng.Randf() >= 0.18f;

        SetVisible(leftArmBone, leftArmBoneVisible);
        SetVisible(leftHandBone, leftArmBoneVisible && rng.Randf() >= 0.22f);
        SetVisible(rightArmBone, rightArmBoneVisible);
        SetVisible(rightHandBone, rightArmBoneVisible && rng.Randf() >= 0.22f);
        SetVisible(leftLegBone, leftLegBoneVisible);
        SetVisible(leftLowerLegBone, leftLegBoneVisible && rng.Randf() >= 0.22f);
        SetVisible(rightLegBone, rightLegBoneVisible);
        SetVisible(rightLowerLegBone, rightLegBoneVisible && rng.Randf() >= 0.22f);

        SetVisible(body, !loseTorsoFlesh);
        SetVisible(ribs, rng.Randf() >= 0.18f);

        Node3D head = FindHumanoidPart("head");
        bool showFleshHead = !loseEntireHead && !skullOnlyHead;
        SetVisible(head, showFleshHead);
        SetVisible(FindHumanoidPart("left eye"), showFleshHead);
        SetVisible(FindHumanoidPart("left pupil"), showFleshHead);
        SetVisible(FindHumanoidPart("right eye"), showFleshHead);
        SetVisible(FindHumanoidPart("right pupil"), showFleshHead);
        SetVisible(skull, !loseEntireHead && (skullOnlyHead || rng.Randf() >= 0.20f));

        GD.Print("[MobAnimation] Rolled random undead damage.");
    }

    private void SetVisible(Node3D part, bool visible)
    {
        if (part != null && GodotObject.IsInstanceValid(part))
            part.Visible = visible;
    }

    private void CreateHumanoidPivot(
        string pivotName,
        Vector3 jointPosition,
        List<(Node3D Node, Vector3 RestRotation)> animationParts,
        Node3D parentPivot,
        params Node3D[] meshes)
    {
        Node3D pivot = new Node3D { Name = pivotName };
        Node3D pivotParent = parentPivot ?? _humanoidModel;
        pivotParent.AddChild(pivot);

        Vector3 jointWorldPosition = _humanoidModel.ToGlobal(jointPosition);
        pivot.Position = pivotParent.ToLocal(jointWorldPosition);

        foreach (Node3D mesh in meshes)
        {
            if (mesh != null && GodotObject.IsInstanceValid(mesh))
                mesh.Reparent(pivot, true);
        }

        animationParts.Add((pivot, pivot.Rotation));
    }

    private void UpdateHumanoidWalkAnimation(float dt, Vector3 velocity)
    {
        if (_humanoidModel == null)
            return;

        float speed = new Vector2(velocity.X, velocity.Z).Length();
        bool walking = speed > 0.05f;

        if (walking)
        {
            float animationSpeed = Mathf.Clamp(speed / _moveSpeed, 0.45f, 1.5f);
            _walkAnimationTime += dt * animationSpeed * 5.2f;
        }

        float swing = walking ? Mathf.Sin(_walkAnimationTime) * 0.48f : 0f;

        // Crawlers rotate the complete humanoid root, not just the torso.
        // This keeps the head, arms, torso and surviving legs aligned as one
        // body while the limbs retain only subtle crawling movement.
        _humanoidModel.Rotation =
            _humanoidModelRestRotation +
            new Vector3(IsCrawler ? -1.38f : 0f, 0f, 0f);

        // Crawlers reach and pull with the same broad arm movement as
        // upright walkers; their legs simply trail behind their body.
        float armReach = 0.72f;
        SetHumanoidSwing(_humanoidLeftArm, armReach - swing * 0.12f);
        SetHumanoidSwing(_humanoidRightArm, armReach + 0.10f + swing * 0.10f);
        SetHumanoidSwing(_humanoidLeftForearm, 0.22f + Mathf.Max(0f, swing) * 0.18f);
        SetHumanoidSwing(_humanoidRightForearm, 0.28f + Mathf.Max(0f, -swing) * 0.18f);

        if (IsCrawler)
        {
            // The whole model is already lying down, so keep the legs nearly
            // straight in local space instead of twisting them independently.
            SetHumanoidSwing(_humanoidLeftLeg, 0f);
            SetHumanoidSwing(_humanoidRightLeg, 0f);
            SetHumanoidSwing(_humanoidLeftLowerLeg, 0.04f);
            SetHumanoidSwing(_humanoidRightLowerLeg, 0.04f);
        }
        else
        {
            // Normal undead use a slow, heavy, uneven walking gait.
            SetHumanoidSwing(_humanoidLeftLeg, walking ? swing : 0f);
            SetHumanoidSwing(_humanoidRightLeg, walking ? -swing : 0f);
            SetHumanoidSwing(_humanoidLeftLowerLeg,
                walking ? -Mathf.Max(0f, -swing) * 0.62f : 0f);
            SetHumanoidSwing(_humanoidRightLowerLeg,
                walking ? -Mathf.Max(0f, swing) * 0.62f : 0f);
        }

        if (_humanoidBodyPivot != null)
        {
            float bodySway = walking && !IsCrawler
                ? Mathf.Sin(_walkAnimationTime) * 0.025f
                : 0f;
            float bodyLean = IsCrawler ? 0f : -0.10f;
            _humanoidBodyPivot.Rotation =
                _humanoidBodyRestRotation +
                new Vector3(bodyLean, 0f, bodySway);
        }

        if (_humanoidHeadPivot != null)
        {
            float headWobble = walking && !IsCrawler
                ? Mathf.Sin(_walkAnimationTime * 0.5f) * 0.035f
                : 0f;

            float lookPitch = IsCrawler ? 1.38f : -0.20f;
            float lookYaw = 0f;

            if (_lookTarget != null && GodotObject.IsInstanceValid(_lookTarget))
            {
                Vector3 toTarget =
                    _lookTarget.GlobalPosition - _humanoidHeadPivot.GlobalPosition;

                if (toTarget.LengthSquared() > 0.0001f)
                {
                    Vector3 localDirection =
                        _humanoidHeadPivot.GlobalTransform.Basis.Inverse() *
                        toTarget.Normalized();

                    float horizontalLength =
                        new Vector2(localDirection.X, localDirection.Z).Length();

                    lookPitch = Mathf.Atan2(
                        localDirection.Y,
                        Mathf.Max(0.001f, horizontalLength)
                    );

                    lookYaw = Mathf.Atan2(
                        -localDirection.X,
                        -localDirection.Z
                    );

                    lookPitch = Mathf.Clamp(lookPitch, -0.85f, 1.15f);
                    lookYaw = Mathf.Clamp(lookYaw, -0.75f, 0.75f);
                }
            }

            Vector3 targetHeadRotation =
                _humanoidHeadRestRotation +
                new Vector3(
                    lookPitch + headWobble,
                    lookYaw,
                    walking ? swing * 0.035f : 0f
                );

            _humanoidHeadPivot.Rotation =
                _humanoidHeadPivot.Rotation.Lerp(
                    targetHeadRotation,
                    1f - Mathf.Exp(-8f * Mathf.Max(dt, 0f))
                );
        }
    }

    private void SetHumanoidSwing(
        List<(Node3D Node, Vector3 RestRotation)> parts,
        float xRotation)
    {
        foreach (var part in parts)
        {
            if (part.Node == null || !GodotObject.IsInstanceValid(part.Node))
                continue;

            part.Node.Rotation =
                part.RestRotation + new Vector3(xRotation, 0f, 0f);
        }
    }


    // =========================================================
    // WALKING
    // =========================================================

    private void UpdateWalkAnimation(
        float dt,
        Vector3 velocity)
    {
        if (_humanoidModel != null)
        {
            UpdateHumanoidWalkAnimation(dt, velocity);
            return;
        }

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