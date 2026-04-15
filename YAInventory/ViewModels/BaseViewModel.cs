using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YAInventory.ViewModels
{
    /// <summary>Base class providing INotifyPropertyChanged for all ViewModels.</summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Sets the field and fires PropertyChanged only when the value actually changes.
        /// Returns true if the value changed.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // ── Common UI state ────────────────────────────────────────────────
        private bool   _isBusy;
        private string _busyMessage = "Loading…";
        private string _statusMessage = string.Empty;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        protected void SetBusy(string message = "Loading…")
        {
            IsBusy = true;
            BusyMessage = message;
        }

        protected void ClearBusy()
        {
            IsBusy = false;
            BusyMessage = "Loading…";
        }
    }
}
