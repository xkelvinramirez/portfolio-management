namespace Application.Common.Security;

public interface ICurrentUserProvider
{
    long UserId { get; }
}
