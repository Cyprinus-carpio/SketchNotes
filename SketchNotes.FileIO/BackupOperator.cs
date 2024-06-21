using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace SketchNotes.FileIO
{
    public class BackupOperator
    {
        public static async Task SaveBackup(
            IRandomAccessStream stream,
            Guid pageGuid,
            Guid appGuid)
        {
            try
            {
                Windows.Storage.StorageFolder storageFolder =
                    Windows.Storage.ApplicationData.Current.LocalFolder;
                Windows.Storage.StorageFolder folder =
                    await storageFolder.CreateFolderAsync("Backup\\" + appGuid,
                    Windows.Storage.CreationCollisionOption.OpenIfExists);
                Windows.Storage.StorageFile file =
                    await folder.CreateFileAsync(pageGuid + ".ink",
                    Windows.Storage.CreationCollisionOption.OpenIfExists);

                await InkOperator.SaveInk(stream, file);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
