using Godot;

public partial class Player : CharacterBody3D
{
    // =========================================================================
    // BLOCK OUTLINE
    // =========================================================================

    private void UpdateBlockOutline()
    {
        if (!_rayCast.IsColliding())
        {
            _blockOutline.Visible = false;
            return;
        }

        var col = _rayCast.GetCollider() as Node;

        if (col == null || !col.HasMeta("chunk"))
        {
            _blockOutline.Visible = false;
            return;
        }

        Vector3 inside =
            _rayCast.GetCollisionPoint()
            - _rayCast.GetCollisionNormal() * 0.5f;

        _blockOutline.GlobalPosition = new Vector3(
            Mathf.Floor(inside.X),
            Mathf.Floor(inside.Y),
            Mathf.Floor(inside.Z)
        );

        _blockOutline.Visible = true;
    }

    // =========================================================================
    // BLOCK DROPS
    // =========================================================================

    private (string ItemId, int Count) GetDrop(string blockId)
    {
        if (BlockDropManager.Instance != null &&
            BlockDropManager.Instance.TryRollDrop(
                blockId,
                _dropRng,
                out string customId,
                out int customCount))
        {
            return (customId, customCount);
        }

        if (blockId == "grass_block")
            return ("dirt", 1);

        if (blockId == "wet_sand1" ||
            blockId == "wet_sand2")
        {
            return ("wet_sand2", 1);
        }

        return (blockId, 1);
    }

    // =========================================================================
    // BREAK BLOCK
    // =========================================================================

