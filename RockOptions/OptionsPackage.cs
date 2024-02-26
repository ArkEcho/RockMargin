using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;


namespace RockMargin
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideOptionPage(typeof(OptionsPage), "RockMargin", "General", 0, 0, true)]
    public sealed class OptionsPackage : Package
    {
        protected override void Initialize()
        {
            var componentModel = GetService(typeof(SComponentModel)) as IComponentModel;
            OptionsPage.OptionsService = componentModel.GetService<IEditorOptionsFactoryService>();
            OptionsPage.SettingsManager = GetService(typeof(SVsSettingsManager)) as IVsSettingsManager;
        }
    }
}
