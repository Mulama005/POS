using Pos.Domain.Common;

namespace Pos.Domain.Entities;

public class Register : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    /// <summary>Full open/close history. Whether the till is currently open is derived
    /// from whether any of these has Status == Open — see TillSession for why this
    /// replaced a plain IsTillOpen boolean.</summary>
    public ICollection<TillSession> TillSessions { get; set; } = new List<TillSession>();
}