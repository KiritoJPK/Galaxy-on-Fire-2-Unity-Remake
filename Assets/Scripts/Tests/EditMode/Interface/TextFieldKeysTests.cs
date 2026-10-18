// TextFieldKeysTests.cs
// TextFieldKeys.InTextField: the menus (StationMenu, MainMenu) let a touch focus only a text input field, for the on-screen
// keyboard. The number fields (IntegerField, FloatField: the Mod Builder's stats, prices and positions) are text input
// fields too, though not TextFields: on a phone their focus was taken away and they couldn't be typed into.

using GoF2Remake.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GoF2Remake.Tests
{
    public class TextFieldKeysTests
    {
        static VisualElement Inner(VisualElement field) => field.Q(className: "unity-text-element") ?? field;

        [Test]
        public void TextAndNumberFieldsTakeTheFocusButtonsDont()
        {
            Assert.IsTrue(TextFieldKeys.InTextField(Inner(new TextField("t"))));
            Assert.IsTrue(TextFieldKeys.InTextField(Inner(new IntegerField("i"))), "an IntegerField");
            Assert.IsTrue(TextFieldKeys.InTextField(Inner(new FloatField("f"))), "a FloatField");
            Assert.IsFalse(TextFieldKeys.InTextField(new Button()));
            Assert.IsFalse(TextFieldKeys.InTextField(new Slider()));
            Assert.IsFalse(TextFieldKeys.InTextField(null));
        }
    }
}
