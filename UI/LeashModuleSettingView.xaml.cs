
using System.Windows.Controls;

namespace VRCOSC.Modules.OSCLeash.UI;

public partial class LeashModuleSettingView : UserControl
{
    public LeashModuleSettingView(OSCLeashModule _, OSCLeashModuleSettings moduleSetting)
    {
        InitializeComponent();
        DataContext = moduleSetting;
    }
} 