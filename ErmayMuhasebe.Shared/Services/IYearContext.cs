using System;

namespace ErmayMuhasebe.Services
{
    public interface IYearContext
    {
        int CurrentYear { get; set; }
        event Action<int>? YearChanged;
    }

    public class YearContext : IYearContext
    {
        private int _currentYear = DateTime.Now.Year;
        public event Action<int>? YearChanged;

        public int CurrentYear
        {
            get => _currentYear;
            set
            {
                if (_currentYear != value)
                {
                    _currentYear = value;
                    YearChanged?.Invoke(value);
                }
            }
        }
    }
}
