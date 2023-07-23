using System.Threading.Tasks;

namespace UABEANext3.Util
{
    public static class MessageBoxUtil
    {
        public static async Task<MessageBoxResult> ShowDialog(string header, string message)
        {
            var window = WindowUtils.GetMainWindow();
            MessageBox mb = new MessageBox(header, message, MessageBoxType.OK);
            return await mb.ShowDialog<MessageBoxResult>(window);
        }

        public static async Task<MessageBoxResult> ShowDialog(string header, string message, MessageBoxType buttons)
        {
            var window = WindowUtils.GetMainWindow();
            MessageBox mb = new MessageBox(header, message, buttons);
            return await mb.ShowDialog<MessageBoxResult>(window);
        }

        public static async Task<string> ShowDialogCustom(string header, string message, params string[] buttons)
        {
            var window = WindowUtils.GetMainWindow();
            MessageBox mb = new MessageBox(header, message, MessageBoxType.Custom, buttons);
            MessageBoxResult res = await mb.ShowDialog<MessageBoxResult>(window);
            if (res == MessageBoxResult.CustomButtonA)
                return buttons[0];
            else if (res == MessageBoxResult.CustomButtonB)
                return buttons[1];
            else if (res == MessageBoxResult.CustomButtonC)
                return buttons[2];

            return string.Empty;
        }
    }
}