    private void TryBreakBlock()
    {
        if (!_rayCast.IsColliding())
        {
            ResetBreak();
            return;
        }

        var gm = GameModeManager.Instance;

        if (gm != null && gm.IsStory)
        {
            ResetBreak();
            return;
        }

        var col = _rayCast.GetCollider() as Node;

        // ---------------------------------------------------------
        // MELON
        // ---------------------------------------------------------

        if (col is Melon melon)
        {
            melon.Break(_inventory);
            ResetBreak();
            return;
        }

        // ---------------------------------------------------------
        // SUN / MOON
        // ---------------------------------------------------------

        if (col != null &&
            (col.Name == "SunCollision" ||
             col.Name == "MoonCollision"))
        {
            string celestialId =
                col.Name == "SunCollision"
                    ? "sun"
                    : "moon";

            if (_breakTargetBlockId != celestialId)
            {
                _breakOverlay?.ResetTarget();

                _breakHitCount = 0;
                _breakTargetBlockId = celestialId;

                _breakMiningPower = 1;
            }

            _breakHitCount++;

            Vector3 celestialPosition =
                ((Node3D)col).GlobalPosition;

            Vector3I animationPosition =
                new Vector3I(
                    Mathf.FloorToInt(celestialPosition.X),
                    Mathf.FloorToInt(celestialPosition.Y),
                    Mathf.FloorToInt(celestialPosition.Z)
                );

            float celestialSize =
                celestialId == "sun"
                    ? 16f
                    : 10f;

            _breakOverlay?.SetCelestialMode(
                celestialPosition,
                new Vector3(
                    celestialSize,
                    celestialSize,
                    celestialSize
                ),
                new Vector3(
                    0f,
                    Mathf.Pi / 4f,
                    0f
                )
            );

            bool celestialShouldBreak =
                _breakOverlay?.UpdateBreak(
                    animationPosition,
                    _breakHitCount,
                    _breakMiningPower
                )
                ??
                (
                    _breakHitCount *
                    _breakMiningPower >=
                    BlockBreakOverlay.TotalStages
                );

            if (celestialShouldBreak)
            {
                GD.Print(
                    $"[Player] BROKE CELESTIAL BODY: {celestialId}"
                );

                var dayNight =
                    GetTree().GetFirstNodeInGroup(
                        "day_night_cycle"
                    );

                if (dayNight is DayNightCycle cycle)
                {
                    cycle.BreakCelestialBody(
                        celestialId
                    );

                    // ---------------------------------------------------------
                    // CELESTIAL DROP
                    // ---------------------------------------------------------

                    string defaultDropItem =
                        celestialId == "sun"
                            ? "sun_shard"
                            : "moon_shard";

                    int dropCount = 1;
                    string dropItem = defaultDropItem;

                    var dropManager =
                        BlockDropManager.Instance;

                    if (dropManager != null)
                    {
                        var rng =
                            new RandomNumberGenerator();

                        rng.Randomize();

                        if (dropManager.TryRollDrop(
                            celestialId,
                            rng,
                            out string jsonDropItem,
                            out int jsonDropCount))
                        {
                            dropItem = jsonDropItem;
                            dropCount = jsonDropCount;

                            GD.Print(
                                $"[Player] Celestial JSON drop: " +
                                $"{dropCount}x {dropItem}"
                            );
                        }
                        else
                        {
                            dropCount = 0;

                            GD.Print(
                                $"[Player] Celestial drop roll failed: " +
                                $"{celestialId}"
                            );
                        }
                    }
                    else
                    {
                        dropCount = 1;

                        GD.PrintErr(
                            "[Player] BlockDropManager unavailable. " +
                            $"Using fallback drop: 1x {dropItem}"
                        );
                    }

                    if (dropCount > 0)
                    {
                        Vector3 dropPosition =
                            ((Node3D)col).GlobalPosition;

                        SpawnItemDrop(
                            dropItem,
                            dropCount,
                            dropPosition
                        );
                    }
                }

                ResetBreak();
            }

            return;
        }

        // ---------------------------------------------------------
        // MOB
        // ---------------------------------------------------------

        if (col is Mob mob)
        {
            mob.TakeDamage(
                UnarmedAttackDamage,
                GlobalPosition
            );

            ResetBreak();
            return;
        }

        // ---------------------------------------------------------
        // CHUNK
        // ---------------------------------------------------------

        if (col == null ||
            !col.HasMeta("chunk"))
        {
            ResetBreak();
            return;
        }

        Chunk chunk =
            (Chunk)col.GetMeta("chunk").AsGodotObject();

        Vector3 tPos =
            _rayCast.GetCollisionPoint()
            - _rayCast.GetCollisionNormal() * 0.5f;

        Vector3 lPos =
            tPos - chunk.GlobalPosition;

        int bx = Mathf.FloorToInt(lPos.X);
        int by = Mathf.FloorToInt(lPos.Y);
        int bz = Mathf.FloorToInt(lPos.Z);

        // ---------------------------------------------------------
        // FLOWERS / ROCKS ABOVE THE BLOCK
        // ---------------------------------------------------------

        BlockState above =
            chunk.GetBlock(
                bx,
                by + 1,
                bz
            );

        if (above.BlockId is
            "rose" or
            "clover" or
            "dandelion" or
            "rock_flint" or
            "rock_coal" or
            "rock_iron" or
            "rock_tin" or
            "rock_copper")
        {
            Vector3 aboveWorldPos =
                chunk.GlobalPosition +
                new Vector3(
                    bx + 0.5f,
                    by + 1.5f,
                    bz + 0.5f
                );

            if (above.BlockId == "rose")
                SpawnItemDrop(
                    "rose",
                    1,
                    aboveWorldPos
                );

            if (above.BlockId == "dandelion")
                SpawnItemDrop(
                    "dandelion",
                    1,
                    aboveWorldPos
                );

            if (above.BlockId.StartsWith("rock_"))
                SpawnItemDrop(
                    above.BlockId,
                    1,
                    aboveWorldPos
                );

            chunk.SetBlock(
                bx,
                by + 1,
                bz,
                BlockState.Air
            );

            ResetBreak();
            return;
        }

        // ---------------------------------------------------------
        // GET TARGET BLOCK
        // ---------------------------------------------------------

        BlockState b =
            chunk.GetBlock(
                bx,
                by,
                bz
            );

        if (b.IsAir())
        {
            ResetBreak();
            return;
        }

        // ---------------------------------------------------------
        // BEDROCK
        // ---------------------------------------------------------

        if (b.BlockId == "bedrock" &&
            gm != null &&
            gm.IsSurvival)
        {
            ResetBreak();
            return;
        }

        // ---------------------------------------------------------
        // BOMB
        // ---------------------------------------------------------

        if (b.BlockId == "bomb")
        {
            Vector3I bombWorldPosition =
                new Vector3I(
                    Mathf.FloorToInt(
                        chunk.GlobalPosition.X
                    ) + bx,

                    Mathf.FloorToInt(
                        chunk.GlobalPosition.Y
                    ) + by,

                    Mathf.FloorToInt(
                        chunk.GlobalPosition.Z
                    ) + bz
                );

            chunk.SetBlock(
                bx,
                by,
                bz,
                BlockState.Air
            );

            ExplodeBomb(
                bombWorldPosition,
                3
            );

            ResetBreak();
            return;
        }

        // ---------------------------------------------------------
        // CREATIVE
        // ---------------------------------------------------------

        if (gm != null && gm.IsCreate)
        {
            var (dropIdC, dropCountC) =
                GetDrop(b.BlockId);

            if (dropIdC is not
                ("rose" or "dandelion" or "clover"))
            {
                AddItemToInventory(
                    dropIdC,
                    dropCountC
                );
            }

            var oreC =
                OreRegistry.Instance?.GetOreFromBlockState(b);

            if (oreC != null)
            {
                AddItemToInventory(
                    oreC.ItemId,
                    1
                );
            }

            chunk.SetBlock(
                bx,
                by,
                bz,
                BlockState.Air
            );

            ResetBreak();
            return;
        }

        // ---------------------------------------------------------
        // NORMAL BREAKING / MINING
        // ---------------------------------------------------------

        var blockWorldPos =
            new Vector3I(
                Mathf.FloorToInt(tPos.X),
                Mathf.FloorToInt(tPos.Y),
                Mathf.FloorToInt(tPos.Z)
            );

        if (blockWorldPos != _breakTargetBlock ||
            b.BlockId != _breakTargetBlockId)
        {
            _breakOverlay?.ResetTarget();

            _breakHitCount = 0;

            _breakTargetBlock =
                blockWorldPos;

            _breakTargetBlockId =
                b.BlockId;

            string heldItem =
                _inventory.Slots[
                    MainInvSize + _selectedSlot
                ].IsEmpty
                    ? ""
                    : _inventory.Slots[
                        MainInvSize + _selectedSlot
                    ].ItemId;

            _breakMiningPower =
                ToolDefinition.GetEffectiveMiningPower(
                    b.BlockId,
                    heldItem
                );
        }

        _breakHitCount++;

        bool shouldBreak =
            _breakOverlay?.UpdateBreak(
                blockWorldPos,
                _breakHitCount,
                _breakMiningPower
            )
            ??
            (
                _breakHitCount *
                _breakMiningPower >=
                BlockBreakOverlay.TotalStages
            );

        if (shouldBreak)
        {
            var (drop, dropCount) =
                GetDrop(b.BlockId);

            if (drop is not
                ("rose" or "dandelion" or "clover"))
            {
                Vector3 dropWorldPos =
                    chunk.GlobalPosition +
                    new Vector3(
                        bx + 0.5f,
                        by + 0.5f,
                        bz + 0.5f
                    );

                SpawnItemDrop(
                    drop,
                    dropCount,
                    dropWorldPos
                );

                var ore =
                    OreRegistry.Instance?
                        .GetOreFromBlockState(b);

                if (ore != null)
                {
                    SpawnItemDrop(
                        ore.ItemId,
                        1,
                        dropWorldPos
                    );
                }
            }

            chunk.SetBlock(
                bx,
                by,
                bz,
                BlockState.Air
            );

            DamageHeldToolDurability();

            ResetBreak();
        }
    }

