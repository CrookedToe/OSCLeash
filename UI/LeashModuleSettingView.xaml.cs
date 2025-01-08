using System.Windows.Controls;

namespace VRCOSC.Modules.OSCLeash.UI;

public partial class LeashModuleSettingView : UserControl
{
    public LeashModuleSettingView()
    {
        InitializeComponent();
    }

    public LeashModuleSettingView(OSCLeashModule module, OSCLeashModuleSettings settings) : this()
    {
        DataContext = new LeashModuleSettingViewModel(settings);
    }
} 