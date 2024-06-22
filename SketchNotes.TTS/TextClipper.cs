using System;
using System.Threading.Tasks;

namespace SketchNotes.TTS
{
    public class TextClipper
    {
        public static Task<string[]> ClipText(string text)
        {         
            return Task.Run(() =>
            {
                text = text.Replace("\n","")
                    .Replace(".", ".\n")
                    .Replace("。", "。\n")
                    .Replace("!", "!\n")
                    .Replace("！", "！\n")
                    .Replace("?", "?\n")
                    .Replace("？", "？\n")
                    .Replace(";", ";\n")
                    .Replace("；","；\n");
                string[] output = text.Split("\n", StringSplitOptions.RemoveEmptyEntries);

                return output;
            });
        }
    }
}