    // =========================================================================
    // BOMB
    // =========================================================================

    private void ExplodeBomb(
        Vector3I center,
        int radius = 3)
    {
        ChunkManager chunkManager =
            GetTree().GetFirstNodeInGroup(
                "chunk_manager"
            ) as ChunkManager;

        if (chunkManager == null)
        {
            GD.PrintErr(
                "[Bomb] Could not find ChunkManager."
            );

            return;
        }

        int radiusSquared =
            radius * radius;

        for (
            int x = center.X - radius;
            x <= center.X + radius;
            x++)
        {
            for (
                int y = center.Y - radius;
                y <= center.Y + radius;
                y++)
            {
                for (
                    int z = center.Z - radius;
                    z <= center.Z + radius;
                    z++)
                {
                    int dx =
                        x - center.X;

                    int dy =
                        y - center.Y;

                    int dz =
                        z - center.Z;

                    if (
                        (dx * dx) +
                        (dy * dy) +
                        (dz * dz)
                        > radiusSquared)
                    {
                        continue;
                    }

                    Vector3I worldPos =
                        new Vector3I(
                            x,
                            y,
                            z
                        );

                    BlockState block =
                        chunkManager.GetBlockAtWorld(
                            worldPos
                        );

                    if (block.IsAir())
                        continue;

                    if (block.BlockId == "bedrock")
                        continue;

                    chunkManager.SetBlockAtWorld(
                        worldPos,
                        BlockState.Air
                    );
                }
            }
        }

        GD.Print(
            $"[Bomb] Explosion at {center} radius {radius}"
        );
    }

