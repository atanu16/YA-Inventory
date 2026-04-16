using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YAInventory.Services
{
    /// <summary>
    /// Simple navigation service: registers view-factory lambdas keyed by name,
    /// then navigates by swapping the CurrentView observable.
    /// ViewModels subscribe to CurrentViewChanged to render the correct page.
    /// </summary>
    public class NavigationService : INotifyPropertyChanged
    {
        private readonly Dictionary<string, Func<object>> _viewFactories = new();
        private object? _currentView;
        private string  _currentPage = string.Empty;

        public object? CurrentView
        {
            get => _currentView;
            private set { _currentView = value; OnPropertyChanged(); }
        }

        public string CurrentPage
        {
            get => _currentPage;
            private set { _currentPage = value; OnPropertyChanged(); }
        }

        public void Register(string pageName, Func<object> factory)
            => _viewFactories[pageName] = factory;

        public void NavigateTo(string pageName)
        {
            if (_viewFactories.TryGetValue(pageName, out var factory))
            {
                CurrentView = factory();
                CurrentPage = pageName;
            }
        }

        public bool CanNavigate(string pageName) => _viewFactories.ContainsKey(pageName);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
