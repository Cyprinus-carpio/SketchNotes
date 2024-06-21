using SharpDX.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace SketchNotes.ContentDialogs
{
    public sealed partial class SelectCharacterContentDialog : ContentDialog
    {
        public SelectCharacterContentDialog()
        {
            InitializeComponent();
        }

        private int SpeechId = 0;
        private string SpeechName = "";
        private List<Characters> SpeechCharacters = new List<Characters>();
        ObservableCollection<Characters> FilteredCharacters;

        private class Characters
        {
            public string Name { get; set; }
            public string Language { get; set; }
            public int Id { get; set; }
        }

        private void GetCachedSettings()
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            var speechId = container.Values["SpeechId"];

            if (speechId != null)
            {
                SpeechId = (int)speechId;
            }
        }

        private async Task GetCharacters()
        {
            try
            {
                var file = await TTS.SpeechClient.GetSpeakers();
                string jsonString = File.ReadAllText(file.Path, Encoding.Default);

                JsonObject jsonObject;

                if (JsonValue.TryParse(jsonString, out JsonValue root))
                {
                    jsonObject = root.GetObject();

                    JsonArray jsonArray = jsonObject.GetNamedArray("VITS");

                    foreach (var item in jsonArray)
                    {
                        var keys = item.GetObject();
                        var languages = keys["lang"].GetArray();

                        List<string> list = new List<string>();

                        foreach (var language in languages)
                        {
                            string value = language.GetString();

                            if (value == "zh")
                            {
                                list.Add("中文");
                            }
                            else if (value == "ja")
                            {
                                list.Add("日语");
                            }
                            else if (value == "en")
                            {
                                list.Add("英语");
                            }
                            else
                            {
                                list.Add("其他");
                            }
                        }

                        string[] array = list.ToArray();
                        string output = string.Join("、", array);

                        Characters character = new Characters
                        {
                            Language = output,
                            Name = keys["name"].GetString(),
                            Id = (int)keys["id"].GetNumber()
                        };

                        SpeechCharacters.Add(character);
                    }

                    FilteredCharacters = new ObservableCollection<Characters>(SpeechCharacters);
                    SpeechListView.ItemsSource = FilteredCharacters;
                }

                ErrorInfoBar.IsOpen = false;
            }
            catch (Exception ex)
            {
                ErrorInfoBar.Message = "无法加载角色列表。\n\n" + ex;
                ErrorInfoBar.IsOpen = true;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            container.Values["SpeechName"] = SpeechName;
            container.Values["SpeechId"] = SpeechId;
        }

        private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await GetCharacters();
            GetCachedSettings();

            if (SpeechId < SpeechListView.Items.Count)
            {
                SpeechListView.SelectedIndex = SpeechId;
            }
            else
            {
                SpeechListView.SelectedIndex = 0;
            }

            MainProgressRing.Visibility = Visibility.Collapsed;
        }

        private bool Filter(Characters characters)
        {
            return characters.Name.Contains(
                SearchAutoSuggestBox.Text, StringComparison.InvariantCultureIgnoreCase);
        }

        private void RemoveNonMatching(IEnumerable<Characters> filteredData)
        {
            for (int i = FilteredCharacters.Count - 1; i >= 0; i--)
            {
                var item = FilteredCharacters[i];
                // If characters is not in the filtered argument list,
                // remove it from the ListView's source.
                if (!filteredData.Contains(item))
                {
                    FilteredCharacters.Remove(item);
                }
            }
        }

        private void AddBackCharacters(IEnumerable<Characters> filteredData)
        {
            foreach (var item in filteredData)
            {
                // If the item in the filtered list is not currently in
                // the ListView's source collection, add it back in.
                if (!FilteredCharacters.Contains(item))
                {
                    FilteredCharacters.Add(item);
                }
            }
        }

        private void SearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var filtered = SpeechCharacters.Where(characters => Filter(characters));
            RemoveNonMatching(filtered);
            AddBackCharacters(filtered);
        }

        private void SpeechListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpeechListView.SelectedItem != null)
            {
                SpeechId = FilteredCharacters[SpeechListView.SelectedIndex].Id;
                SpeechName = SpeechCharacters[SpeechId].Name;
                IdTextBlock.Text = SpeechId.ToString();
                NameTextBlock.Text = SpeechName;
            }
        }
    }
}