namespace BillaresPos.Domain.Inventory;

public enum InventoryMovementType
{
    InitialStock = 1,
    Purchase = 2,
    Sale = 3,
    Return = 4,
    Waste = 5,
    Adjustment = 6,
    PhysicalCountAdjustment = 7
}

