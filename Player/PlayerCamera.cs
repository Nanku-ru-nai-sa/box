using Godot;

/// <summary>
/// Handles first person camera and mouse look
/// </summary>
public partial class PlayerCamera : Node3D
{
    // Mouse sensitivity
    [Export] public float MouseSensitivity { get; set; } = 0.002f;

    // Camera tilt limits (in radians)
    [Export] public float MinPitch { get; set; } = -1.5f;
    [Export] public float MaxPitch { get; set; } = 1.5f;

    // The actual camera node
    private Camera3D _camera;

    // Current pitch (up/down rotation)
    private float _pitch = 0f;

    // Is mouse captured
    private bool _mouseCaptured = false;

    public override void _Ready()
{
    // Find existing Camera3D child
    _camera = GetNodeOrNull<Camera3D>("Camera3D");

    if (_camera == null)
    {
        _camera = new Camera3D();
        AddChild(_camera);
    }

    // Set as current camera
    _camera.Current = true;

    // Set camera height (eye level)
    _camera.Position = new Vector3(0, 0, 0);

    // Capture mouse on start
    CaptureMouse();
}

    public override void _Input(InputEvent @event)
{
    // DEBUG
    if (@event is InputEventMouseMotion motion)
        GD.Print($"Mouse moved: {motion.Relative}");

    // ... rest of code
        // Mouse look
        if (@event is InputEventMouseMotion mouseMotion
            && _mouseCaptured)
        {
            // Left/right rotates the whole player body
            // We do this via the parent player node
            var player = GetParent<Player>();
            if (player != null)
            {
                player.RotateY(
                    -mouseMotion.Relative.X * MouseSensitivity);
            }

            // Up/down rotates only the camera
            _pitch -= mouseMotion.Relative.Y * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            Rotation = new Vector3(_pitch, 0, 0);
        }

        // Toggle mouse capture with Escape
        if (@event is InputEventKey keyEvent
            && keyEvent.Pressed
            && keyEvent.Keycode == Key.Escape)
        {
            if (_mouseCaptured)
                ReleaseMouse();
            else
                CaptureMouse();
        }
    }

    public void CaptureMouse()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _mouseCaptured = true;
    }

    public void ReleaseMouse()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _mouseCaptured = false;
    }

    public bool IsMouseCaptured => _mouseCaptured;

    // Get the camera's forward direction for raycasting
    public Vector3 GetCameraForward()
    {
        return -_camera.GlobalTransform.Basis.Z;
    }

    // Get the camera itself
    public Camera3D GetCamera()
    {
        return _camera;
    }
}