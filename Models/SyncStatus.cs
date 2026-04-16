using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YAInventory.Models
{
    public enum SyncState
    {
        Idle,
        Syncing,
        Success,
        Failed,
        Offline
    }

    /// <summary>
    /// Observable sync status shown in the UI status bar.
    /// </summary>
    public class SyncStatus : INotifyPropertyChanged
    {
        private SyncState _state = SyncState.Offline;
        private string _message = "Not connected";
        private DateTime? _lastSync;

        public SyncState State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        public DateTime? LastSync
        {
            get => _lastSync;
            set { _lastSync = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastSyncLabel)); }
        }

        public string LastSyncLabel => LastSync.HasValue
            ? $"Synced {LastSync.Value.ToLocalTime():HH:mm:ss}"
            : "Never synced";

        public string StatusIcon => State switch
        {
            SyncState.Syncing => "↻",
            SyncState.Success => "✓",
            SyncState.Failed  => "✗",
            SyncState.Offline => "⊘",
            _                 => "—"
        };

        public string StatusColor => State switch
        {
            SyncState.Syncing => "#F59E0B",
            SyncState.Success => "#10B981",
            SyncState.Failed  => "#EF4444",
            SyncState.Offline => "#6B7280",
            _                 => "#6B7280"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
