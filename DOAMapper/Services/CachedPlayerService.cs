using DOAMapper.Services.Interfaces;
using DOAMapper.Shared.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace DOAMapper.Services;

public class CachedPlayerService : IPlayerService
{
    private readonly IPlayerService _playerService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedPlayerService> _logger;

    // Cache durations for different types of data
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DetailsCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HistoryCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DatesCacheDuration = TimeSpan.FromHours(1);

    public CachedPlayerService(
        IPlayerService playerService,
        IMemoryCache cache,
        ILogger<CachedPlayerService> logger)
    {
        _playerService = playerService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<PlayerDto>> SearchPlayersAsync(string query, DateTime date, int page, int pageSize)
    {
        var cacheKey = $"players_search_{query?.ToLower() ?? ""}_{date:yyyyMMdd}_{page}_{pageSize}";
        
        if (_cache.TryGetValue(cacheKey, out PagedResult<PlayerDto>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for player search: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for player search: {CacheKey}", cacheKey);
        var result = await _playerService.SearchPlayersAsync(query, date, page, pageSize);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SearchCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<PlayerDetailDto?> GetPlayerAsync(string playerId, DateTime date)
    {
        var cacheKey = $"player_detail_{playerId}_{date:yyyyMMdd}";
        
        if (_cache.TryGetValue(cacheKey, out PlayerDetailDto? cachedResult))
        {
            _logger.LogDebug("Cache hit for player detail: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for player detail: {CacheKey}", cacheKey);
        var result = await _playerService.GetPlayerAsync(playerId, date);
        
        if (result != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DetailsCacheDuration,
                Size = EstimateSize(result)
            };
            
            _cache.Set(cacheKey, result, cacheOptions);
        }
        
        return result;
    }

    public async Task<List<TileDto>> GetPlayerTilesAsync(string playerId, DateTime date)
    {
        var cacheKey = $"player_tiles_{playerId}_{date:yyyyMMdd}";
        
        if (_cache.TryGetValue(cacheKey, out List<TileDto>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for player tiles: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for player tiles: {CacheKey}", cacheKey);
        var result = await _playerService.GetPlayerTilesAsync(playerId, date);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DetailsCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<List<HistoryEntryDto<PlayerDto>>> GetPlayerHistoryAsync(string playerId)
    {
        var cacheKey = $"player_history_{playerId}";
        
        if (_cache.TryGetValue(cacheKey, out List<HistoryEntryDto<PlayerDto>>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for player history: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for player history: {CacheKey}", cacheKey);
        var result = await _playerService.GetPlayerHistoryAsync(playerId);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = HistoryCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync()
    {
        const string cacheKey = "player_available_dates";
        
        if (_cache.TryGetValue(cacheKey, out List<DateTime>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for available dates: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for available dates: {CacheKey}", cacheKey);
        var result = await _playerService.GetAvailableDatesAsync();
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DatesCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    /// <summary>
    /// Estimates the memory size of an object for cache sizing
    /// </summary>
    private static long EstimateSize(object obj)
    {
        return obj switch
        {
            PagedResult<PlayerDto> paged => 1000 + (paged.Items?.Count ?? 0) * 500, // Base + items
            PlayerDetailDto => 2000, // Detailed player with tiles
            List<TileDto> tiles => 200 + tiles.Count * 300, // Base + tiles
            List<HistoryEntryDto<PlayerDto>> history => 500 + history.Count * 600, // Base + history entries
            List<DateTime> dates => 100 + dates.Count * 20, // Base + dates
            _ => 1000 // Default estimate
        };
    }
}
