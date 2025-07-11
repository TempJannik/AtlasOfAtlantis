using DOAMapper.Shared.Models.Authentication;

namespace DOAMapper.Shared.Services;

public interface IAuthenticationStateService
{
    AuthenticationState CurrentState { get; }
    event Action<AuthenticationState>? StateChanged;
    void UpdateState(AuthenticationState newState);
    void NotifyStateChanged();
    bool RequiresAuthentication(string route);
    bool RequiresAdminAccess(string route);
}
