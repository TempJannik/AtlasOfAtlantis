using DOAMapper.Services.Interfaces;
using DOAMapper.Shared.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace DOAMapper.Services;

public class CachedAllianceService : IAllianceService
{
    private readonly IAllianceService _allianceService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedAllianceService> _logger;

    // Cache durations for different types of data
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DetailsCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HistoryCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DatesCacheDuration = TimeSpan.FromHours(1);

    public CachedAllianceService(
        IAllianceService allianceService,
        IMemoryCache cache,
        ILogger<CachedAllianceService> logger)
    {
        _allianceService = allianceService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<AllianceDto>> GetAlliancesAsync(DateTime date, int page, int pageSize)
    {
        var cacheKey = $"alliances_list_{date:yyyyMMdd}_{page}_{pageSize}";
        
        if (_cache.TryGetValue(cacheKey, out PagedResult<AllianceDto>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for alliance list: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for alliance list: {CacheKey}", cacheKey);
        var result = await _allianceService.GetAlliancesAsync(date, page, pageSize);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DetailsCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<PagedResult<AllianceDto>> SearchAlliancesAsync(string query, DateTime date, int page, int pageSize)
    {
        var cacheKey = $"alliances_search_{query?.ToLower() ?? ""}_{date:yyyyMMdd}_{page}_{pageSize}";
        
        if (_cache.TryGetValue(cacheKey, out PagedResult<AllianceDto>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for alliance search: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for alliance search: {CacheKey}", cacheKey);
        var result = await _allianceService.SearchAlliancesAsync(query, date, page, pageSize);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SearchCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<AllianceDto?> GetAllianceAsync(string allianceId, DateTime date)
    {
        var cacheKey = $"alliance_detail_{allianceId}_{date:yyyyMMdd}";
        
        if (_cache.TryGetValue(cacheKey, out AllianceDto? cachedResult))
        {
            _logger.LogDebug("Cache hit for alliance detail: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for alliance detail: {CacheKey}", cacheKey);
        var result = await _allianceService.GetAllianceAsync(allianceId, date);
        
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

    public async Task<PagedResult<PlayerDto>> GetAllianceMembersAsync(string allianceId, DateTime date, int page, int pageSize)
    {
        var cacheKey = $"alliance_members_{allianceId}_{date:yyyyMMdd}_{page}_{pageSize}";
        
        if (_cache.TryGetValue(cacheKey, out PagedResult<PlayerDto>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for alliance members: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for alliance members: {CacheKey}", cacheKey);
        var result = await _allianceService.GetAllianceMembersAsync(allianceId, date, page, pageSize);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DetailsCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<List<TileDto>> GetAllianceTilesAsync(string allianceId, DateTime date)
    {
        var cacheKey = $"alliance_tiles_{allianceId}_{date:yyyyMMdd}";
        
        if (_cache.TryGetValue(cacheKey, out List<TileDto>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for alliance tiles: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for alliance tiles: {CacheKey}", cacheKey);
        var result = await _allianceService.GetAllianceTilesAsync(allianceId, date);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DetailsCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<List<HistoryEntryDto<AllianceDto>>> GetAllianceHistoryAsync(string allianceId)
    {
        var cacheKey = $"alliance_history_{allianceId}";
        
        if (_cache.TryGetValue(cacheKey, out List<HistoryEntryDto<AllianceDto>>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for alliance history: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for alliance history: {CacheKey}", cacheKey);
        var result = await _allianceService.GetAllianceHistoryAsync(allianceId);
        
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
        const string cacheKey = "alliance_available_dates";
        
        if (_cache.TryGetValue(cacheKey, out List<DateTime>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for available dates: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for available dates: {CacheKey}", cacheKey);
        var result = await _allianceService.GetAvailableDatesAsync();
        
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
            PagedResult<AllianceDto> paged => 1000 + (paged.Items?.Count ?? 0) * 600, // Base + alliance items
            PagedResult<PlayerDto> paged => 1000 + (paged.Items?.Count ?? 0) * 500, // Base + player items
            AllianceDto => 1500, // Alliance with member count
            List<TileDto> tiles => 200 + tiles.Count * 300, // Base + tiles
            List<HistoryEntryDto<AllianceDto>> history => 500 + history.Count * 700, // Base + history entries
            List<DateTime> dates => 100 + dates.Count * 20, // Base + dates
            _ => 1000 // Default estimate
        };
    }
}
