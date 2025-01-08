using System.Windows;
using System.Windows.Controls;

namespace VRCOSC.Modules.OSCLeash.UI;

/// <summary>
/// Interaction logic for LeashModuleSettingView.xaml
/// </summary>
public partial class LeashModuleSettingView : UserControl
{
    public LeashModuleSettingView(OSCLeashModule _, OSCLeashModuleSettings settings)
    {
        InitializeComponent();
        DataContext = new LeashModuleSettingViewModel(settings);
    }
} 