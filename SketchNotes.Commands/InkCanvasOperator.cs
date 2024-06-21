using SketchNotes.FileIO;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SketchNotes.Commands
{
    public class InkCanvasOperator
    {
        private static InkCanvas MainInkCanvas;
        private static InkToolbar MainInkToolbar;
        public static DispatcherTimer AutoSaveTimer;

        public static void TimeSpanSetup()
        {
            AutoSaveTimer = new DispatcherTimer();
            AutoSaveTimer.Interval = TimeSpan.FromMilliseconds(100);
            AutoSaveTimer.Tick -= AutoTasksTimer_Tick;
            AutoSaveTimer.Tick += AutoTasksTimer_Tick;
        }

        private static async void AutoTasksTimer_Tick(object sender, object e)
        {
            AutoSaveTimer?.Stop();
            await SaveRestorePoint();
        }

        public static async Task SaveRestorePoint()
        {
            CommandsOperator.CurrentStep++;
            CommandsOperator.TotalStep = CommandsOperator.CurrentStep;

            Windows.Storage.StorageFolder storageFolder =
                Windows.Storage.ApplicationData.Current.LocalFolder;
            Windows.Storage.StorageFolder folder =
                await storageFolder.CreateFolderAsync("Temp\\" + CommandsOperator.NoteBookGuid + "\\" + CommandsOperator.PageGuid,
                Windows.Storage.CreationCollisionOption.OpenIfExists);
            Windows.Storage.StorageFile file =
                await folder.CreateFileAsync(CommandsOperator.CurrentStep + ".ink",
                Windows.Storage.CreationCollisionOption.ReplaceExisting);

            IRandomAccessStream stream = new InMemoryRandomAccessStream();
            await MainInkCanvas.InkPresenter.StrokeContainer.SaveAsync(stream);
            await InkOperator.SaveInk(stream, file);
            stream.Dispose();

            CommandsOperator.SetBtnStatue();
        }

        public static void SetInkCanvas(InkCanvas inkCanvas, InkToolbar inkToolbar)
        {
            MainInkCanvas = inkCanvas;
            MainInkToolbar = inkToolbar;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeStarted -= StrokeInput_StrokeStarted;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeEnded -= StrokeInput_StrokeEnded;
            MainInkCanvas.InkPresenter.StrokesErased -= InkPresenter_StrokesErased;
            MainInkToolbar.EraseAllClicked -= MainInkToolbar_EraseAllClicked;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_StrokeEnded;
            MainInkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
            MainInkToolbar.EraseAllClicked += MainInkToolbar_EraseAllClicked;
        }

        private static async void MainInkToolbar_EraseAllClicked(InkToolbar sender, object args)
        {
            if (MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
            {
                MainInkCanvas.InkPresenter.StrokeContainer.Clear();
                // Save the restore point.
                await SaveRestorePoint();
            }
        }

        public async static void LoadInk(StorageFile file)
        {
            await InkOperator.LoadInk(file, MainInkCanvas);
        }
        public static void ClearAll()
        {
            MainInkCanvas.InkPresenter.StrokeContainer.Clear();
        }

        private static void InkPresenter_StrokesErased(Windows.UI.Input.Inking.InkPresenter sender, Windows.UI.Input.Inking.InkStrokesErasedEventArgs args)
        {
            AutoSaveTimer?.Stop();
            TimeSpanSetup();
            AutoSaveTimer.Start();
        }

        private static void StrokeInput_StrokeStarted(Windows.UI.Input.Inking.InkStrokeInput sender, Windows.UI.Core.PointerEventArgs args)
        {
            AutoSaveTimer?.Stop();
        }

        private static void StrokeInput_StrokeEnded(Windows.UI.Input.Inking.InkStrokeInput sender, Windows.UI.Core.PointerEventArgs args)
        {
            TimeSpanSetup();
            AutoSaveTimer.Start();
        }
    }
}
