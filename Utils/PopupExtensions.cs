using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Views;

namespace mindvault.Utils
{
    public static class PopupExtensionsCompat
    {
        public static Task<object?> ShowPopupAsync(this Page page, Popup popup)
        {
            var tcs = new TaskCompletionSource<object?>();

            popup.Closed += (s, e) =>
            {
                // In CommunityToolkit, Popup has Result; fallback to null if unavailable
                object? result = (popup as dynamic)?.Result;
                tcs.TrySetResult(result);
            };

            page.ShowPopup(popup);
            return tcs.Task;
        }
    }
}
