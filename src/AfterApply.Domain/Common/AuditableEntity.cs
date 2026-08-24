namespace AfterApply.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; protected set; }

    public DateTimeOffset UpdatedAt { get; protected set; }

    protected void Touch(DateTimeOffset now) => UpdatedAt = now;
}
