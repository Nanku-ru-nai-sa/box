// NEW FILE
// Put this anywhere in your Scripts folder, e.g. Scripts/Tools/ToolFamily.cs
//
// These two enums are shared by every tool-crafting script below.
// ToolFamily = what the tool DOES (pickaxe, axe, etc).
// PartSlot   = which physical spot on the tool a material is socketed into.

public enum ToolFamily
{
    Pickaxe,
    Axe,
    Shovel,
    Sword,
    Hoe,
    Hammer
}

public enum PartSlot
{
    HeadA,    // main head - every tool has this
    HeadB,    // second head - Tool Bench only, dual-head tools
    Handle,   // the stick/rod
    Binding   // Tool Bench only, wraps head to handle
}