namespace Shared.Entity;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}
