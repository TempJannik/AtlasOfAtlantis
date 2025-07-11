namespace DOAMapper.Client.Services;

public class DateStateService
{
    private DateTime? _selectedDate;
    private List<DateTime> _availableDates = new();

    public event Action<DateTime?>? DateChanged;
    public event Action<List<DateTime>>? AvailableDatesChanged;

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate != value)
            {
                _selectedDate = value;
                DateChanged?.Invoke(_selectedDate);
            }
        }
    }

    public List<DateTime> AvailableDates
    {
        get => _availableDates;
        set
        {
            _availableDates = value;
            AvailableDatesChanged?.Invoke(_availableDates);
            
            // Auto-select the latest date if none is selected
            if (_selectedDate == null && _availableDates.Any())
            {
                SelectedDate = _availableDates.First();
            }
        }
    }

    public void SetAvailableDates(List<DateTime> dates)
    {
        AvailableDates = dates;
    }

    public void SetSelectedDate(DateTime? date)
    {
        SelectedDate = date;
    }

    public bool IsDateAvailable(DateTime date)
    {
        return _availableDates.Any(d => d.Date == date.Date);
    }

    public DateTime? GetLatestDate()
    {
        return _availableDates.FirstOrDefault();
    }

    public DateTime? GetEarliestDate()
    {
        return _availableDates.LastOrDefault();
    }
}
