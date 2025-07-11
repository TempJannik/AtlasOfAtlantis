# Frontend UI/UX Design

## Component Architecture

### Navigation Structure
```
DOAMapper
├── Home (Dashboard)
├── Players
│   ├── Search Players
│   └── Player Details
├── Alliances  
│   ├── Browse Alliances
│   └── Alliance Details
├── Import Data
└── History/Analytics
```

## Core Components

### 1. Global Date Selector Component

**DateSelector.razor**
```razor
<div class="date-selector-container">
    <label class="form-label">View Data From:</label>
    <select class="form-select" @bind="SelectedDate" @bind:after="OnDateChanged">
        <option value="">Select Date...</option>
        @foreach (var date in AvailableDates)
        {
            <option value="@date.ToString("yyyy-MM-dd")">
                @date.ToString("MMM dd, yyyy") (@GetRecordCount(date) records)
            </option>
        }
    </select>
    @if (SelectedDate != null)
    {
        <small class="text-muted">
            Showing data imported on @SelectedDate.Value.ToString("MMMM dd, yyyy")
        </small>
    }
</div>

@code {
    [Parameter] public DateTime? SelectedDate { get; set; }
    [Parameter] public EventCallback<DateTime?> SelectedDateChanged { get; set; }
    [Parameter] public List<DateTime> AvailableDates { get; set; } = new();
    
    private async Task OnDateChanged()
    {
        await SelectedDateChanged.InvokeAsync(SelectedDate);
    }
    
    private string GetRecordCount(DateTime date)
    {
        // Implementation to get record count for date
        return "Loading...";
    }
}
```

### 2. Player Search Component

**PlayerSearch.razor**
```razor
@page "/players"
@inject IPlayerService PlayerService
@inject NavigationManager Navigation

<div class="player-search">
    <div class="search-header">
        <h2>Player Search</h2>
        <DateSelector @bind-SelectedDate="CurrentDate" AvailableDates="AvailableDates" />
    </div>
    
    <div class="search-controls">
        <div class="input-group">
            <input type="text" class="form-control" placeholder="Search by player name or ID..." 
                   @bind="SearchQuery" @bind:after="OnSearchChanged" />
            <button class="btn btn-primary" @onclick="Search">
                <i class="bi bi-search"></i> Search
            </button>
        </div>
    </div>
    
    @if (IsLoading)
    {
        <div class="text-center p-4">
            <div class="spinner-border" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>
    }
    else if (SearchResults?.Items.Any() == true)
    {
        <div class="search-results">
            <div class="results-header">
                <span>Found @SearchResults.TotalCount players</span>
            </div>
            
            @foreach (var player in SearchResults.Items)
            {
                <div class="player-card" @onclick="() => NavigateToPlayer(player.PlayerId)">
                    <div class="player-info">
                        <h5>@player.Name</h5>
                        <p class="text-muted">@player.CityName</p>
                    </div>
                    <div class="player-stats">
                        <span class="might">Might: @player.Might.ToString("N0")</span>
                        @if (!string.IsNullOrEmpty(player.AllianceName))
                        {
                            <span class="alliance">[@player.AllianceName]</span>
                        }
                    </div>
                </div>
            }
            
            <Pagination CurrentPage="CurrentPage" TotalPages="SearchResults.TotalPages" 
                       OnPageChanged="OnPageChanged" />
        </div>
    }
    else if (!string.IsNullOrEmpty(SearchQuery))
    {
        <div class="alert alert-info">
            No players found matching "@SearchQuery"
        </div>
    }
</div>

@code {
    private DateTime? CurrentDate;
    private string SearchQuery = "";
    private bool IsLoading = false;
    private int CurrentPage = 1;
    private const int PageSize = 20;
    
    private PagedResult<PlayerDto>? SearchResults;
    private List<DateTime> AvailableDates = new();
    
    protected override async Task OnInitializedAsync()
    {
        AvailableDates = await PlayerService.GetAvailableDatesAsync();
        if (AvailableDates.Any())
        {
            CurrentDate = AvailableDates.First();
        }
    }
    
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || !CurrentDate.HasValue) return;
        
        IsLoading = true;
        try
        {
            SearchResults = await PlayerService.SearchPlayersAsync(SearchQuery, CurrentDate.Value, CurrentPage, PageSize);
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task OnSearchChanged()
    {
        CurrentPage = 1;
        await Search();
    }
    
    private async Task OnPageChanged(int page)
    {
        CurrentPage = page;
        await Search();
    }
    
    private void NavigateToPlayer(string playerId)
    {
        Navigation.NavigateTo($"/players/{playerId}?date={CurrentDate:yyyy-MM-dd}");
    }
}
```

