namespace src.dotnet.shared.Entity
{
    public class AuditLog : BaseEntity
    {
        public Guid UserId { get; set; }
        public string DeleteBy { get; set; }
        public DateOnly DeleteAt { get; set; }
        public string CreatedBy { get; set; }
        public DateOnly CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateOnly UpdatedAt { get; set; }
    
    }
}