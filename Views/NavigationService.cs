using System;
using System.Collections.Generic;
using System.Windows.Controls;
using SLSKDONET.Views;

namespace SLSKDONET.Services;

public interface INavigationService
{
    void SetFrame(Frame frame);
    void RegisterPage(string key, Type pageType);
    void NavigateTo(string pageKey);
}

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private Frame? _frame;
    private readonly Dictionary<string, Type> _pages = new();

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void RegisterPage(string key, Type pageType)
    {
        _pages[key] = pageType;
    }

    public void SetFrame(Frame frame)
    {
        _frame = frame;
    }

    public void NavigateTo(string pageKey)
    {
        if (_frame != null && _pages.TryGetValue(pageKey, out var pageType))
        {
            var page = _serviceProvider.GetService(pageType) as Page;
            if (page != null)
            {
                // Ensure the page's DataContext is the MainViewModel.
                // This is crucial for bindings to work correctly.
                page.DataContext = _serviceProvider.GetService(typeof(MainViewModel));
                _frame.Navigate(page);
            }
        }
    }
}