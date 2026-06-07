using Godot;

public partial class PlayerCamera : Node3D
{
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
        GD.Print("PlayerCamera ready.");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion
            && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            GetParent<CharacterBody3D>()?.RotateY(
                -mouseMotion.Relative.X * MouseSensitivity);

            _pitch -= mouseMotion.Relative.Y * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            Rotation = new Vector3(_pitch, 0, 0);
        }
    }

    public Camera3D GetCamera() => _camera;
    public Vector3 GetCameraForward() => -_camera.GlobalTransform.Basis.Z;
}