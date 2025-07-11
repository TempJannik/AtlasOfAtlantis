# Dragons of Atlantis Map Tracker

A comprehensive web application for tracking and analyzing Dragons of Atlantis game map data with historical tracking capabilities.

## 🎯 Project Overview

This application provides a robust solution for managing large-scale Dragons of Atlantis map data (90MB+ JSON files) with efficient historical tracking, player/alliance search functionality, and temporal data visualization.

### Key Features

- **🔍 Player Search**: Search players by name or ID with historical data support
- **🏰 Alliance Browsing**: Browse alliances with member lists and statistics  
- **📊 Player Details**: Comprehensive player information including owned tiles and alliance membership
- **🛡️ Alliance Details**: Alliance information with member rosters and historical data
- **📈 Historical Tracking**: Track changes over time with date-based data selection
- **📥 Data Import**: Process large JSON files with intelligent change detection
- **⚡ Performance**: Optimized for handling 750x750 coordinate grids efficiently

## 🏗️ Architecture

### Technology Stack

- **Frontend**: Blazor Server with Bootstrap 5
- **Backend**: ASP.NET Core 9.0 Web API
- **Database**: Entity Framework Core with PostgreSQL (production) / SQLite (development)
- **Data Processing**: System.Text.Json with streaming support
- **Logging**: Serilog with structured logging
- **Deployment**: Docker containerization

### Core Components

1. **Temporal Database Design**: Tracks historical changes with validity periods
2. **Change Detection Algorithm**: Efficiently identifies and stores only modified data
3. **RESTful API**: Comprehensive endpoints for all functionality
4. **Responsive UI**: Mobile-friendly Blazor components
5. **Import System**: Handles large JSON files with progress tracking

## 📁 Documentation Structure

### Implementation Guides

1. **[Project Overview](01-Project-Overview.md)** - Mission, requirements, and success criteria
2. **[Database Design](02-Database-Design.md)** - Schema, entities, and temporal patterns
3. **[API Design](03-API-Design.md)** - RESTful endpoints and service architecture
4. **[Frontend Design](04-Frontend-Design.md)** - UI components and user experience
5. **[Implementation Guide](05-Implementation-Guide.md)** - Step-by-step development phases
6. **[Deployment Guide](06-Deployment-Guide.md)** - Production deployment and infrastructure
7. **[Change Detection Algorithm](07-Change-Detection-Algorithm.md)** - Detailed algorithm implementation

## 🚀 Quick Start

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL (for production) or SQLite (for development)
- Docker (optional, for containerized deployment)

### Development Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd DOAMapper
   ```

2. **Install dependencies**
   ```bash
   dotnet restore
   ```

3. **Configure database**
   ```bash
   # Update appsettings.Development.json with your database connection
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

5. **Access the application**
   - Navigate to `https://localhost:7297` or `http://localhost:5126`

### Docker Setup

1. **Build and run with Docker Compose**
   ```bash
   docker-compose up -d
   ```

2. **Access the application**
   - Navigate to `http://localhost:5000`

## 📊 Data Structure

The application processes JSON files containing three main entity types:

### Tiles
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

### Players
```json
{
  "playerId": "6109",
  "name": "Senna",
  "city": "Goob City",
  "might": "5145442"
}
```

### Alliance Bases
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

## 🔧 Configuration

### Environment Variables

```bash
# Database
ConnectionStrings__PostgreSQL=Host=localhost;Database=doamapper;Username=postgres;Password=password

# Import Settings
ImportSettings__MaxFileSizeMB=100
ImportSettings__ImportDirectory=/app/Data/Imports

# Logging
ASPNETCORE_ENVIRONMENT=Production
```

### Application Settings

Key configuration options in `appsettings.json`:

- **Database Connection**: Configure PostgreSQL or SQLite
- **Import Settings**: File size limits and allowed types
- **Logging Levels**: Control log verbosity
- **Security Headers**: Configure HTTPS and security policies

## 📈 Performance Considerations

### Database Optimization

- **Spatial Indexing**: Optimized for 750x750 coordinate queries
- **Temporal Indexing**: Efficient historical data retrieval
- **Full-text Search**: Fast player and alliance name searches
- **Change Detection**: Minimizes storage through intelligent comparison

### Application Performance

- **Streaming JSON**: Handles large files without memory issues
- **Batch Processing**: Processes data in chunks for optimal performance
- **Caching**: Memory and Redis caching for frequently accessed data
- **Async Operations**: Non-blocking I/O for better responsiveness

## 🔒 Security

### Data Protection

- **Input Validation**: Comprehensive validation of all user inputs
- **File Upload Security**: Strict validation of JSON file structure and size
- **SQL Injection Prevention**: Parameterized queries through EF Core
- **Security Headers**: HTTPS enforcement and security headers

### Access Control

- **Public Access**: No authentication required for current implementation
- **Rate Limiting**: Planned for future versions
- **API Security**: Input sanitization and error handling

## 🧪 Testing

### Test Coverage

- **Unit Tests**: Core business logic and services
- **Integration Tests**: API endpoints and database operations
- **Performance Tests**: Large data import scenarios
- **UI Tests**: Component functionality and user interactions

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 📦 Deployment

### Production Deployment Options

1. **Azure App Service** - Managed platform with integrated monitoring
2. **DigitalOcean Droplet** - Cost-effective VPS solution
3. **Self-hosted Docker** - Maximum control and customization

### Monitoring and Logging

- **Health Checks**: Database and file system monitoring
- **Structured Logging**: Serilog with file and console outputs
- **Performance Metrics**: Application insights and custom metrics
- **Backup Strategy**: Automated database and file backups

## 🤝 Contributing

### Development Workflow

1. Fork the repository
2. Create a feature branch
3. Implement changes with tests
4. Submit a pull request

### Code Standards

- Follow C# coding conventions
- Include unit tests for new features
- Update documentation for API changes
- Use meaningful commit messages

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

### Getting Help

- **Documentation**: Comprehensive guides in the `/docs` folder
- **Issues**: Report bugs and feature requests via GitHub issues
- **Discussions**: Community discussions and questions

### Common Issues

1. **Large File Imports**: Ensure sufficient memory and disk space
2. **Database Performance**: Check indexing and query optimization
3. **Connection Issues**: Verify database connection strings
4. **File Upload Errors**: Check file size limits and JSON structure

## 🗺️ Roadmap

### Future Enhancements

- **Interactive Map Visualization**: 2D canvas-based map viewer
- **Real-time Notifications**: SignalR for live updates
- **Advanced Analytics**: Statistical dashboards and trends
- **Mobile App**: Native mobile application
- **API Rate Limiting**: Enhanced security and usage controls

### Version History

- **v1.0.0**: Initial release with core functionality
- **v1.1.0**: Performance optimizations and UI improvements
- **v1.2.0**: Advanced search and filtering capabilities
- **v2.0.0**: Interactive map visualization (planned)

---

**Built with ❤️ for the Dragons of Atlantis community**
