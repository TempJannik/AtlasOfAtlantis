# API Design & Backend Services

## RESTful API Endpoints

### Data Import API

#### Upload JSON Data
```http
POST /api/import/upload
Content-Type: multipart/form-data

Parameters:
- file: JSON file (max 100MB)

Response:
{
  "sessionId": "uuid",
  "status": "Processing",
  "message": "Import started successfully"
}
```

#### Get Import Status
```http
GET /api/import/status/{sessionId}

Response:
{
  "id": "uuid",
  "importDate": "2024-01-15T10:30:00Z",
  "fileName": "map_data.json",
  "status": "Completed",
  "recordsProcessed": 125000,
  "recordsChanged": 1250,
  "progressPercentage": 100
}
```

#### Get Import History
```http
GET /api/import/history?page=1&size=20

Response:
{
  "items": [...],
  "totalCount": 45,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

### Historical Data API

#### Get Available Dates
```http
GET /api/dates/available

Response:
[
  "2024-01-15T00:00:00Z",
  "2024-01-10T00:00:00Z",
  "2024-01-05T00:00:00Z"
]
```

#### Get Data Snapshot
```http
GET /api/data/snapshot/{date}

Response:
{
  "date": "2024-01-15T00:00:00Z",
  "playerCount": 15420,
  "allianceCount": 245,
  "tileDistribution": {
    "City": 15420,
    "Mountain": 45000,
    "Forest": 38000
  }
}
```

### Player API

#### Search Players
```http
GET /api/players/search?query=senna&date=2024-01-15&page=1&size=20

