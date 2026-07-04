using Godot;

public partial class PlayerCamera : Node3D
{
    // These exports are fallbacks if SettingsManager isn't loaded yet
    [Export] public float MouseSensitivity { get; set; } = 0.002f;
    [Export] public float MinPitch { get; set; } = -1.5f;
    [Export] public float MaxPitch { get; set; } = 1.5f;

    private Camera3D _camera;
    private float _pitch = 0f;

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera3D>("Camera3D");

        if (_camera == null)
        {
            _camera = new Camera3D();
            AddChild(_camera);
        }

        _camera.Current = true;
        _camera.Position = Vector3.Zero;

        // Apply saved FOV if SettingsManager is available
        if (SettingsManager.Instance != null)
            _camera.Fov = SettingsManager.Instance.Fov;

        GD.Print("PlayerCamera ready.");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion
            && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            // Convert 0-100 slider value to a usable multiplier (0 = stopped, 100 = fast)
            float stored = SettingsManager.Instance?.MouseSensitivity ?? 50f;
            float sens = (stored / 100f) * 0.003f; // max 0.003 feels like a fast but sane ceiling

            GetParent<CharacterBody3D>()?.RotateY(
                -mouseMotion.Relative.X * sens);

            _pitch -= mouseMotion.Relative.Y * sens;
            _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            Rotation = new Vector3(_pitch, 0, 0);
        }
    }

    // Called by SettingsManager when FOV changes at runtime
    public void RefreshFov()
    {
        if (_camera != null && SettingsManager.Instance != null)
            _camera.Fov = SettingsManager.Instance.Fov;
    }

    public Camera3D GetCamera() => _camera;
    public Vector3 GetCameraForward() => -_camera.GlobalTransform.Basis.Z;
}