### 3. Player Details Component

**PlayerDetails.razor**
```razor
@page "/players/{PlayerId}"
@inject IPlayerService PlayerService
@inject NavigationManager Navigation

<div class="player-details">
    @if (Player != null)
    {
        <div class="player-header">
            <div class="player-title">
                <h1>@Player.Name</h1>
                <p class="city-name">@Player.CityName</p>
            </div>
            <div class="player-actions">
                <DateSelector @bind-SelectedDate="CurrentDate" AvailableDates="AvailableDates" />
                <button class="btn btn-outline-secondary" @onclick="ShowHistory">
                    <i class="bi bi-clock-history"></i> View History
                </button>
            </div>
        </div>
        
        <div class="row">
            <div class="col-md-4">
                <div class="card">
                    <div class="card-header">
                        <h5>Player Stats</h5>
                    </div>
                    <div class="card-body">
                        <div class="stat-item">
                            <label>Player ID:</label>
                            <span>@Player.PlayerId</span>
                        </div>
                        <div class="stat-item">
                            <label>Might:</label>
                            <span class="might-value">@Player.Might.ToString("N0")</span>
                        </div>
                        @if (Player.Alliance != null)
                        {
                            <div class="stat-item">
                                <label>Alliance:</label>
                                <a href="/alliances/@Player.Alliance.AllianceId?date=@CurrentDate?.ToString("yyyy-MM-dd")" 
                                   class="alliance-link">
                                    @Player.Alliance.Name
                                </a>
                            </div>
                        }
                    </div>
                </div>
            </div>
            
            <div class="col-md-8">
                <div class="card">
                    <div class="card-header">
                        <h5>Owned Tiles (@PlayerTiles?.Count ?? 0)</h5>
                    </div>
                    <div class="card-body">
                        @if (PlayerTiles?.Any() == true)
                        {
                            <div class="tile-summary">
                                @foreach (var tileGroup in PlayerTiles.GroupBy(t => t.Type))
                                {
                                    <div class="tile-type-group">
                                        <h6>@tileGroup.Key (@tileGroup.Count())</h6>
                                        <div class="tile-list">
                                            @foreach (var tile in tileGroup.Take(10))
                                            {
                                                <span class="tile-badge" title="Level @tile.Level">
                                                    (@tile.X, @tile.Y) L@tile.Level
                                                </span>
                                            }
                                            @if (tileGroup.Count() > 10)
                                            {
                                                <span class="text-muted">... and @(tileGroup.Count() - 10) more</span>
                                            }
                                        </div>
                                    </div>
                                }
                            </div>
                        }
                        else
                        {
                            <p class="text-muted">No tiles owned by this player.</p>
                        }
                    </div>
                </div>
            </div>
        </div>
        
        @if (ShowPlayerHistory && PlayerHistory?.Any() == true)
        {
            <div class="card mt-3">
                <div class="card-header">
                    <h5>Player History</h5>
                </div>
                <div class="card-body">
                    <PlayerHistoryChart Data="PlayerHistory" />
                </div>
            </div>
        }
    }
    else if (IsLoading)
    {
        <div class="text-center p-4">
            <div class="spinner-border" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>
    }
    else
    {
        <div class="alert alert-warning">
            Player not found for the selected date.
        </div>
    }
</div>

@code {
    [Parameter] public string PlayerId { get; set; } = string.Empty;
    [SupplyParameterFromQuery] public string? Date { get; set; }
    
    private DateTime? CurrentDate;
    private bool IsLoading = true;
    private bool ShowPlayerHistory = false;
    
    private PlayerDetailDto? Player;
    private List<TileDto>? PlayerTiles;
    private List<PlayerHistoryDto>? PlayerHistory;
    private List<DateTime> AvailableDates = new();
    
    protected override async Task OnInitializedAsync()
    {
        AvailableDates = await PlayerService.GetAvailableDatesAsync();
        
        if (!string.IsNullOrEmpty(Date) && DateTime.TryParse(Date, out var parsedDate))
        {
            CurrentDate = parsedDate;
        }
        else if (AvailableDates.Any())
        {
            CurrentDate = AvailableDates.First();
        }
        
        await LoadPlayerData();
    }
    
    private async Task LoadPlayerData()
    {
        if (!CurrentDate.HasValue) return;
        
        IsLoading = true;
        try
        {
            Player = await PlayerService.GetPlayerAsync(PlayerId, CurrentDate.Value);
            PlayerTiles = await PlayerService.GetPlayerTilesAsync(PlayerId, CurrentDate.Value);
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task ShowHistory()
    {
        if (PlayerHistory == null)
        {
            PlayerHistory = await PlayerService.GetPlayerHistoryAsync(PlayerId);
        }
        ShowPlayerHistory = !ShowPlayerHistory;
    }
}
```

