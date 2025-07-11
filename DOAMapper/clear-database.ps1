# Script to clear the database and start fresh

Write-Host "🗑️ Clearing Dragons of Atlantis Map Tracker Database..." -ForegroundColor Yellow

# Stop any running application processes
$processes = Get-Process -Name "DOAMapper" -ErrorAction SilentlyContinue
if ($processes) {
    Write-Host "Stopping running DOAMapper processes..." -ForegroundColor Yellow
    $processes | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Database file path
$dbPath = ".\DOAMapper\doamapper.db"

# Remove the database file if it exists
if (Test-Path $dbPath) {
    Write-Host "Removing existing database file: $dbPath" -ForegroundColor Cyan
    Remove-Item $dbPath -Force
    Write-Host "✅ Database file removed successfully" -ForegroundColor Green
} else {
    Write-Host "ℹ️ No existing database file found" -ForegroundColor Blue
}

# Also remove any journal files
$journalPath = "$dbPath-journal"
if (Test-Path $journalPath) {
    Write-Host "Removing database journal file: $journalPath" -ForegroundColor Cyan
    Remove-Item $journalPath -Force
}

$walPath = "$dbPath-wal"
if (Test-Path $walPath) {
    Write-Host "Removing database WAL file: $walPath" -ForegroundColor Cyan
    Remove-Item $walPath -Force
}

$shmPath = "$dbPath-shm"
if (Test-Path $shmPath) {
    Write-Host "Removing database SHM file: $shmPath" -ForegroundColor Cyan
    Remove-Item $shmPath -Force
}

Write-Host "🎉 Database cleared successfully! Ready for fresh import." -ForegroundColor Green
