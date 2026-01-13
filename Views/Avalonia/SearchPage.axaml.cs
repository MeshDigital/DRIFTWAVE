using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
using SLSKDONET.Views;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Views.Avalonia
{
    public partial class SearchPage : UserControl
    {
        public SearchPage()
        {
            InitializeComponent();
            
            // Wire up TreeDataGrid interaction
            var grid = this.FindControl<TreeDataGrid>("ResultsGrid");
            if (grid != null)
            {
                grid.DoubleTapped += (s, e) => {
                    if (DataContext is SearchViewModel vm && vm.SelectedResults.Any())
                    {
                        vm.DownloadSelectedCommand.Execute(null);
                    }
                };

                grid.KeyDown += (s, e) => {
                    if (e.Key == Key.Enter && DataContext is SearchViewModel vm && vm.SelectedResults.Any())
                    {
                        vm.DownloadSelectedCommand.Execute(null);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Control) && DataContext is SearchViewModel vm2)
                    {
                        // Navigate to next Platinum track
                        if (grid.Source is FlatTreeDataGridSource<AnalyzedSearchResultViewModel> source && source.RowSelection != null)
                        {
                            var currentIndex = source.RowSelection.SelectedIndex.FirstOrDefault();
                            var items = vm2.SearchResults.ToList();
                            for (int i = currentIndex + 1; i < items.Count; i++)
                            {
                                if (items[i].Tier == Models.SearchTier.Platinum)
                                {
                                    source.RowSelection.SelectedIndex = new global::Avalonia.Controls.IndexPath(i);
                                    grid.RowsPresenter?.BringIntoView(i);
                                    e.Handled = true;
                                    break;
                                }
                            }
                        }
                    }
                };

                // Sync selection
                if (grid.Source is FlatTreeDataGridSource<AnalyzedSearchResultViewModel> source && source.RowSelection != null)
                {
                    source.RowSelection.SelectionChanged += (s, e) => {
                        if (DataContext is SearchViewModel vm)
                        {
                            vm.SelectedResults.Clear();
                            foreach (var item in source.RowSelection.SelectedItems.OfType<AnalyzedSearchResultViewModel>())
                            {
                                vm.SelectedResults.Add(item);
                            }
                        }
                    };
                }
            }

            // Enable Drag & Drop
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);
        }

        private void ValidatingSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SearchViewModel vm)
            {
                // Synchronize selection to ViewModel
                // Note: We clear and re-add or just modify?
                // ObservableCollection doesn't support range actions easily but we can use helper.
                
                // Optimized approach: 
                // 1. Remove items that were unselected
                foreach (var item in e.RemovedItems.OfType<AnalyzedSearchResultViewModel>())
                {
                    vm.SelectedResults.Remove(item);
                }

                // 2. Add items that were selected
                foreach (var item in e.AddedItems.OfType<AnalyzedSearchResultViewModel>())
                {
                    if (!vm.SelectedResults.Contains(item))
                        vm.SelectedResults.Add(item);
                }
            }
        }

        public SearchPage(SearchViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                // Only allow CSV files
                var files = e.Data.GetFiles();
                if (files != null && files.Any(f => f.Name.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase)))
                {
                    e.DragEffects = DragDropEffects.Copy;
                    return;
                }
            }
            e.DragEffects = DragDropEffects.None;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                var csvFile = files?.FirstOrDefault(f => f.Name.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase));

                if (csvFile != null && DataContext is SLSKDONET.ViewModels.SearchViewModel vm)
                {
                    // Auto-switch to CSV mode and populate path
                    // vm.CurrentSearchMode = Models.SearchInputMode.CsvFile; // Logic is now inferred from extension in SearchViewModel
                    
                    if (csvFile.Path.IsAbsoluteUri && csvFile.Path.Scheme == "file")
                    {
                        vm.SearchQuery = csvFile.Path.LocalPath;
                    }
                    else
                    {
                        vm.SearchQuery = System.Uri.UnescapeDataString(csvFile.Path.ToString());
                    }
                    
                    // Optional: Trigger browse/preview automatically if desired?
                    // vm.BrowseCsvCommand.Execute(null); 
                }
            }
        }
    }
}
