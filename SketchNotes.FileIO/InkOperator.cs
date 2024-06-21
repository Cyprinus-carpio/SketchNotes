using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace SketchNotes.FileIO
{
    public class InkOperator
    {
        public async static Task SaveInk(IRandomAccessStream stream, IStorageFile file)
        {
            try
            {
                var bt = await ConvertImagetoByte(stream);
                await Windows.Storage.FileIO.WriteBytesAsync(file, bt);
                await CachedFileManager.CompleteUpdatesAsync(file);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async static Task<byte[]> ConvertImagetoByte(IRandomAccessStream fileStream)
        {
            var reader = new DataReader(fileStream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)fileStream.Size);

            byte[] pixels = new byte[fileStream.Size];

            reader.ReadBytes(pixels);
            return pixels;
        }

        public async static Task LoadInk(StorageFile pickedFile, InkCanvas targetMainInkCanvas)
        {
            try
            {
                var file = await pickedFile.OpenReadAsync();
                await targetMainInkCanvas.InkPresenter.StrokeContainer.LoadAsync(file);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