### 4. Alliance Browse Component

**AllianceBrowse.razor**
```razor
@page "/alliances"
@inject IAllianceService AllianceService
@inject NavigationManager Navigation

<div class="alliance-browse">
    <div class="browse-header">
        <h2>Alliances</h2>
        <DateSelector @bind-SelectedDate="CurrentDate" AvailableDates="AvailableDates" />
    </div>
    
    @if (IsLoading)
    {
        <div class="text-center p-4">
            <div class="spinner-border" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>
    }
    else if (Alliances?.Items.Any() == true)
    {
        <div class="alliance-list">
            <div class="list-header">
                <span>@Alliances.TotalCount alliances found</span>
                <small class="text-muted">Sorted by power (highest first)</small>
            </div>
            
            @foreach (var alliance in Alliances.Items)
            {
                <div class="alliance-card" @onclick="() => NavigateToAlliance(alliance.AllianceId)">
                    <div class="alliance-info">
                        <h5>@alliance.Name</h5>
                        <p class="overlord">Led by @alliance.OverlordName</p>
                    </div>
                    <div class="alliance-stats">
                        <div class="stat">
                            <label>Power:</label>
                            <span class="power-value">@alliance.Power.ToString("N0")</span>
                        </div>
                        <div class="stat">
                            <label>Members:</label>
                            <span>@alliance.MemberCount</span>
                        </div>
                        <div class="stat">
                            <label>Fortress:</label>
                            <span>Level @alliance.FortressLevel (@alliance.FortressX, @alliance.FortressY)</span>
                        </div>
                    </div>
                </div>
            }
            
            <Pagination CurrentPage="CurrentPage" TotalPages="Alliances.TotalPages" 
                       OnPageChanged="OnPageChanged" />
        </div>
    }
</div>

@code {
    private DateTime? CurrentDate;
    private bool IsLoading = true;
    private int CurrentPage = 1;
    private const int PageSize = 20;
    
    private PagedResult<AllianceDto>? Alliances;
    private List<DateTime> AvailableDates = new();
    
    protected override async Task OnInitializedAsync()
    {
        AvailableDates = await AllianceService.GetAvailableDatesAsync();
        if (AvailableDates.Any())
        {
            CurrentDate = AvailableDates.First();
            await LoadAlliances();
        }
    }
    
    private async Task LoadAlliances()
    {
        if (!CurrentDate.HasValue) return;
        
        IsLoading = true;
        try
        {
            Alliances = await AllianceService.GetAlliancesAsync(CurrentDate.Value, CurrentPage, PageSize);
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task OnPageChanged(int page)
    {
        CurrentPage = page;
        await LoadAlliances();
    }
    
    private void NavigateToAlliance(string allianceId)
    {
        Navigation.NavigateTo($"/alliances/{allianceId}?date={CurrentDate:yyyy-MM-dd}");
    }
}
```

## Shared Components

### Pagination Component