    // =========================================================================
    // TOOL DURABILITY
    // =========================================================================

    private void DamageHeldToolDurability()
    {
        var slot =
            _inventory.Slots[
                MainInvSize + _selectedSlot
            ];

        if (slot.IsEmpty)
            return;

        var item =
            ItemRegistry.Instance.GetItem(
                slot.ItemId
            );

        if (item == null ||
            !item.HasDurability)
        {
            return;
        }

        slot.CurrentDurability--;

        if (slot.CurrentDurability <= 0)
            slot.Clear();

        FireChanged();
        RefreshAllSlotVisuals();
    }

    // =========================================================================
    // BREAK STATE
    // =========================================================================

    private void ResetBreak()
    {
        _breakOverlay?.HideOverlay();

        _breakHitCount = 0;

        _breakTargetBlock =
            new Vector3I(
                int.MinValue,
                0,
                0
            );

        _breakTargetBlockId = "";
    }

    private void SyncBreakOverlayPosition()
    {
        if (!_rayCast.IsColliding())
            return;

        var col =
            _rayCast.GetCollider() as Node;

        if (col == null ||
            !col.HasMeta("chunk"))
        {
            return;
        }

        Vector3 tPos =
            _rayCast.GetCollisionPoint()
            - _rayCast.GetCollisionNormal() * 0.5f;

        _breakOverlay?.SyncPosition(
            new Vector3I(
                Mathf.FloorToInt(tPos.X),
                Mathf.FloorToInt(tPos.Y),
                Mathf.FloorToInt(tPos.Z)
            )
        );
    }

    // =========================================================================
    // PLACE BLOCK
    // =========================================================================

    private void TryPlaceBlock()
    {
        if (!_rayCast.IsColliding() ||
            string.IsNullOrEmpty(_selectedBlockId))
        {
            return;
        }

        var gm =
            GameModeManager.Instance;

        if (gm != null && gm.IsStory)
            return;

        bool consume =
            gm == null || !gm.IsCreate;

        if (consume &&
            !_inventory.HasItem(
                _selectedBlockId,
                1))
        {
            return;
        }

        if (!BlockRegistry.Instance.BlockExists(
            _selectedBlockId))
        {
            ShowFeedback(
                $"{_selectedBlockId} can't be placed"
            );

            return;
        }

        var col =
            _rayCast.GetCollider() as Node;

        if (col == null ||
            !col.HasMeta("chunk"))
        {
            return;
        }

        Chunk hitChunk =
            (Chunk)col.GetMeta(
                "chunk"
            ).AsGodotObject();

        Vector3 worldTarget =
            _rayCast.GetCollisionPoint()
            + _rayCast.GetCollisionNormal() * 0.5f;

        Vector3 center =
            new(
                Mathf.Floor(worldTarget.X) + 0.5f,
                Mathf.Floor(worldTarget.Y) + 0.5f,
                Mathf.Floor(worldTarget.Z) + 0.5f
            );

        if (center.DistanceTo(GlobalPosition) < 0.9f)
            return;

        var cm =
            hitChunk.GetParent() as ChunkManager;

        if (cm == null)
            return;

        Chunk tc =
            cm.GetChunk(
                cm.WorldToChunk(worldTarget)
            );

        if (tc == null)
            return;

        Vector3 lp =
            worldTarget - tc.GlobalPosition;

        tc.SetBlock(
            Mathf.FloorToInt(lp.X),
            Mathf.FloorToInt(lp.Y),
            Mathf.FloorToInt(lp.Z),
            new BlockState
            {
                BlockId = _selectedBlockId,
                BitMask = 0xFF
            }
        );

        if (consume)
        {
            _inventory.RemoveItem(
                _selectedBlockId,
                1
            );
        }
    }

