using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace SketchNotes.TTS
{
    public class SpeechClient
    {
        public async static Task<Windows.Storage.StorageFile> GetSpeech(
            Guid pageGuid,
            Guid appGuid,
            string text,
            int id,
            string format,
            double noise,
            double noisew)
        {
            try
            {
                Windows.Storage.StorageFolder storageFolder =
                    Windows.Storage.ApplicationData.Current.LocalFolder;
                Windows.Storage.StorageFolder folder =
                    await storageFolder.CreateFolderAsync("TTS\\" + appGuid + "\\" + pageGuid,
                    Windows.Storage.CreationCollisionOption.OpenIfExists);
                Windows.Storage.StorageFile file =
                    await folder.CreateFileAsync(Guid.NewGuid() + "." + format,
                    Windows.Storage.CreationCollisionOption.ReplaceExisting);

                HttpClient httpClient = new HttpClient();
                var response = await httpClient.GetAsync(
                    new Uri("https://cyprinus-carpio-vits-api.hf.space/voice/vits?text=" + text + "&id=" + id + "&format=" + format + "&noise=" + noise + "&noisew=" + noisew));
                var inputStream = await response.Content.ReadAsInputStreamAsync();
                var stream = await file.OpenStreamForWriteAsync();
                await inputStream.AsStreamForRead().CopyToAsync(stream);
                stream.Dispose();

                return file;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async static Task<Windows.Storage.StorageFile> GetSpeakers()
        {
            try
            {
                Windows.Storage.StorageFolder storageFolder =
                    Windows.Storage.ApplicationData.Current.LocalFolder;
                Windows.Storage.StorageFolder folder =
                    await storageFolder.CreateFolderAsync("TTS",
                    Windows.Storage.CreationCollisionOption.OpenIfExists);
                Windows.Storage.StorageFile file =
                    await folder.CreateFileAsync("Speakers.json",
                    Windows.Storage.CreationCollisionOption.ReplaceExisting);

                HttpClient httpClient = new HttpClient();
                var response = await httpClient.GetAsync(
                    new Uri("https://cyprinus-carpio-vits-api.hf.space/voice/speakers"));
                var inputStream = await response.Content.ReadAsInputStreamAsync();
                var stream = await file.OpenStreamForWriteAsync();
                await inputStream.AsStreamForRead().CopyToAsync(stream);
                stream.Dispose();

                return file;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