**Pagination.razor**
```razor
<nav aria-label="Page navigation">
    <ul class="pagination justify-content-center">
        <li class="page-item @(CurrentPage <= 1 ? "disabled" : "")">
            <button class="page-link" @onclick="() => OnPageChanged.InvokeAsync(CurrentPage - 1)" 
                    disabled="@(CurrentPage <= 1)">
                Previous
            </button>
        </li>
        
        @for (int i = Math.Max(1, CurrentPage - 2); i <= Math.Min(TotalPages, CurrentPage + 2); i++)
        {
            <li class="page-item @(i == CurrentPage ? "active" : "")">
                <button class="page-link" @onclick="() => OnPageChanged.InvokeAsync(i)">
                    @i
                </button>
            </li>
        }
        
        <li class="page-item @(CurrentPage >= TotalPages ? "disabled" : "")">
            <button class="page-link" @onclick="() => OnPageChanged.InvokeAsync(CurrentPage + 1)"
                    disabled="@(CurrentPage >= TotalPages)">
                Next
            </button>
        </li>
    </ul>
</nav>

@code {
    [Parameter] public int CurrentPage { get; set; } = 1;
    [Parameter] public int TotalPages { get; set; } = 1;
    [Parameter] public EventCallback<int> OnPageChanged { get; set; }
}
```

## CSS Styling

### Custom Styles

**Components/Shared/Styles.css**
```css
/* Player and Alliance Cards */
.player-card, .alliance-card {
    border: 1px solid #dee2e6;
    border-radius: 8px;
    padding: 1rem;
    margin-bottom: 0.5rem;
    cursor: pointer;
    transition: all 0.2s ease;
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.player-card:hover, .alliance-card:hover {
    border-color: #0d6efd;
    box-shadow: 0 2px 8px rgba(13, 110, 253, 0.15);
    transform: translateY(-1px);
}

/* Stat Items */
.stat-item {
    display: flex;
    justify-content: space-between;
    margin-bottom: 0.5rem;
    padding-bottom: 0.5rem;
    border-bottom: 1px solid #f8f9fa;
}

.stat-item:last-child {
    border-bottom: none;
    margin-bottom: 0;
}

.stat-item label {
    font-weight: 600;
    color: #6c757d;
}

/* Might and Power Values */
.might-value, .power-value {
    font-weight: bold;
    color: #198754;
    font-size: 1.1em;
}

/* Tile Badges */
.tile-badge {
    display: inline-block;
    background-color: #f8f9fa;
    border: 1px solid #dee2e6;
    border-radius: 4px;
    padding: 0.25rem 0.5rem;
    margin: 0.125rem;
    font-size: 0.875rem;
    font-family: monospace;
}

.tile-type-group {
    margin-bottom: 1rem;
}

.tile-type-group h6 {
    color: #495057;
    margin-bottom: 0.5rem;
}

/* Date Selector */
.date-selector-container {
    margin-bottom: 1rem;
}

.date-selector-container .form-select {
    max-width: 300px;
}

/* Search Results */
.search-results {
    margin-top: 1rem;
}

.results-header {
    margin-bottom: 1rem;
    padding-bottom: 0.5rem;
    border-bottom: 1px solid #dee2e6;
    display: flex;
    justify-content: space-between;
    align-items: center;
}

/* Alliance Link */
.alliance-link {
    color: #0d6efd;
    text-decoration: none;
    font-weight: 500;
}

.alliance-link:hover {
    text-decoration: underline;
}

/* Loading States */
.spinner-border {
    color: #0d6efd;
}

/* Responsive Design */
@media (max-width: 768px) {
    .player-card, .alliance-card {
        flex-direction: column;
        align-items: flex-start;
    }
    
    .player-stats, .alliance-stats {
        margin-top: 0.5rem;
        width: 100%;
    }
    
    .stat {
        margin-bottom: 0.25rem;
    }
}
```

## User Experience Flow

### Navigation Flow
1. **Home Page** → Overview and quick access to main features
2. **Player Search** → Search results → Player details → Alliance details
3. **Alliance Browse** → Alliance details → Member list → Player details
4. **Data Import** → Upload progress → Import history
5. **Historical Data** → Date selection affects all views consistently

### Responsive Design
- Mobile-first approach with Bootstrap 5
- Touch-friendly interface elements
- Collapsible navigation for mobile devices
- Responsive data tables with horizontal scrolling
- Optimized loading states and error messages

## Data Import Component

