using System.Collections.Generic;
using DotVVM.Framework.ViewModel;

namespace SampleApp.ViewModels
{
    /// <summary>
    /// Named differently from the view on purpose: the view model of Sample.dothtml is found
    /// through the directive, not by convention.
    /// </summary>
    public class DifferentlyNamedViewModel : DotvvmViewModelBase
    {
        public string Name { get; set; } = "";

        /// <summary>Bound by the Repeater, which needs something to iterate over.</summary>
        public List<Item> Items { get; set; } = new();

        public void Save() { }
    }

    public class Item
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
    }
}
