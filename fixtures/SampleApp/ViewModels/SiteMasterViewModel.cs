using DotVVM.Framework.ViewModel;

namespace SampleApp.ViewModels;

/// <summary>
/// The view model the master pages share. It exists because the directives naming it have to
/// resolve: a type a view declares and the project does not hold is exactly what the validator
/// reports, and what go-to-definition then cannot find. The properties are the ones the master
/// page binds - a menu that collapses, and a count that decides whether a badge is rendered.
/// </summary>
public class SiteMasterViewModel : DotvvmViewModelBase
{
    public string Title { get; set; } = "Sample";

    public bool MenuCollapsed { get; set; }

    public bool MenuVisible { get; set; } = true;

    public int UnreadCount { get; set; }

    public int SelectedId { get; set; }
}
