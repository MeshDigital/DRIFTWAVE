using System;

namespace SLSKDONET.Views;

/// <summary>
/// Simple notification interface used by view models.
/// Implementations may delegate to Wpf.Ui notification services when available.
/// </summary>
public interface INotificationService
{
    void Show(string title, string message, NotificationType type = NotificationType.Information, TimeSpan? duration = null);
}
