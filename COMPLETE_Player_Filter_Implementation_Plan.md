# COMPLETE Players Filter Implementation Plan (Dragons of Atlantis)

## 1) Objectives and scope
- Build a full-featured Players filter that:
  - Operates per selected Realm and Import Date (temporal snapshot)
  - Filters across the entire dataset server-side (~20k players)
  - Auto-applies changes (no search button), debounced
  - Persists state to local storage and supports deep links via querystring
  - Uses MudBlazor components; mobile-friendly; consistent “Power” terminology

## 2) User experience overview
- Collapsible filter panel grouped as:
  - Basics
  - Activity and changes
  - Location
  - Wilderness
  - Alliance-related
  - Quick chips (No Alliance, New Today, Renamed, Moved, Gained Wilds, Lost Wilds)
- Auto-update: debounce 250–400ms for text/range inputs
- Show result count; keep pagination; apply filters before pagination
- Persist realm, date, and filter state (local storage); sync with querystring
- Mobile: stack groups when narrow; ensure generous tap targets and spacing

## 3) Data model and precomputations
- Add/ensure per-import aggregates (materialized during import or first query):
  - PlayerAggregate (RealmId, ImportDate, PlayerId):
    - Power
    - TotalWilds
    - WildCountByType (e.g., Forest, Mountain, Lake, Hill/Stone, Desert/Swamp)
    - MaxWildLevelOwned
    - PowerDelta (vs previous import)
    - NameChanged (bool vs previous import)
    - CityMovedDistance (int; 0 if unchanged)
    - WildsGained (count), WildsLost (count)
    - FirstSeenDate, LastSeenDate (DateOnly)
  - AllianceAggregate (RealmId, ImportDate, AllianceId):
    - MemberCount
- Indexes:
  - Player: lower(Name), AllianceId, RealmId, CityX, CityY
  - PlayerAggregate: (RealmId, ImportDate), PlayerId, Power, TotalWilds, PowerDelta,
    NameChanged, CityMovedDistance, WildsGained, WildsLost, MaxWildLevelOwned
  - AllianceAggregate: (RealmId, ImportDate), AllianceId, MemberCount
- Use date-only precision for ImportDate joins (consistent with imports)

## 4) Filter facets and DTOs
- PlayerFilterDTO
  - Name
    - NameContains: string
  - Alliance
    - AllianceId: Guid? (single)
    - NoAlliance: bool
    - AllianceIds: Guid[]? (optional multi-select)
  - Power
    - PowerMin: long?
    - PowerMax: long?
    - PowerDeltaMin: long? (vs previous import)
    - PowerDeltaMax: long?
  - Activity/temporal
    - InactiveDaysGreaterThan: int? (relative to selected ImportDate)
    - NewToday: bool (FirstSeenDate == ImportDate)
    - RenamedSinceLast: bool
    - AllianceChangedSinceLast: bool
    - CityMovedSinceLast: bool
    - CityMovedMinDistance: int?
  - Location
    - CityXMin: int?; CityXMax: int?
    - CityYMin: int?; CityYMax: int?
    - NearX: int?; NearY: int?; NearRadius: int?
    - Quadrants: string[]? ("NW","NE","SW","SE")
    - ExactCityX: int?; ExactCityY: int?
  - Wilderness
    - TotalWildsMin: int?; TotalWildsMax: int?
    - WildTypes: string[]?
    - MinPerSelectedWildType: int?
    - OwnsAnyWildLevelAtLeast: int?
    - OwnsAnyWildLevelExactly: int?
    - WildsGainedMin: int?; WildsLostMin: int?
    - AnyWildsGained: bool; AnyWildsLost: bool
  - Alliance-related
    - AllianceMemberCountMin: int?
    - AllianceMemberCountMax: int?
    - SoloPlayers: bool (alliance size == 1)
- Query wrapper
  - RealmId: Guid
  - ImportDate: DateOnly
  - Filter: PlayerFilterDTO
  - Page: int; PageSize: int; SortBy: string; SortDirection: string

## 5) API design
- Endpoint: POST /api/players/query
  - Body: { RealmId, ImportDate, Filter, Page, PageSize, SortBy, SortDirection }
  - Response: PagedResult<PlayerListItem>
    - TotalCount: int
    - Items: [{ PlayerId, Name, Power, AllianceId, AllianceName, CityX, CityY,
      TotalWilds, PowerDelta, CityMovedDistance, Badges: { Renamed, Moved, GainedWilds, LostWilds } }]
- GET /api/alliances?realmId=&importDate=
  - For Alliance filter dropdown (include MemberCount)
- Optional: GET /api/players/suggest?realmId=&importDate=&namePrefix=
  - Typeahead suggestions (if needed)

