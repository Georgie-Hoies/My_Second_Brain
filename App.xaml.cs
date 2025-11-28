using System.Configuration;
using System.Data;
using System.Windows;

namespace My_Second_Brain
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    // 【修复】显式指定继承自 System.Windows.Application (WPF)，消除二义性
    public partial class App : System.Windows.Application
    {
    }
}