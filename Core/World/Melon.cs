using Godot;

public partial class Melon : RigidBody3D
{
    private MeshInstance3D _mesh;
    private CollisionShape3D _collision;

    private static Texture2D _topTexCache;
    private static Texture2D _sideTexCache;

    public override void _Ready()
    {
        Mass = 2f;
        LinearDamp = 1.5f;
        AngularDamp = 1.5f;
        CollisionLayer = 1;
        CollisionMask = 1;

        // Sphere collision so it rolls
        _collision = new CollisionShape3D();
        var sphere = new SphereShape3D();
        sphere.Radius = 0.45f;
        _collision.Shape = sphere;
        AddChild(_collision);

        // Cache textures
        if (_topTexCache == null)
            _topTexCache = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/melon_top.png");
        if (_sideTexCache == null)
            _sideTexCache = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/melon_side.png");

        // Build materials
        var topMat = new StandardMaterial3D();
        topMat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        topMat.AlbedoTexture = _topTexCache;

        var sideMat = new StandardMaterial3D();
        sideMat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        sideMat.AlbedoTexture = _sideTexCache;

        // Build mesh with correct per-face UVs
        float h = 0.45f;
        var arrayMesh = new ArrayMesh();

        // Top face
        AddFace(arrayMesh, topMat,
            new Vector3(-h,  h, -h),
            new Vector3( h,  h, -h),
            new Vector3( h,  h,  h),
            new Vector3(-h,  h,  h));

        // Bottom face
        AddFace(arrayMesh, sideMat,
            new Vector3(-h, -h,  h),
            new Vector3( h, -h,  h),
            new Vector3( h, -h, -h),
            new Vector3(-h, -h, -h));

        // Front face (z+)
        AddFace(arrayMesh, sideMat,
            new Vector3(-h,  h,  h),
            new Vector3( h,  h,  h),
            new Vector3( h, -h,  h),
            new Vector3(-h, -h,  h));

        // Back face (z-)
        AddFace(arrayMesh, sideMat,
            new Vector3( h,  h, -h),
            new Vector3(-h,  h, -h),
            new Vector3(-h, -h, -h),
            new Vector3( h, -h, -h));

        // Left face (x-)
        AddFace(arrayMesh, sideMat,
            new Vector3(-h,  h, -h),
            new Vector3(-h,  h,  h),
            new Vector3(-h, -h,  h),
            new Vector3(-h, -h, -h));

        // Right face (x+)
        AddFace(arrayMesh, sideMat,
            new Vector3( h,  h,  h),
            new Vector3( h,  h, -h),
            new Vector3( h, -h, -h),
            new Vector3( h, -h,  h));

        _mesh = new MeshInstance3D();
        _mesh.Mesh = arrayMesh;
        AddChild(_mesh);
    }

    private void AddFace(ArrayMesh mesh, StandardMaterial3D mat,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        st.SetUV(new Vector2(0, 0)); st.AddVertex(v0);
        st.SetUV(new Vector2(1, 0)); st.AddVertex(v1);
        st.SetUV(new Vector2(1, 1)); st.AddVertex(v2);

        st.SetUV(new Vector2(0, 0)); st.AddVertex(v0);
        st.SetUV(new Vector2(1, 1)); st.AddVertex(v2);
        st.SetUV(new Vector2(0, 1)); st.AddVertex(v3);

        st.GenerateNormals();
        st.Commit(mesh);

        int surfIdx = mesh.GetSurfaceCount() - 1;
        if (surfIdx >= 0)
            mesh.SurfaceSetMaterial(surfIdx, mat);
    }

    public void Break(Inventory inventory)
    {
        inventory.AddItem("melon", 1);
        GD.Print("Melon picked up!");
        QueueFree();
    }
}