Response:
{
  "items": [
    {
      "playerId": "6109",
      "name": "Senna",
      "cityName": "Goob City",
      "might": 5145442,
      "allianceId": "18",
      "allianceName": "Elite Warriors",
      "dataDate": "2024-01-15T00:00:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

#### Get Player Details
```http
GET /api/players/{playerId}?date=2024-01-15

Response:
{
  "playerId": "6109",
  "name": "Senna",
  "cityName": "Goob City",
  "might": 5145442,
  "allianceId": "18",
  "allianceName": "Elite Warriors",
  "alliance": {
    "allianceId": "18",
    "name": "Elite Warriors",
    "overlordName": "Commander",
    "power": 125000000
  },
  "tileCount": 45,
  "tilesByType": {
    "City": 1,
    "Mountain": 15,
    "Forest": 20,
    "Lake": 9
  },
  "dataDate": "2024-01-15T00:00:00Z"
}
```

#### Get Player Tiles
```http
GET /api/players/{playerId}/tiles?date=2024-01-15

Response:
[
  {
    "x": 125,
    "y": 340,
    "type": "City",
    "level": 20,
    "playerId": "6109",
    "playerName": "Senna",
    "allianceId": "18",
    "allianceName": "Elite Warriors",
    "dataDate": "2024-01-15T00:00:00Z"
  }
]
```

#### Get Player History
```http
GET /api/players/{playerId}/history

Response:
[
  {
    "data": {
      "playerId": "6109",
      "name": "Senna",
      "might": 5145442
    },
    "validFrom": "2024-01-15T00:00:00Z",
    "validTo": null,
    "changeType": "Modified"
  }
]
```

### Alliance API

#### Get Alliances
```http
GET /api/alliances?date=2024-01-15&page=1&size=20

Response:
{
  "items": [
    {
      "allianceId": "18",
      "name": "Elite Warriors",
      "overlordName": "Commander",
      "power": 125000000,
      "fortressLevel": 10,
      "fortressX": 400,
      "fortressY": 300,
      "memberCount": 45,
      "dataDate": "2024-01-15T00:00:00Z"
    }
  ],
  "totalCount": 245,
  "page": 1,
  "pageSize": 20,
  "totalPages": 13
}
```

#### Get Alliance Details
```http
GET /api/alliances/{allianceId}?date=2024-01-15

Response:
{
  "allianceId": "18",
  "name": "Elite Warriors",
  "overlordName": "Commander",
  "power": 125000000,
  "fortressLevel": 10,
  "fortressX": 400,
  "fortressY": 300,
  "memberCount": 45,
  "dataDate": "2024-01-15T00:00:00Z"
}
```

#### Get Alliance Members
```http
GET /api/alliances/{allianceId}/members?date=2024-01-15

Response:
[
  {
    "playerId": "6109",
    "name": "Senna",
    "cityName": "Goob City",
    "might": 5145442,
    "allianceId": "18",
    "allianceName": "Elite Warriors",
    "dataDate": "2024-01-15T00:00:00Z"
  }
]
```

### Map API

#### Get Region Tiles
```http
GET /api/map/region?x1=100&y1=100&x2=200&y2=200&date=2024-01-15

Response:
[
  {
    "x": 125,
    "y": 150,
    "type": "City",
    "level": 15,
    "playerId": "6109",
    "playerName": "Senna",
    "allianceId": "18",
    "allianceName": "Elite Warriors"
  }
]
```

## Service Layer Architecture

### Core Services

#### IImportService
```csharp
public interface IImportService
{
    Task<ImportSession> StartImportAsync(Stream jsonStream, string fileName);
    Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId);
    Task<List<ImportSessionDto>> GetImportHistoryAsync();
    Task<List<DateTime>> GetAvailableImportDatesAsync();
}
```

#### IPlayerService
```csharp
public interface IPlayerService
{
    Task<PagedResult<PlayerDto>> SearchPlayersAsync(string query, DateTime date, int page, int size);
    Task<PlayerDetailDto> GetPlayerAsync(string playerId, DateTime date);
    Task<List<TileDto>> GetPlayerTilesAsync(string playerId, DateTime date);
    Task<List<PlayerHistoryDto>> GetPlayerHistoryAsync(string playerId);
}
```

#### IAllianceService
```csharp
public interface IAllianceService
{
    Task<PagedResult<AllianceDto>> GetAlliancesAsync(DateTime date, int page, int size);
    Task<AllianceDetailDto> GetAllianceAsync(string allianceId, DateTime date);
    Task<List<PlayerDto>> GetAllianceMembersAsync(string allianceId, DateTime date);
    Task<List<AllianceHistoryDto>> GetAllianceHistoryAsync(string allianceId);
}
```

#### ITemporalQueryService
```csharp
public interface ITemporalQueryService
{
    IQueryable<T> GetDataAsOf<T>(DateTime date) where T : ITemporalEntity;
    Task<List<HistoryEntry<T>>> GetHistoryAsync<T>(string entityId, DateTime? fromDate = null, DateTime? toDate = null) where T : ITemporalEntity;
}
```

### Data Transfer Objects (DTOs)

#### PlayerDto
```csharp
public class PlayerDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public long Might { get; set; }
    public string? AllianceId { get; set; }
    public string? AllianceName { get; set; }
    public DateTime DataDate { get; set; }
}
```

#### PagedResult<T>
```csharp
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
```

## Error Handling

### Standard Error Response
```json
{
  "error": {
    "code": "PLAYER_NOT_FOUND",
    "message": "Player with ID '12345' not found for date 2024-01-15",
    "details": {
      "playerId": "12345",
      "date": "2024-01-15T00:00:00Z"
    }
  }
}
```

### HTTP Status Codes
- `200 OK`: Successful request
- `400 Bad Request`: Invalid parameters or request format
- `404 Not Found`: Resource not found
- `422 Unprocessable Entity`: Validation errors
- `500 Internal Server Error`: Server errors

## Authentication & Authorization

### Public Access
- All endpoints are publicly accessible
- No authentication required for current implementation
- Rate limiting may be added in future versions

### Future Considerations
- API key authentication for third-party access
- Role-based access control for administrative functions
- OAuth integration for user accounts
