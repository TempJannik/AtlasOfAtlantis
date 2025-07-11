namespace DOAMapper.Models.Interfaces;

public interface ITemporalEntity
{
    Guid Id { get; set; }
    Guid ImportSessionId { get; set; }
    bool IsActive { get; set; }
    DateTime ValidFrom { get; set; }
    DateTime? ValidTo { get; set; }
}
