using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace SketchNotes.TabPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class StartPage : Page
    {
        public StartPage()
        {
            InitializeComponent();
        }

        //private void AddExampleFileBtn_Click(object sender, RoutedEventArgs e)
        //{

        //}

        //private void AddWelcomePageBtn_Click(object sender, RoutedEventArgs e)
        //{

        //}

        //private void AddWebPageBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    MainPage.RootPage.MainTabView.TabItems.Add(
        //        MainPage.RootPage.CreateNewTab(
        //        MainPage.RootPage.MainTabView.TabItems.Count, 1));
        //    MainPage.RootPage.MainTabView.SelectedIndex =
        //        MainPage.RootPage.MainTabView.TabItems.Count - 1;
        //}

        private void RecoverySettingsCard_Click(object sender, RoutedEventArgs e)
        {
            HomePage.InitialPage.MainNavigationView.SelectedItem = 
                HomePage.InitialPage.MainNavigationView.MenuItems[3];
            HomePage.InitialPage.MainNavigationView_Navigate(
                "Recovery", new Windows.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
        }

        //private void AddMapPageBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    MainPage.RootPage.MainTabView.TabItems.Add(
        //        MainPage.RootPage.CreateNewTab(
        //        MainPage.RootPage.MainTabView.TabItems.Count, 2));
        //    MainPage.RootPage.MainTabView.SelectedIndex =
        //        MainPage.RootPage.MainTabView.TabItems.Count - 1;
        //}

        //private void AddCapturePageBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    MainPage.RootPage.MainTabView.TabItems.Add(
        //        MainPage.RootPage.CreateNewTab(
        //        MainPage.RootPage.MainTabView.TabItems.Count, 3));
        //    MainPage.RootPage.MainTabView.SelectedIndex =
        //        MainPage.RootPage.MainTabView.TabItems.Count - 1;
        //}
    }
}
