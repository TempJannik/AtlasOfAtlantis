using DOAMapper.Services.Interfaces;
using DOAMapper.Shared.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace DOAMapper.Services;

public class CachedMapService : IMapService
{
    private readonly IMapService _mapService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedMapService> _logger;

    // Cache durations for different types of data
    private static readonly TimeSpan TileCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RegionCacheDuration = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan HistoryCacheDuration = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan StatisticsCacheDuration = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan DatesCacheDuration = TimeSpan.FromHours(1);

    public CachedMapService(
        IMapService mapService,
        IMemoryCache cache,
        ILogger<CachedMapService> logger)
    {
        _mapService = mapService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<TileDto>> GetRegionTilesAsync(int x1, int y1, int x2, int y2, DateTime date)
    {
        var cacheKey = $"region_tiles_{x1}_{y1}_{x2}_{y2}_{date:yyyyMMdd}";
        
        if (_cache.TryGetValue(cacheKey, out List<TileDto>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for region tiles: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for region tiles: {CacheKey}", cacheKey);
        var result = await _mapService.GetRegionTilesAsync(x1, y1, x2, y2, date);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = RegionCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<TileDto?> GetTileAsync(int x, int y, DateTime date)
    {
        var cacheKey = $"tile_{x}_{y}_{date:yyyyMMdd}";
        
        if (_cache.TryGetValue(cacheKey, out TileDto? cachedResult))
        {
            _logger.LogDebug("Cache hit for tile: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for tile: {CacheKey}", cacheKey);
        var result = await _mapService.GetTileAsync(x, y, date);
        
        if (result != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TileCacheDuration,
                Size = EstimateSize(result)
            };
            
            _cache.Set(cacheKey, result, cacheOptions);
        }
        
        return result;
    }

    public async Task<List<HistoryEntryDto<TileDto>>> GetTileHistoryAsync(int x, int y)
    {
        var cacheKey = $"tile_history_{x}_{y}";
        
        if (_cache.TryGetValue(cacheKey, out List<HistoryEntryDto<TileDto>>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for tile history: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for tile history: {CacheKey}", cacheKey);
        var result = await _mapService.GetTileHistoryAsync(x, y);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = HistoryCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<Dictionary<string, int>> GetTileStatisticsAsync(DateTime date)
    {
        var cacheKey = $"tile_statistics_{date:yyyyMMdd}";
        
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, int>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for tile statistics: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for tile statistics: {CacheKey}", cacheKey);
        var result = await _mapService.GetTileStatisticsAsync(date);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = StatisticsCacheDuration,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        return result;
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync()
    {
        const string cacheKey = "map_available_dates";
        
        if (_cache.TryGetValue(cacheKey, out List<DateTime>? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Cache hit for available dates: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for available dates: {CacheKey}", cacheKey);
        var result = await _mapService.GetAvailableDatesAsync();
        
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
            List<TileDto> tiles => 200 + tiles.Count * 300, // Base + tiles
            TileDto => 300, // Single tile
            List<HistoryEntryDto<TileDto>> history => 500 + history.Count * 400, // Base + history entries
            Dictionary<string, int> stats => 200 + stats.Count * 50, // Base + statistics entries
            List<DateTime> dates => 100 + dates.Count * 20, // Base + dates
            _ => 500 // Default estimate
        };
    }
}
