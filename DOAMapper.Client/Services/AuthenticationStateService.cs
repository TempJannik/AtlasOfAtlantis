﻿using DOAMapper.Shared.Models.Authentication;
using DOAMapper.Shared.Services;

namespace DOAMapper.Client.Services;

public class AuthenticationStateService : IAuthenticationStateService
{
    private AuthenticationState _currentState = AuthenticationState.Unauthenticated;
    private readonly HashSet<string> _adminRoutes = new()
    {
        "/import",
        "/history"
    };

    public AuthenticationState CurrentState => _currentState;
    
    public event Action<AuthenticationState>? StateChanged;

    public void UpdateState(AuthenticationState newState)
    {
        _currentState = newState;
        StateChanged?.Invoke(_currentState);
    }

    public void NotifyStateChanged()
    {
        StateChanged?.Invoke(_currentState);
    }

    public bool RequiresAuthentication(string route)
    {
        // Only admin routes require authentication
        return _adminRoutes.Contains(route.ToLowerInvariant());
    }

    public bool RequiresAdminAccess(string route)
    {
        return _adminRoutes.Contains(route.ToLowerInvariant());
    }
}
