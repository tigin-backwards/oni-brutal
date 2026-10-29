using Brutal.GlfwApi;
using OniMoono.Desktop;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    public class KeyboardLayoutTests
    {
        const int Dead = -1;

        // The original Linux player on an Xvfb with `setxkbmap fr` (KeyboardLayout's table,
        // tests/e2e/keyboard-layout.yaml): each key's first two levels in the fr layout, and the
        // KeyCode it reported.
        [Theory]
        [InlineData(Dead, '~', KeyCode.None)]                 // ² ~
        [InlineData((uint)'&', (uint)'1', KeyCode.Alpha1)]
        [InlineData(Dead, '2', KeyCode.Alpha2)]               // é 2
        [InlineData((uint)'"', (uint)'3', KeyCode.Alpha3)]
        [InlineData('\'', '4', KeyCode.Alpha4)]
        [InlineData((uint)'(', (uint)'5', KeyCode.Alpha5)]
        [InlineData((uint)'-', (uint)'6', KeyCode.Alpha6)]
        [InlineData(Dead, '7', KeyCode.Alpha7)]               // è 7
        [InlineData((uint)'_', (uint)'8', KeyCode.Alpha8)]
        [InlineData(Dead, '9', KeyCode.Alpha9)]               // ç 9
        [InlineData(Dead, '0', KeyCode.Alpha0)]               // à 0
        [InlineData(')', Dead, KeyCode.RightParen)]           // ) °
        [InlineData((uint)'=', (uint)'+', KeyCode.Equals)]
        [InlineData((uint)'a', (uint)'A', KeyCode.A)]
        [InlineData((uint)'z', (uint)'Z', KeyCode.Z)]
        [InlineData(Dead, Dead, KeyCode.None)]                // dead ^, dead ¨
        [InlineData('$', Dead, KeyCode.Dollar)]               // $ £
        [InlineData((uint)'q', (uint)'Q', KeyCode.Q)]
        [InlineData((uint)'m', (uint)'M', KeyCode.M)]
        [InlineData(Dead, '%', KeyCode.None)]                 // ù %
        [InlineData('*', Dead, KeyCode.Asterisk)]             // * µ
        [InlineData((uint)'<', (uint)'>', KeyCode.Less)]
        [InlineData((uint)'w', (uint)'W', KeyCode.W)]
        [InlineData((uint)',', (uint)'?', KeyCode.Comma)]
        [InlineData((uint)';', (uint)'.', KeyCode.Semicolon)]
        [InlineData((uint)':', (uint)'/', KeyCode.Colon)]
        [InlineData('!', Dead, KeyCode.Exclaim)]              // ! §
        public void FrenchLayoutGivesTheOriginalsKeyCodes(int level1, int level2, KeyCode expected)
            => Assert.Equal(expected, KeyboardLayout.FromCharacters(level1, level2));

        // On a US layout the mapping gives what the positional table gave before.
        [Theory]
        [InlineData(GlfwKey.GraveAccent, '`', '~')]
        [InlineData(GlfwKey.Number1, '1', '!')]
        [InlineData(GlfwKey.Number0, '0', ')')]
        [InlineData(GlfwKey.Minus, '-', '_')]
        [InlineData(GlfwKey.Equal, '=', '+')]
        [InlineData(GlfwKey.Q, 'q', 'Q')]
        [InlineData(GlfwKey.LeftBracket, '[', '{')]
        [InlineData(GlfwKey.RightBracket, ']', '}')]
        [InlineData(GlfwKey.Backslash, '\\', '|')]
        [InlineData(GlfwKey.Semicolon, ';', ':')]
        [InlineData(GlfwKey.Apostrophe, '\'', '"')]
        [InlineData(GlfwKey.Comma, ',', '<')]
        [InlineData(GlfwKey.Period, '.', '>')]
        [InlineData(GlfwKey.Slash, '/', '?')]
        [InlineData(GlfwKey.M, 'm', 'M')]
        public void UsLayoutKeepsThePositions(GlfwKey key, int level1, int level2)
            => Assert.Equal(KeyMap.ToUnity(key), KeyboardLayout.FromCharacters(level1, level2));

        [Theory]
        [InlineData(0x61ul, 'a')]            // XK_a
        [InlineData(0x26ul, '&')]            // XK_ampersand
        [InlineData(0xe9ul, -1)]             // XK_eacute: not ASCII
        [InlineData(0xfe52ul, -1)]           // XK_dead_circumflex
        [InlineData(0x1000031ul, '1')]       // a Unicode keysym
        [InlineData(0ul, -1)]                // NoSymbol
        public void KeysymsAsCharacters(ulong keysym, int expected)
            => Assert.Equal(expected, KeyboardLayout.KeysymCharacter(keysym));

        // Windows: the original's measured table, by virtual key (the French layout's for each
        // AZERTY key here).
        [Theory]
        [InlineData((uint)'A', KeyCode.A)]               // the key at the US Q position
        [InlineData((uint)'Q', KeyCode.Q)]               // the key at the US A position
        [InlineData((uint)'1', KeyCode.Alpha1)]          // types &
        [InlineData(0xDBu, KeyCode.LeftBracket)]         // VK_OEM_4: the ) key
        [InlineData(0xBAu, KeyCode.Semicolon)]           // VK_OEM_1: the $ key
        [InlineData(0xBEu, KeyCode.Period)]              // VK_OEM_PERIOD: the ; key
        [InlineData(0xDFu, KeyCode.BackQuote)]           // VK_OEM_8: the ! key
        [InlineData(0xC0u, KeyCode.BackQuote)]           // VK_OEM_3: the ù key
        [InlineData(0xDEu, KeyCode.Quote)]               // VK_OEM_7: the ² key
        [InlineData(0xDDu, KeyCode.RightBracket)]        // VK_OEM_6: the dead ^ key
        [InlineData(0xE2u, KeyCode.Backslash)]           // VK_OEM_102: the < key
        [InlineData(0xBDu, KeyCode.Minus)]
        [InlineData(0u, KeyCode.None)]
        public void WindowsVirtualKeys(uint vk, KeyCode expected)
            => Assert.Equal(expected, KeyboardLayout.FromVirtualKey(vk));

        [Fact]
        public void OnlyTheMainBlocksCharacterKeysFollowTheLayout()
        {
            Assert.True(KeyboardLayout.IsPrintable(GlfwKey.A));
            Assert.True(KeyboardLayout.IsPrintable(GlfwKey.World1));
            Assert.True(KeyboardLayout.IsPrintable(GlfwKey.Apostrophe));
            Assert.False(KeyboardLayout.IsPrintable(GlfwKey.Space));
            Assert.False(KeyboardLayout.IsPrintable(GlfwKey.Kp1));
            Assert.False(KeyboardLayout.IsPrintable(GlfwKey.Left));
            Assert.False(KeyboardLayout.IsPrintable(GlfwKey.F1));
        }
    }
}
