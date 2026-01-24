namespace Commerce.Domain.Configuration;

/// <summary>
/// Configuration for inventory reservation limits
/// </summary>
public class InventoryConfiguration
{
    /// <summary>
    /// Maximum quantity that can be reserved in a single reservation
    /// </summary>
    public int MaxReservationQuantity { get; set; } = 100;
    
    /// <summary>
    /// Minimum quantity that can be reserved in a single reservation
    /// </summary>
    public int MinReservationQuantity { get; set; } = 1;
}
