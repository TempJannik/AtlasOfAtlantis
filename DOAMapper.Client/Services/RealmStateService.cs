using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Shared.Constants;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DOAMapper.Client.Services;

public class RealmStateService
{
    private readonly IJSRuntime _jsRuntime;
    private RealmDto? _selectedRealm;
    private List<RealmDto> _availableRealms = new();
    private const string SelectedRealmKey = "selectedRealmId";

    public event Action<RealmDto?>? RealmChanged;
    public event Action<List<RealmDto>>? AvailableRealmsChanged;

    public RealmStateService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public RealmDto? SelectedRealm
    {
        get => _selectedRealm;
        private set
        {
            if (_selectedRealm?.RealmId != value?.RealmId)
            {
                _selectedRealm = value;
                _ = SaveSelectedRealmToLocalStorageAsync();
                RealmChanged?.Invoke(_selectedRealm);
            }
        }
    }

    public List<RealmDto> AvailableRealms
    {
        get => _availableRealms;
        private set
        {
            _availableRealms = value;
            AvailableRealmsChanged?.Invoke(_availableRealms);

            // Try to restore previously selected realm from local storage
            _ = RestoreSelectedRealmFromLocalStorageAsync();
        }
    }

    public void SetAvailableRealms(List<RealmDto> realms)
    {
        AvailableRealms = realms;
    }

    public void SetSelectedRealm(RealmDto? realm)
    {
        SelectedRealm = realm;
    }

    public void SetSelectedRealmById(string realmId)
    {
        var realm = _availableRealms.FirstOrDefault(r => r.RealmId == realmId);
        if (realm != null)
        {
            SelectedRealm = realm;
        }
    }

    public bool IsRealmAvailable(string realmId)
    {
        return _availableRealms.Any(r => r.RealmId == realmId);
    }

    public RealmDto? GetRealmById(string realmId)
    {
        return _availableRealms.FirstOrDefault(r => r.RealmId == realmId);
    }

    public RealmDto? GetDefaultRealm()
    {
        return _availableRealms.FirstOrDefault(r => r.RealmId == RealmConstants.DefaultRealmId) ?? _availableRealms.FirstOrDefault();
    }

    public async Task InitializeFromLocalStorageAsync()
    {
        if (_availableRealms.Any())
        {
            await RestoreSelectedRealmFromLocalStorageAsync();
        }
    }

    private async Task SaveSelectedRealmToLocalStorageAsync()
    {
        try
        {
            if (_selectedRealm != null)
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SelectedRealmKey, _selectedRealm.RealmId);
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", SelectedRealmKey);
            }
        }
        catch (Exception)
        {
            // Ignore localStorage errors (e.g., during prerendering)
        }
    }

    private async Task RestoreSelectedRealmFromLocalStorageAsync()
    {
        try
        {
            var savedRealmId = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", SelectedRealmKey);

            if (!string.IsNullOrEmpty(savedRealmId) && _availableRealms.Any())
            {
                var savedRealm = _availableRealms.FirstOrDefault(r => r.RealmId == savedRealmId);
                if (savedRealm != null)
                {
                    SelectedRealm = savedRealm;
                    return;
                }
            }

            // Fallback: Auto-select the first realm if none is selected and no saved realm found
            if (_selectedRealm == null && _availableRealms.Any())
            {
                SelectedRealm = _availableRealms.First();
            }
        }
        catch (Exception)
        {
            // Ignore localStorage errors (e.g., during prerendering)
            // Fallback to first available realm
            if (_selectedRealm == null && _availableRealms.Any())
            {
                SelectedRealm = _availableRealms.First();
            }
        }
    }
}
