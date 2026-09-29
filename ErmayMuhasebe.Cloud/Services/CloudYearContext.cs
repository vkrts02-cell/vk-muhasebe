using System;
using ErmayMuhasebe.Services;
using Blazored.LocalStorage;

namespace ErmayMuhasebe.Cloud.Services
{
    public class CloudYearContext : IYearContext
    {
        private readonly ISyncLocalStorageService _localStorage;
        private int _currentYear;

        public CloudYearContext(ISyncLocalStorageService localStorage)
        {
            _localStorage = localStorage;
            
            if (_localStorage.ContainKey("active_year"))
            {
                var val = _localStorage.GetItem<int>("active_year");
                if (val >= 2000 && val <= 2100)
                {
                    _currentYear = val;
                }
                else
                {
                    _currentYear = DateTime.Now.Year;
                }
            }
            else
            {
                _currentYear = DateTime.Now.Year;
            }
        }

        public event Action<int>? YearChanged;

        public int CurrentYear
        {
            get => _currentYear;
            set
            {
                if (_currentYear != value)
                {
                    _currentYear = value;
                    _localStorage.SetItem("active_year", _currentYear);
                    YearChanged?.Invoke(value);
                }
            }
        }
    }
}