    // =========================================================================
    // PICK BLOCK
    // =========================================================================

    private void PickBlock()
    {
        // Don't pick through UI.
        if (_inventoryOpen ||
            _chatOpen ||
            _creativeMenuOpen)
        {
            return;
        }

        if (_inventory == null ||
            _inventory.Slots == null)
        {
            return;
        }

        // Must be looking at an actual chunk block.
        if (!_rayCast.IsColliding())
            return;

        var col =
            _rayCast.GetCollider() as Node;

        if (col == null ||
            !col.HasMeta("chunk"))
        {
            return;
        }

        Chunk chunk =
            (Chunk)col.GetMeta(
                "chunk"
            ).AsGodotObject();

        if (chunk == null)
            return;

        // Find the block we're looking at.
        Vector3 targetPos =
            _rayCast.GetCollisionPoint()
            - _rayCast.GetCollisionNormal() * 0.5f;

        Vector3 localPos =
            targetPos - chunk.GlobalPosition;

        int bx =
            Mathf.FloorToInt(localPos.X);

        int by =
            Mathf.FloorToInt(localPos.Y);

        int bz =
            Mathf.FloorToInt(localPos.Z);

        BlockState block =
            chunk.GetBlock(
                bx,
                by,
                bz
            );

        if (block.IsAir())
            return;

        string blockId =
            block.BlockId;

        if (string.IsNullOrEmpty(blockId))
            return;

        // ---------------------------------------------------------
        // FIRST: HOTBAR
        // ---------------------------------------------------------

        for (int i = 0; i < HotbarSize; i++)
        {
            int inventoryIndex =
                MainInvSize + i;

            var slot =
                _inventory.Slots[
                    inventoryIndex
                ];

            if (slot != null &&
                !slot.IsEmpty &&
                slot.ItemId == blockId)
            {
                SelectHotbarSlot(i);
                RefreshAllSlotVisuals();
                return;
            }
        }

        // ---------------------------------------------------------
        // SECOND: MAIN INVENTORY
        // ---------------------------------------------------------

        for (int i = 0; i < MainInvSize; i++)
        {
            var slot =
                _inventory.Slots[i];

            if (slot == null ||
                slot.IsEmpty ||
                slot.ItemId != blockId)
            {
                continue;
            }

            int hotbarIndex =
                MainInvSize + _selectedSlot;

            var hotbarSlot =
                _inventory.Slots[
                    hotbarIndex
                ];

            (
                hotbarSlot.ItemId,
                slot.ItemId
            ) =
            (
                slot.ItemId,
                hotbarSlot.ItemId
            );

            (
                hotbarSlot.Count,
                slot.Count
            ) =
            (
                slot.Count,
                hotbarSlot.Count
            );

            (
                hotbarSlot.CurrentDurability,
                slot.CurrentDurability
            ) =
            (
                slot.CurrentDurability,
                hotbarSlot.CurrentDurability
            );

            SelectHotbarSlot(
                _selectedSlot
            );

            FireChanged();
            RefreshAllSlotVisuals();

            return;
        }

        // ---------------------------------------------------------
        // CREATE MODE
        // ---------------------------------------------------------

        var gm =
            GameModeManager.Instance;

        if (gm != null && gm.IsCreate)
        {
            if (!BlockRegistry.Instance.BlockExists(
                blockId))
            {
                return;
            }

            int hotbarIndex =
                MainInvSize + _selectedSlot;

            var hotbarSlot =
                _inventory.Slots[
                    hotbarIndex
                ];

            hotbarSlot.ItemId =
                blockId;

            hotbarSlot.Count =
                _inventory.MaxStackSize;

            SelectHotbarSlot(
                _selectedSlot
            );

            FireChanged();
            RefreshAllSlotVisuals();
        }
    }
}