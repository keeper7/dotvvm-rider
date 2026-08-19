using DotVVM.Framework.ViewModel;

namespace SampleApp.ViewModels
{
    public class DifferentlyNamedViewModel : DotvvmViewModelBase
    {
        public string Name { get; set; } = "";
        public void Save() { }
    }
}
