using Godot;
using System;

public partial class Player : CharacterBody3D
{
// =========================================================================
// DEATH / RESPAWN
// =========================================================================

private Control _deathPanel;
private CanvasLayer _deathLayer;

private Vector3 _deathPosition;
private bool _deathHandled = false;
private bool _deathScreenOpen = false;
private Label _deathLocationLabel;

// -------------------------------------------------------------------------
// INITIALIZE
// -------------------------------------------------------------------------

private void InitializeDeathSystem()
{
if (_stats == null)
return;

_stats.PlayerDied += OnPlayerDied;

BuildDeathPanel();

}

// -------------------------------------------------------------------------
// DEATH
// -------------------------------------------------------------------------

private void OnPlayerDied()
{
if (_deathHandled)
return;

_deathHandled = true;

// Remember exactly where the player died.
_deathPosition = GlobalPosition;

// Stop movement immediately.
Velocity = Vector3.Zero;

// Close anything that should not remain open during death.
CloseDeathRelatedMenus();

// Stop normal mouse/gameplay control.
Input.MouseMode = Input.MouseModeEnum.Visible;

ShowDeathPanel();

GD.Print(
    $"Player died at {_deathPosition}"
);

}

// -------------------------------------------------------------------------
// CLOSE MENUS
// -------------------------------------------------------------------------

private void CloseDeathRelatedMenus()
{
// Inventory
if (_inventoryOpen)
{
_inventoryOpen = false;

    if (_inventoryScreen != null)
        _inventoryScreen.Visible = false;

    if (_inventoryLayer != null)
        _inventoryLayer.Visible = false;
}

// Crafting
if (_craftingLayer != null)
    _craftingLayer.Visible = false;

// Equipment
if (_equipmentLayer != null)
    _equipmentLayer.Visible = false;

// Creative
if (_creativeMenuOpen)
{
    _creativeMenuOpen = false;

    if (_creativeMenuLayer != null)
        _creativeMenuLayer.Visible = false;
}

// Calendar
if (_calendarLayer != null)
    _calendarLayer.Visible = false;

// Tool bench / station UI
if (_toolBenchPanel != null)
    _toolBenchPanel.Visible = false;

if (_stationTabBar != null)
    _stationTabBar.Visible = false;

// Clear held inventory item.
if (_heldSlot != null)
    _heldSlot.Clear();

EndDrag();

UpdateCursorVisual();

}

// -------------------------------------------------------------------------
// DEATH PANEL
// -------------------------------------------------------------------------

private void BuildDeathPanel()
{
if (_deathLayer != null)
return;

_deathLayer = new CanvasLayer();
_deathLayer.Name = "DeathLayer";
_deathLayer.Layer = 100;

AddChild(_deathLayer);

_deathPanel = new Control();
_deathPanel.Name = "DeathPanel";

_deathPanel.SetAnchorsAndOffsetsPreset(
    Control.LayoutPreset.FullRect
);

_deathPanel.MouseFilter =
    Control.MouseFilterEnum.Stop;

_deathLayer.AddChild(_deathPanel);

// Dark full-screen background.
var background = new ColorRect();

background.SetAnchorsAndOffsetsPreset(
    Control.LayoutPreset.FullRect
);

background.Color =
    new Color(0f, 0f, 0f, 0.78f);

background.MouseFilter =
    Control.MouseFilterEnum.Stop;

_deathPanel.AddChild(background);

// Center container.
var center = new VBoxContainer();

center.SetAnchorsPreset(
    Control.LayoutPreset.Center
);

center.Position =
    new Vector2(-175f, -110f);

center.Size =
    new Vector2(350f, 220f);

center.Alignment =
    BoxContainer.AlignmentMode.Center;

center.AddThemeConstantOverride(
    "separation",
    18
);

_deathPanel.AddChild(center);

// Death title.
var title = new Label();

title.Text = "YOU DIED";

title.HorizontalAlignment =
    HorizontalAlignment.Center;

title.AddThemeFontSizeOverride(
    "font_size",
    42
);

center.AddChild(title);

// Death location.
_deathLocationLabel = new Label();

_deathLocationLabel.Name = "DeathLocation";
_deathLocationLabel.Text = "Death Location";

_deathLocationLabel.HorizontalAlignment =
    HorizontalAlignment.Center;

_deathLocationLabel.AddThemeFontSizeOverride(
    "font_size",
    16
);

center.AddChild(_deathLocationLabel);

// Respawn button.
var respawnButton = new Button();

respawnButton.Text = "Respawn";

respawnButton.CustomMinimumSize =
    new Vector2(260f, 50f);

respawnButton.Pressed += RespawnPlayer;

center.AddChild(respawnButton);

// Main menu button.
var menuButton = new Button();

menuButton.Text = "Return to Main Menu";

menuButton.CustomMinimumSize =
    new Vector2(260f, 50f);

menuButton.Pressed += ReturnToMainMenu;

center.AddChild(menuButton);

_deathPanel.Visible = false;
_deathLayer.Visible = false;

}

// -------------------------------------------------------------------------
// SHOW
// -------------------------------------------------------------------------

private void ShowDeathPanel()
{
if (_deathPanel == null)
BuildDeathPanel();

if (_deathLocationLabel != null)
{
    _deathLocationLabel.Text =
        $"Death Location\n" +
        $"X: {_deathPosition.X:F1}   " +
        $"Y: {_deathPosition.Y:F1}   " +
        $"Z: {_deathPosition.Z:F1}";
}

_deathPanel.Visible = true;
_deathLayer.Visible = true;

_deathScreenOpen = true;

Input.MouseMode =
    Input.MouseModeEnum.Visible;

}

// -------------------------------------------------------------------------
// RESPAWN
// -------------------------------------------------------------------------

private void RespawnPlayer()
{
if (!_deathScreenOpen)
return;

// Reset stats.
if (_stats != null)
{
    _stats.ResetAfterDeath();
}

// Respawn at the world origin.
Vector3 spawnPosition =
    GetRespawnPosition();

GlobalPosition = spawnPosition;

// Reset movement.
Velocity = Vector3.Zero;

_isSprinting = false;
_isCrouching = false;
_hasDoubleJumped = false;
_isGliding = false;
_isPlacing = false;
_isBreaking = false;
_isDropping = false;
_isFlying = false;

ResetBreak();

// Hide death screen.
_deathScreenOpen = false;
_deathHandled = false;

_deathPanel.Visible = false;
_deathLayer.Visible = false;

// Return control to the player.
Input.MouseMode =
    Input.MouseModeEnum.Captured;

GD.Print(
    $"Player respawned at {spawnPosition}"
);

}

// -------------------------------------------------------------------------
// RESPAWN LOCATION
// -------------------------------------------------------------------------

private Vector3 GetRespawnPosition()
{
    var chunkManager =
        GetTree().Root.FindChild(
            "ChunkManager",
            true,
            false
        ) as ChunkManager;

    if (chunkManager != null)
    {
        return chunkManager.GetWorldSpawnPosition();
    }

    // Safety fallback if ChunkManager cannot be found.
    return new Vector3(
        0.5f,
        2.5f,
        0.5f
    );
}

// -------------------------------------------------------------------------
// MAIN MENU
// -------------------------------------------------------------------------

private void ReturnToMainMenu()
{
if (GetTree().Paused)
GetTree().Paused = false;

// Save player inventory.
var cm =
    GetTree().Root.FindChild(
        "ChunkManager",
        true,
        false
    ) as ChunkManager;

if (cm != null)
{
    cm.SaveInventory(_inventory);

    cm.SavePlayerPosition(
        GlobalPosition,
        Rotation.Y,
        _playerCamera?.GetPitch() ?? 0f
    );
}

// Save the active world.
if (SaveManager.Instance != null)
{
    SaveManager.Instance.SaveCurrentWorld();
}

GD.Print(
    "[PlayerDeath] Game saved. Returning to Main Menu."
);

GetTree().ChangeSceneToFile(
    "res://Scenes/MainMenu.tscn"
);

}
}
