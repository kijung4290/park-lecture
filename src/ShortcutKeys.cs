using System.Collections.Generic;
using System.Windows.Forms;

namespace ParkLecture
{
    internal sealed class ShortcutKeyChoice
    {
        internal readonly Keys Key;
        internal ShortcutKeyChoice(Keys key) { Key = key; }
        public override string ToString() { return ShortcutKeys.Label(Key, true); }
    }
    internal static class ShortcutKeys
    {
        internal static string Label(Keys key, bool describe)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
            { string number = ((int)key - (int)Keys.D0).ToString(); return describe ? Words.T("숫자 " + number + " (위쪽)", number + " (top row)") : number; }
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            { string number = ((int)key - (int)Keys.NumPad0).ToString(); return Words.T("키패드 " + number + (describe ? " (오른쪽)" : ""), "Numpad " + number); }
            return key.ToString();
        }
        internal static IEnumerable<ShortcutKeyChoice> Choices()
        {
            foreach (var range in new[] { new[] { Keys.D0, Keys.D9 }, new[] { Keys.NumPad0, Keys.NumPad9 }, new[] { Keys.A, Keys.Z }, new[] { Keys.F1, Keys.F12 } })
                for (int key = (int)range[0]; key <= (int)range[1]; key++) yield return new ShortcutKeyChoice((Keys)key);
        }
        internal static string Format(uint modifiers, int key)
        {
            return ((modifiers & 2) != 0 ? "Ctrl+" : "") + ((modifiers & 4) != 0 ? "Shift+" : "") + ((modifiers & 1) != 0 ? "Alt+" : "") + Label((Keys)key, false);
        }
    }
}