## 6) Server-side query composition
- Base query:
  - From Player snapshot at (RealmId, ImportDate)
  - Join PlayerAggregate on (RealmId, ImportDate, PlayerId)
  - Left-join AllianceAggregate on (RealmId, ImportDate, AllianceId)
- Predicate application order (cheap to expensive):
  1) RealmId and ImportDate equals
  2) AllianceId/AllianceIds/NoAlliance
  3) PowerMin/Max
  4) PowerDeltaMin/Max
  5) NewToday, RenamedSinceLast, AllianceChangedSinceLast
  6) CityMovedSinceLast, CityMovedMinDistance
  7) TotalWildsMin/Max
  8) AnyWildsGained/Lost, WildsGainedMin/LostMin
  9) OwnsAnyWildLevelAtLeast/Exactly, WildTypes + MinPerSelectedWildType
  10) Location bounding box and Quadrants;
      NearX/NearY/Radius via squared distance (dx*dx + dy*dy <= r*r)
  11) InactiveDaysGreaterThan (ImportDate - LastSeenDate)
  12) AllianceMemberCountMin/Max; SoloPlayers
  13) NameContains (lower-cased; normalized names column preferred)
- Sorting:
  - Default: Power desc; secondary Name asc
  - Options: Name asc, PowerDelta desc, CityMovedDistance desc, TotalWilds desc

## 7) MudBlazor UI
- Filter panel with groups:
  - Basics: Name (MudTextField, debounce); Alliance (MudSelect, No Alliance first, optional multi-select);
    PowerMin/Max (MudNumericFields or MudRangeSlider)
  - Activity and changes: Inactive days (numeric), switches for New Today, Renamed, Alliance Changed,
    City Moved; CityMovedMinDistance; PowerDelta min/max
  - Location: CityX/Y min-max; NearX/NearY/NearRadius; Quadrants (4 custom-styled checkboxes);
    ExactX/ExactY
  - Wilderness: TotalWilds min/max; WildTypes chips multi-select; MinPerSelectedWildType;
    OwnsAnyWildLevelAtLeast/Exactly; AnyWildsGained/Lost switches; WildsGainedMin/LostMin
  - Alliance-related: AllianceMemberCount min/max; SoloPlayers switch
  - Quick chips: No Alliance, New Today, Renamed, Moved, Gained Wilds, Lost Wilds
- Behavior:
  - Debounced on-change dispatch; display small inline progress while querying
  - Persist filter state with key: playersFilter:{realmId}:{importDate}
  - Querystring sync for deep links on load and state changes
  - Mobile responsive spacing; consistent dragon theme styling

## 8) Validation and safeguards
- Clamp numeric ranges (ensure min ≤ max)
- If both OwnsAnyWildLevelAtLeast and Exactly provided, prefer Exactly
- MinPerSelectedWildType only applied when WildTypes selected
- Limit NearRadius to a sensible max (e.g., 500) to prevent pathological scans
- InactiveDaysGreaterThan computed against ImportDate

## 9) Performance considerations
- Execute filters in DB via EF Core; avoid client-side filtering
- Precompute heavy deltas/aggregates at import time
- Maintain helpful indexes per §3
- Pagination with TotalCount (consider caching count when expensive)
- Cache results per (realm, date, filter-hash, page, pageSize, sort) with short TTL
- Rate limiting per IP/user for query endpoints

## 10) Security and access
- Normal player list APIs: no auth (per preference)
- History endpoints: basic auth only
- Keep passwords out of frontend; ensure API does not leak admin-only data

## 11) Telemetry and logging
- Log filter hash, execution time, result count (info level)
- Keep EF DbCommand logging disabled
- Include realmId/importDate context in errors

## 12) Testing and verification (lean)
- Manual checks (at minimum):
  - Alliance = No Alliance; Power ranges; Power deltas
  - City moved + min distance; Wilderness type + min per type
  - Near radius; Inactive > N days; Solo players; New Today; Renamed
- Ensure compilation passes and UI interactions debounce properly

## 13) Implementation tasks
1. Data layer
   - Create PlayerAggregate and AllianceAggregate entities/tables
   - Implement aggregate population in import pipeline (prev-import lookup; date-only joins)
   - Add indexes
2. API layer
   - Define PlayerFilterDTO and request wrapper
   - Implement POST /api/players/query with server-side predicates and paging
   - Implement GET /api/alliances for filter dropdown
   - Add sorting options and defaults
3. UI layer (MudBlazor)
   - Build filter panel with grouped controls and quick chips
   - Implement debounce, auto-apply, loading indicators
   - Persist to local storage; querystring sync; deep-link handling
   - Integrate with players list; show counts; pagination preserved
   - Mobile responsive polishing
4. Perf/ops
   - Add lightweight caching and rate limiting



