# Dragons of Atlantis Map Tracker - Project Overview

## Mission Statement

Develop a comprehensive implementation plan for a Dragons of Atlantis map tracking web application that enables historical data management, player/alliance search functionality, and temporal data visualization. The system must efficiently handle large JSON datasets (90MB), implement change detection to optimize storage, provide date-based data selection, and support player/alliance relationship navigation with historical tracking capabilities.

## Project Requirements

### Core Functionality
- **Player Search**: Search players by name or ID with historical data support
- **Alliance Browsing**: Browse alliances with member lists and statistics
- **Player Details**: Show player information, alliance membership, and owned tiles
- **Alliance Details**: Display alliance information and member rosters
- **Historical Data**: Track changes over time with date-based data selection
- **Data Import**: Process large JSON files (90MB) with change detection

### Technical Requirements
- **Data Volume**: Handle 90MB JSON imports efficiently
- **Historical Tracking**: Store multiple snapshots over time (1-7 day intervals)
- **Change Detection**: Avoid duplicate storage through intelligent comparison
- **Performance**: Support concurrent users with responsive UI
- **Map Coverage**: Handle 750x750 coordinate grid (0,0 to 749,749)

### Data Structure Analysis

Based on the example JSON file, the system processes three main entity types:

#### Tiles
```json
{
  "x": 5,
  "y": 0,
  "type": "City",
  "level": 20,
  "playerId": 9415,
  "allianceId": 18
}
```

#### Players
```json
{
  "playerId": "6109",
  "name": "Senna",
  "city": "Goob City",
  "might": "5145442"
}
```

#### Alliance Bases (Alliances)
```json
{
  "alliance_id": 495,
  "fortress_level": 4,
  "x": 256,
  "y": 3,
  "name": "144k_Elites",
  "overlord": "Phantom_Dragon",
  "power": "33908760"
}
```

### Key Design Decisions

1. **Alliance Membership**: Determined by player's city tile (type="City"), falling back to any owned tile's allianceId
2. **Data Normalization**: All IDs normalized to strings for consistency
3. **Map Boundaries**: 750x750 grid requiring spatial indexing
4. **Alliance Structure**: One alliance base per alliance (alliance bases = alliances)
5. **Access Control**: Publicly accessible (no authentication required)
6. **Performance**: Optimized for low concurrent user load

### Technology Stack

- **Frontend**: Blazor Server with Bootstrap 5
- **Backend**: ASP.NET Core Web API
- **Database**: Entity Framework Core with PostgreSQL (production) / SQLite (development)
- **Data Processing**: System.Text.Json with streaming support
- **Logging**: Serilog with structured logging
- **Deployment**: Docker containerization
- **Monitoring**: Health checks and application insights

### Success Criteria

1. **Functional**: All search, browse, and detail views working correctly
2. **Performance**: JSON imports complete within reasonable time (< 10 minutes)
3. **Storage**: Change detection reduces storage by avoiding duplicates
4. **Usability**: Intuitive UI with responsive design
5. **Reliability**: Robust error handling and data validation
6. **Maintainability**: Clean architecture with proper separation of concerns

## Project Phases Overview

1. **Phase 1**: Foundation & Data Layer (Week 1-2)
2. **Phase 2**: Core Backend Services (Week 2-3)
3. **Phase 3**: API Layer Development (Week 3-4)
4. **Phase 4**: Frontend Core Components (Week 4-5)
5. **Phase 5**: Data Import & Historical Features (Week 5-6)
6. **Phase 6**: Testing & Polish (Week 6-7)

## Risk Assessment

### Technical Risks
- **Memory Issues**: Large JSON files may cause memory problems
- **Performance**: Database queries on historical data may be slow
- **Data Integrity**: Inconsistent data in JSON imports

### Mitigation Strategies
- **Streaming Processing**: Use streaming JSON reader for large files
- **Indexing Strategy**: Implement proper database indexes for performance
- **Validation**: Comprehensive data validation during import
- **Error Handling**: Robust error handling with detailed logging

## Next Steps

1. Review and approve this project overview
2. Set up development environment
3. Begin Phase 1 implementation
4. Establish regular progress reviews
5. Plan deployment infrastructure
