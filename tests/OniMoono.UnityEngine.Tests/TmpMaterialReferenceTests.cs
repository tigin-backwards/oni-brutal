using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Xunit;
using Object = UnityEngine.Object;

namespace OniMoono.Tests
{
    // MaterialReferenceManager and the rich-text tags that find their assets in it
    // (docs/cleanroom/textmeshpro.md, "MaterialReferenceManager and TMP_Text.OnSpriteAssetRequest").
    // The registry is process-wide and keeps what is added, so each test uses names of its own.
    public class TmpMaterialReferenceTests : EngineTest
    {
        static int Hash(string s) => TMP_TextUtilities.GetHashCode(s);

        static TextMeshProUGUI Text(string text) => TmpTestSupport.NewText(text, TmpTestSupport.Graystroke, 14f, new Vector2(500f, 60f));

        static TMP_SpriteAsset Sprites() => TmpTestSupport.FindAsset<TMP_SpriteAsset>("oni_sprite_assets");

        // A copy of the game's sprite asset under another name.
        static TMP_SpriteAsset SpriteCopy(string name)
        {
            var copy = Object.Instantiate(Sprites());
            copy.name = name;
            return copy;
        }

        static TMP_FontAsset NewFont(string name)
        {
            var font = TmpTestSupport.Load<Font>("sharedassets0.assets", "LiberationSans");
            var asset = TMP_FontAsset.CreateFontAsset(font, 48, 5, GlyphRenderMode.SDFAA, 256, 256, AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            return asset;
        }

        static string Characters(TMP_Text t) => new(t.textInfo.characterInfo.Take(t.textInfo.characterCount).Select(c => c.character).ToArray());

        // The requests the handler sees while `body` runs, and what it returns.
        static List<(int Hash, string Name)> Requests(Func<string, TMP_SpriteAsset> serve, Action body)
        {
            var seen = new List<(int, string)>();
            Func<int, string, TMP_SpriteAsset> handler = (h, n) => { seen.Add((h, n)); return serve(n); };
            TMP_Text.OnSpriteAssetRequest += handler;
            try { body(); }
            finally { TMP_Text.OnSpriteAssetRequest -= handler; }
            return seen;
        }

        [Fact]
        public void TheRegistryHasTheShippedMembers()
        {
            var t = typeof(MaterialReferenceManager);
            Assert.True(t.IsPublic);
            Assert.NotNull(t.GetConstructor(Type.EmptyTypes));
            Assert.NotNull(t.GetProperty("instance")?.GetMethod?.IsStatic == true ? t : null);
            Assert.NotNull(t.GetMethod("AddFontAsset", new[] { typeof(TMP_FontAsset) }));
            Assert.NotNull(t.GetMethod("AddSpriteAsset", new[] { typeof(TMP_SpriteAsset) }));
            Assert.NotNull(t.GetMethod("AddSpriteAsset", new[] { typeof(int), typeof(TMP_SpriteAsset) }));
            Assert.NotNull(t.GetMethod("AddFontMaterial", new[] { typeof(int), typeof(Material) }));
            Assert.NotNull(t.GetMethod("AddColorGradientPreset", new[] { typeof(int), typeof(TMP_ColorGradient) }));
            Assert.False(t.GetMethod("Contains", new[] { typeof(TMP_FontAsset) }).IsStatic);
            Assert.False(t.GetMethod("Contains", new[] { typeof(TMP_SpriteAsset) }).IsStatic);
            foreach (var (name, type) in new[] { ("TryGetFontAsset", typeof(TMP_FontAsset)), ("TryGetSpriteAsset", typeof(TMP_SpriteAsset)), ("TryGetMaterial", typeof(Material)), ("TryGetColorGradientPreset", typeof(TMP_ColorGradient)) })
                Assert.True(t.GetMethod(name, new[] { typeof(int), type.MakeByRefType() }).IsStatic);
            Assert.NotNull(typeof(TMP_Text).GetEvent("OnSpriteAssetRequest"));
            Assert.Equal(typeof(Func<int, string, TMP_SpriteAsset>), typeof(TMP_Text).GetEvent("OnSpriteAssetRequest").EventHandlerType);
            Assert.Equal(typeof(Func<int, string, TMP_FontAsset>), typeof(TMP_Text).GetEvent("OnFontAssetRequest").EventHandlerType);
        }

        [Fact]
        public void TheGamesAssetsAreNotRegisteredAtFirst()
        {
            TmpTestSupport.LoadGameData();
            Assert.False(MaterialReferenceManager.TryGetFontAsset(Hash("NotoSans-Regular"), out _));
            Assert.False(MaterialReferenceManager.TryGetFontAsset(Hash("GRAYSTROKE REGULAR SDF"), out _));
            Assert.False(MaterialReferenceManager.TryGetSpriteAsset(TMP_Settings.defaultSpriteAsset.hashCode, out _));
        }

        [Fact]
        public void SpriteAssetsKeepTheFirstUnderAKey()
        {
            var a = SpriteCopy("mrm_first");
            var b = SpriteCopy("mrm_second");
            MaterialReferenceManager.AddSpriteAsset(Hash("mrm_first"), a);
            MaterialReferenceManager.AddSpriteAsset(Hash("mrm_first"), b);
            Assert.True(MaterialReferenceManager.TryGetSpriteAsset(Hash("mrm_first"), out var got));
            Assert.Same(a, got);
            Assert.True(MaterialReferenceManager.instance.Contains(a));
            MaterialReferenceManager.AddSpriteAsset(b);
            Assert.True(MaterialReferenceManager.TryGetSpriteAsset(b.hashCode, out got));
            Assert.Same(b, got);
        }

        [Fact]
        public void FontAssetsRegisterTheirMaterialToo()
        {
            var a = NewFont("MrmFontA");
            MaterialReferenceManager.AddFontAsset(a);
            Assert.True(MaterialReferenceManager.TryGetFontAsset(a.hashCode, out var got));
            Assert.Same(a, got);
            Assert.True(MaterialReferenceManager.instance.Contains(a));
            // The same font again does nothing, though its material key is taken.
            MaterialReferenceManager.AddFontAsset(a);
            // Another font under a taken key is ignored.
            var d = NewFont("MrmFontD");
            d.hashCode = a.hashCode;
            d.materialHashCode = Hash("MrmFontD material");
            MaterialReferenceManager.AddFontAsset(d);
            Assert.True(MaterialReferenceManager.TryGetFontAsset(a.hashCode, out got));
            Assert.Same(a, got);
            Assert.False(MaterialReferenceManager.TryGetMaterial(Hash("MrmFontD material"), out _));
            // A font whose material key is taken is registered, then the material's add throws.
            var b = NewFont("MrmFontB");
            b.materialHashCode = Hash("MrmFontB material");
            MaterialReferenceManager.AddFontAsset(b);
            Assert.True(MaterialReferenceManager.TryGetMaterial(b.materialHashCode, out var material));
            Assert.Same(b.material, material);
            var c = NewFont("MrmFontC");
            c.materialHashCode = b.materialHashCode;
            var e = Assert.Throws<ArgumentException>(() => MaterialReferenceManager.AddFontAsset(c));
            Assert.Contains("An item with the same key has already been added", e.Message);
            Assert.True(MaterialReferenceManager.TryGetFontAsset(c.hashCode, out got));
            Assert.Same(c, got);
            Assert.Throws<NullReferenceException>(() => MaterialReferenceManager.AddFontAsset(null));
        }

        // ----- Sprite tags -----

        [Fact]
        public void AnUnregisteredSpriteAssetStaysAsTextAndIsRequested()
        {
            const string source = "a<sprite=\"mrm_unreg\" index=0>b";
            TextMeshProUGUI t = null;
            var seen = Requests(_ => null, () => t = Text(source));
            Assert.Equal(source.Length, t.textInfo.characterCount);
            Assert.Equal(source, Characters(t));
            Assert.Contains((Hash("mrm_unreg"), "mrm_unreg"), seen);
            // Asked again at each later layout.
            seen = Requests(_ => null, () => t.ForceMeshUpdate(false, true));
            Assert.Contains((Hash("mrm_unreg"), "mrm_unreg"), seen);
            // No handler: still text.
            t.ForceMeshUpdate(false, true);
            Assert.Equal(source.Length, t.textInfo.characterCount);
            // Asked again at the next layout, with the text unchanged: an asset registered since
            // is drawn.
            seen = Requests(_ => null, () => t.ForceMeshUpdate());
            Assert.Contains((Hash("mrm_unreg"), "mrm_unreg"), seen);
            MaterialReferenceManager.AddSpriteAsset(Hash("mrm_unreg"), SpriteCopy("mrm_unreg"));
            t.ForceMeshUpdate();
            Assert.Equal(3, t.textInfo.characterCount);
            Assert.Equal(TMP_TextElementType.Sprite, t.textInfo.characterInfo[1].elementType);
        }

        [Fact]
        public void ARequestedSpriteAssetIsDrawnAndRegistered()
        {
            var served = SpriteCopy("mrm_req");
            TextMeshProUGUI t = null;
            var seen = Requests(n => n == "mrm_req" ? served : null, () => t = Text("a<sprite=\"mrm_req\" index=0>b"));
            Assert.Contains((Hash("mrm_req"), "mrm_req"), seen);
            Assert.Equal(3, t.textInfo.characterCount);
            Assert.Equal(TMP_TextElementType.Sprite, t.textInfo.characterInfo[1].elementType);
            Assert.Equal(2, t.textInfo.materialCount);
            Assert.True(MaterialReferenceManager.TryGetSpriteAsset(Hash("mrm_req"), out var got));
            Assert.Same(served, got);
            // Later layouts find it registered, with no request.
            seen = Requests(_ => null, () => Text("x<sprite=\"mrm_req\" index=0>y"));
            Assert.Empty(seen);
        }

        [Fact]
        public void OnlyTheLastHandlersResultCounts()
        {
            var served = SpriteCopy("mrm_two");
            Func<int, string, TMP_SpriteAsset> first = (_, n) => n == "mrm_two" ? served : null;
            int secondCalls = 0;
            Func<int, string, TMP_SpriteAsset> second = (_, _) => { secondCalls++; return null; };
            TMP_Text.OnSpriteAssetRequest += first;
            TMP_Text.OnSpriteAssetRequest += second;
            try
            {
                var t = Text("a<sprite=\"mrm_two\" index=0>b");
                Assert.True(secondCalls > 0);
                Assert.Equal("a<sprite=\"mrm_two\" index=0>b".Length, t.textInfo.characterCount);
                Assert.False(MaterialReferenceManager.TryGetSpriteAsset(Hash("mrm_two"), out _));
            }
            finally
            {
                TMP_Text.OnSpriteAssetRequest -= first;
                TMP_Text.OnSpriteAssetRequest -= second;
            }
        }

        [Fact]
        public void WhichSpriteTagsAreRequested()
        {
            // Unquoted, the name runs to the tag's end.
            var seen = Requests(_ => null, () => Text("a<sprite=mrm_unq index=0>b"));
            Assert.Contains((Hash("mrm_unq index=0"), "mrm_unq index=0"), seen);
            // Not for the default sprite asset's sprites, by index or name, nor with rich text off.
            seen = Requests(_ => null, () =>
            {
                var byIndex = Text("a<sprite=0>b");
                Assert.Equal(TMP_TextElementType.Sprite, byIndex.textInfo.characterInfo[1].elementType);
                var byName = Text("a<sprite name=\"mrm_nope\">b");
                Assert.Equal("a<sprite name=\"mrm_nope\">b", Characters(byName));
                var plain = TmpTestSupport.NewText("", TmpTestSupport.Graystroke, 14f, new Vector2(500f, 60f));
                plain.richText = false;
                plain.text = "a<sprite=\"mrm_plain\" index=0>b";
                plain.ForceMeshUpdate(false, true);
                Assert.Equal("a<sprite=\"mrm_plain\" index=0>b", Characters(plain));
            });
            Assert.Equal("", string.Join("|", seen.Select(r => r.Name)));
        }

        [Fact]
        public void SpriteTagsFindAssetsByTheHashOfTheirName()
        {
            var aliased = SpriteCopy("mrm_x");
            MaterialReferenceManager.AddSpriteAsset(Hash("mrm_alias"), aliased);
            // Any case; under the key it was added with, not its name.
            var t = Text("a<sprite=\"MRM_ALIAS\" index=0>b");
            Assert.Equal(3, t.textInfo.characterCount);
            Assert.Same(aliased.spriteCharacterTable[0], t.textInfo.characterInfo[1].textElement);
            var seen = Requests(_ => null, () => t = Text("a<sprite=\"mrm_x\" index=0>b"));
            Assert.Contains((Hash("mrm_x"), "mrm_x"), seen);
            Assert.Equal("a<sprite=\"mrm_x\" index=0>b", Characters(t));
            // A sprite name the asset has, and one it lacks (sprite 0).
            var named = aliased.spriteCharacterTable[2].name;
            t = Text("a<sprite=\"mrm_alias\" name=\"" + named + "\">b");
            Assert.Same(aliased.spriteCharacterTable[2], t.textInfo.characterInfo[1].textElement);
            t = Text("a<sprite=\"mrm_alias\" name=\"mrm_lacks\">b");
            Assert.Same(aliased.spriteCharacterTable[0], t.textInfo.characterInfo[1].textElement);
            // An index past the end stays as text.
            t = Text("a<sprite=\"mrm_alias\" index=100000>b");
            Assert.Equal("a<sprite=\"mrm_alias\" index=100000>b", Characters(t));
        }

        [Fact]
        public void TheTextsOwnSpriteAssetIsNotFoundByName()
        {
            var own = SpriteCopy("mrm_own");
            var t = TmpTestSupport.NewText("", TmpTestSupport.Graystroke, 14f, new Vector2(500f, 60f));
            t.spriteAsset = own;
            t.text = "a<sprite=\"mrm_own\" index=0>b";
            t.ForceMeshUpdate();
            Assert.Equal("a<sprite=\"mrm_own\" index=0>b", Characters(t));
            // Its sprites by name alone are found.
            t.text = "a<sprite name=\"" + own.spriteCharacterTable[1].name + "\">b";
            t.ForceMeshUpdate();
            Assert.Equal(3, t.textInfo.characterCount);
        }

        // The game's own tags name an asset under Resources ("Sprite Assets/oni_sprite_assets"):
        // the food and battery tooltips.
        [Fact]
        public void TheGamesSpriteTagsAreDrawn()
        {
            var sprites = Sprites();
            var t = Text("a<sprite=\"oni_sprite_assets\" name=\"oni_sprite_assets_germs\">b");
            Assert.Equal(3, t.textInfo.characterCount);
            var element = t.textInfo.characterInfo[1].textElement as TMP_SpriteCharacter;
            Assert.NotNull(element);
            Assert.Equal("oni_sprite_assets_germs", element.name);
            Assert.Same(sprites.material, t.textInfo.meshInfo[1].material);
            Assert.True(MaterialReferenceManager.TryGetSpriteAsset(Hash("oni_sprite_assets"), out var registered));
            Assert.Same(sprites, registered);
        }

        // A sprite asset under Resources that no tag named yet (only this test names it): the
        // request is raised first, then the asset is loaded and registered (measured with
        // oni_sprite_assets and EmojiOne).
        [Fact]
        public void AResourcesSpriteAssetIsRequestedThenLoadedAndRegistered()
        {
            TextMeshProUGUI t = null;
            var seen = Requests(_ => null, () => t = Text("a<sprite=\"PS4Sheet\" index=0>b"));
            Assert.Equal(new[] { (Hash("PS4Sheet"), "PS4Sheet") }, seen.Distinct());
            Assert.Equal(TMP_TextElementType.Sprite, t.textInfo.characterInfo[1].elementType);
            Assert.True(MaterialReferenceManager.TryGetSpriteAsset(Hash("ps4sheet"), out var registered));
            Assert.Equal("PS4Sheet", registered.name, ignoreCase: true);
            seen = Requests(_ => null, () => Text("a<sprite=\"PS4Sheet\" index=0>b"));
            Assert.Empty(seen);
        }

        // ----- Font tags -----

        // The opening tag stays as text; the closing one is taken as a tag all the same
        // (measured: "a<font=\"ProbeNoSuchFont\">bc").
        [Fact]
        public void AnUnregisteredFontStaysAsText()
        {
            foreach (var name in new[] { "Economica-Bold-OTF SDF", "GRAYSTROKE NAME SDF", "GRAYSTROKE REGULAR SDF", "MrmUnknownFont", "" })
            {
                var t = Text("a<font=\"" + name + "\">b</font>c");
                Assert.Equal("a<font=\"" + name + "\">bc", Characters(t));
                Assert.All(t.textInfo.characterInfo.Take(t.textInfo.characterCount), c => Assert.Same(TmpTestSupport.Graystroke, c.fontAsset));
            }
            // "default" is the text's font; a lone closing tag is taken.
            Assert.Equal("abc", Characters(Text("a<font=\"default\">b</font>c")));
            Assert.Equal("abc", Characters(Text("ab</font>c")));
        }

        [Fact]
        public void AnUnregisteredFontIsRequestedAtEachLayout()
        {
            var seen = new List<(int, string)>();
            Func<int, string, TMP_FontAsset> handler = (h, n) => { seen.Add((h, n)); return null; };
            TMP_Text.OnFontAssetRequest += handler;
            try
            {
                var t = Text("a<font=\"MrmFontRequested\">b</font>c");
                Assert.Contains((Hash("MrmFontRequested"), "MrmFontRequested"), seen);
                seen.Clear();
                t.ForceMeshUpdate(false, true);
                Assert.Contains((Hash("MrmFontRequested"), "MrmFontRequested"), seen);
            }
            finally { TMP_Text.OnFontAssetRequest -= handler; }
        }

        [Fact]
        public void FontTagsFindRegisteredFontsByTheHashOfTheirName()
        {
            var a = NewFont("MrmFontTag");
            a.materialHashCode = Hash("MrmFontTag material");
            MaterialReferenceManager.AddFontAsset(a);
            foreach (var tag in new[] { "<font=\"MrmFontTag\">", "<font=\"mrmfonttag\">", "<font=MrmFontTag>" })
            {
                var t = Text("a" + tag + "b</font>c");
                Assert.Equal("abc", Characters(t));
                Assert.Same(a, t.textInfo.characterInfo[1].fontAsset);
                Assert.NotSame(a, t.textInfo.characterInfo[2].fontAsset);
                Assert.Equal(2, t.textInfo.materialCount);
            }
            // Renamed after it was added, it is found by its old name only.
            a.name = "MrmFontTag2";
            Assert.Equal("abc", Characters(Text("a<font=\"MrmFontTag\">b</font>c")));
            Assert.Equal("a<font=\"MrmFontTag2\">bc", Characters(Text("a<font=\"MrmFontTag2\">b</font>c")));
            // Under another key, by that key only.
            var c = NewFont("MrmFontAliased");
            c.hashCode = Hash("MrmAliasC");
            c.materialHashCode = Hash("MrmFontAliased material");
            MaterialReferenceManager.AddFontAsset(c);
            Assert.Same(c, Text("a<font=\"MrmAliasC\">b</font>c").textInfo.characterInfo[1].fontAsset);
            Assert.Equal("a<font=\"MrmFontAliased\">bc", Characters(Text("a<font=\"MrmFontAliased\">b</font>c")));
        }

        // The one font tag in the game's code (TextMeshPro's example scripts, not shown in its
        // UI) names a font and a material under Resources ("Fonts & Materials/"): both are
        // found, and registered (measured). The original draws "BANG" from Bangers SDF, a dynamic
        // asset with empty tables; this implementation's font engine adds no glyphs to it, so
        // the characters fall back (not part of this lookup).
        [Fact]
        public void TheGamesFontTagFindsItsAssets()
        {
            var t = Text("a<font=\"Bangers SDF\" material=\"Bangers SDF - Drop Shadow\">BANG</font>c");
            Assert.Equal("aBANGc", Characters(t));
            Assert.True(MaterialReferenceManager.TryGetFontAsset(Hash("bangers sdf"), out var font));
            Assert.Equal("Bangers SDF", font.name);
            Assert.True(MaterialReferenceManager.TryGetMaterial(Hash("Bangers SDF - Drop Shadow"), out var material));
            Assert.Equal("Bangers SDF - Drop Shadow", material.name);
            // A Resources material by a <material> tag.
            t = Text("a<material=\"LiberationSans SDF - Outline\">b</material>c");
            Assert.Equal("abc", Characters(t));
        }
    }
}
