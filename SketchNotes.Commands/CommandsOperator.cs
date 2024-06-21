using System;
using System.IO;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace SketchNotes.Commands
{
    public class CommandsOperator
    {
        public static int TotalStep = 0;
        public static int CurrentStep = 0;
        public static Guid NoteBookGuid;
        public static Guid PageGuid;
        private static Button UndoBtn;
        private static Button RedoBtn;
        private static Button DragBtn;

        public static async Task SetPage(
            Guid noteBookGuid,
            Guid pageGuid,
            Button undoBtn,
            Button redoBtn,
            Button dragBtn,
            InkCanvas inkCanvas,
            InkToolbar inkToolbar)
        {
            await Task.Delay(500);
            UndoBtn = undoBtn;
            RedoBtn = redoBtn;
            DragBtn = dragBtn;
            NoteBookGuid = noteBookGuid;
            PageGuid = pageGuid;
            UndoBtn.Click -= UndoBtn_Click;
            RedoBtn.Click -= RedoBtn_Click;
            UndoBtn.Click += UndoBtn_Click;
            RedoBtn.Click += RedoBtn_Click;
            InkCanvasOperator.SetInkCanvas(inkCanvas, inkToolbar);

            try
            {
                await GetStep();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static void SetBtnStatue()
        {
            if (CurrentStep < TotalStep)
            {
                RedoBtn.IsEnabled = true;
            }
            else
            {
                RedoBtn.IsEnabled = false;
            }

            if (CurrentStep > 0)
            {
                UndoBtn.IsEnabled = true;
            }
            else
            {
                UndoBtn.IsEnabled = false;
            }
        }

        private static async void RedoBtn_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            RedoBtn.IsEnabled = false;
            UndoBtn.IsEnabled = false;
            DragBtn.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            if (CurrentStep < TotalStep)
            {
                CurrentStep++;

                Windows.Storage.StorageFolder storageFolder =
                    Windows.Storage.ApplicationData.Current.LocalFolder;
                Windows.Storage.StorageFolder folder =
                               await storageFolder.CreateFolderAsync("Temp\\" + NoteBookGuid + "\\" + PageGuid,
                               Windows.Storage.CreationCollisionOption.OpenIfExists);

                if (File.Exists(folder.Path + "\\" + CurrentStep + ".ink"))
                {
                    var file = await folder.GetFileAsync(CurrentStep + ".ink");
                    InkCanvasOperator.LoadInk(file);
                }
            }

            SetBtnStatue();
        }

        private static async void UndoBtn_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            RedoBtn.IsEnabled = false;
            UndoBtn.IsEnabled = false;
            DragBtn.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            if (CurrentStep > 0)
            {
                CurrentStep--;

                if (CurrentStep == 0)
                {
                    InkCanvasOperator.ClearAll();
                }
                else
                {
                    Windows.Storage.StorageFolder storageFolder =
                        Windows.Storage.ApplicationData.Current.LocalFolder;
                    Windows.Storage.StorageFolder folder =
                        await storageFolder.CreateFolderAsync("Temp\\" + NoteBookGuid + "\\" + PageGuid,
                        Windows.Storage.CreationCollisionOption.OpenIfExists);

                    if (File.Exists(folder.Path + "\\" + CurrentStep + ".ink"))
                    {
                        var file = await folder.GetFileAsync(CurrentStep + ".ink");
                        InkCanvasOperator.LoadInk(file);
                    }
                }
            }

            SetBtnStatue();
        }

        private static async Task GetStep()
        {
            try
            {
                Windows.Storage.StorageFolder storageFolder =
                    Windows.Storage.ApplicationData.Current.LocalFolder;
                Windows.Storage.StorageFolder folder =
                    await storageFolder.CreateFolderAsync("Temp\\" + NoteBookGuid + "\\" + PageGuid,
                    Windows.Storage.CreationCollisionOption.OpenIfExists);
                var file = await folder.TryGetItemAsync("Step.txt");

                if (file != null)
                {
                    string[] lines = File.ReadAllLines(file.Path);
                    CurrentStep = int.Parse(lines[0]);
                    TotalStep = int.Parse(lines[1]);
                }
                else
                {
                    CurrentStep = 0;
                    TotalStep = 0;
                }
            }
            catch(Exception)
            {
                throw;
            }
        }

        public static async Task SaveStep()
        {
            try
            {
                Windows.Storage.StorageFolder storageFolder =
                    Windows.Storage.ApplicationData.Current.LocalFolder;
                Windows.Storage.StorageFolder folder =
                    await storageFolder.CreateFolderAsync("Temp\\" + NoteBookGuid + "\\" + PageGuid,
                    Windows.Storage.CreationCollisionOption.OpenIfExists);
                Windows.Storage.StorageFile file =
                    await folder.CreateFileAsync("Step.txt",
                    Windows.Storage.CreationCollisionOption.OpenIfExists);
                await Windows.Storage.FileIO.WriteTextAsync(file, CurrentStep + "\n" + TotalStep);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
