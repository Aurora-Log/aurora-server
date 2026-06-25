namespace src.dotnet.shared.Entity
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
    }
}