### DataImport.razor
```razor
@page "/import"
@inject IImportService ImportService
@inject IJSRuntime JSRuntime

<div class="data-import">
    <div class="import-header">
        <h2>Import Map Data</h2>
        <p class="text-muted">Upload a new JSON file to update the map data</p>
    </div>

    <div class="card">
        <div class="card-body">
            @if (CurrentImport == null)
            {
                <div class="upload-area">
                    <InputFile OnChange="OnFileSelected" accept=".json" class="form-control" />
                    <div class="upload-help">
                        <small class="text-muted">
                            Select a JSON file containing map data. Maximum file size: 100MB
                        </small>
                    </div>
                    @if (SelectedFile != null)
                    {
                        <div class="selected-file mt-3">
                            <p>Selected: @SelectedFile.Name (@(SelectedFile.Size / 1024 / 1024)MB)</p>
                            <button class="btn btn-primary" @onclick="StartImport" disabled="@IsUploading">
                                @if (IsUploading)
                                {
                                    <span class="spinner-border spinner-border-sm" role="status"></span>
                                    <span>Uploading...</span>
                                }
                                else
                                {
                                    <span>Start Import</span>
                                }
                            </button>
                        </div>
                    }
                </div>
            }
            else
            {
                <div class="import-progress">
                    <h5>Import in Progress</h5>
                    <div class="progress mb-3">
                        <div class="progress-bar" style="width: @(CurrentImport.ProgressPercentage)%">
                            @CurrentImport.ProgressPercentage%
                        </div>
                    </div>
                    <div class="import-stats">
                        <div class="stat">
                            <label>Status:</label>
                            <span class="badge bg-@GetStatusColor(CurrentImport.Status)">
                                @CurrentImport.Status
                            </span>
                        </div>
                        <div class="stat">
                            <label>Records Processed:</label>
                            <span>@CurrentImport.RecordsProcessed.ToString("N0")</span>
                        </div>
                        <div class="stat">
                            <label>Records Changed:</label>
                            <span>@CurrentImport.RecordsChanged.ToString("N0")</span>
                        </div>
                    </div>
                </div>
            }
        </div>
    </div>

    <div class="card mt-4">
        <div class="card-header">
            <h5>Import History</h5>
        </div>
        <div class="card-body">
            @if (ImportHistory?.Any() == true)
            {
                <div class="table-responsive">
                    <table class="table">
                        <thead>
                            <tr>
                                <th>Date</th>
                                <th>File Name</th>
                                <th>Status</th>
                                <th>Records</th>
                                <th>Changes</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var import in ImportHistory)
                            {
                                <tr>
                                    <td>@import.ImportDate.ToString("MMM dd, yyyy HH:mm")</td>
                                    <td>@import.FileName</td>
                                    <td>
                                        <span class="badge bg-@GetStatusColor(import.Status)">
                                            @import.Status
                                        </span>
                                    </td>
                                    <td>@import.RecordsProcessed.ToString("N0")</td>
                                    <td>@import.RecordsChanged.ToString("N0")</td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </div>
    </div>
</div>

@code {
    private IBrowserFile? SelectedFile;
    private bool IsUploading = false;
    private ImportSessionDto? CurrentImport;
    private List<ImportSessionDto>? ImportHistory;

    protected override async Task OnInitializedAsync()
    {
        ImportHistory = await ImportService.GetImportHistoryAsync();
    }

    private void OnFileSelected(InputFileChangeEventArgs e)
    {
        SelectedFile = e.File;
    }

    private async Task StartImport()
    {
        if (SelectedFile == null) return;

        IsUploading = true;
        try
        {
            using var stream = SelectedFile.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024);
            var session = await ImportService.StartImportAsync(stream, SelectedFile.Name);
            CurrentImport = await ImportService.GetImportStatusAsync(session.Id);

            // Poll for updates
            _ = Task.Run(async () => await PollImportStatus(session.Id));
        }
        finally
        {
            IsUploading = false;
        }
    }

    private async Task PollImportStatus(Guid sessionId)
    {
        while (CurrentImport?.Status == ImportStatus.Processing)
        {
            await Task.Delay(2000);
            CurrentImport = await ImportService.GetImportStatusAsync(sessionId);
            await InvokeAsync(StateHasChanged);
        }

        // Refresh import history
        ImportHistory = await ImportService.GetImportHistoryAsync();
        await InvokeAsync(StateHasChanged);
    }

    private string GetStatusColor(ImportStatus status)
    {
        return status switch
        {
            ImportStatus.Completed => "success",
            ImportStatus.Processing => "primary",
            ImportStatus.Failed => "danger",
            ImportStatus.Cancelled => "warning",
            _ => "secondary"
        };
    }
}
```
