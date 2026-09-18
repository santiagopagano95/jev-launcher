using System.Windows;
using JevLauncher.Core;

namespace JevLauncher.App;

public sealed class WindowsClipboardKindProvider : IClipboardKindProvider
{
    public string Kind
    {
        get
        {
            try
            {
                return Clipboard.ContainsImage() ? "image"
                    : Clipboard.ContainsText() ? "text"
                    : Clipboard.ContainsFileDropList() ? "files"
                    : "empty";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
