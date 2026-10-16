// EntryPatchTests.cs
// Feature: CLAUDE.md "Mods" > Mod Builder MCP server, patch_entry (Modding.EntryPatch): small edits of an entry by path,
// all or nothing, the rest of the entry left exactly as it was (a quest's graph layout: pos, startPos, values, loose).
// Expected values: the operations' definitions in EntryPatch's header (JSON Patch's, RFC 6902, with plainer paths).

using GoF2Remake.Modding;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GoF2Remake.Tests
{
    public class EntryPatchTests
    {
        static JObject Quest() => JObject.Parse(@"{
            ""format"": ""gof2quest"", ""title"": ""T"", ""startPos"": [0, 0],
            ""steps"": [
                { ""type"": ""dialog"", ""pages"": [ { ""speaker"": ""Keith"", ""text"": ""Hi."" } ], ""pos"": [0, 110] },
                { ""type"": ""if"", ""condition"": ""kills > 2"", ""then"": [ { ""type"": ""reward"", ""credits"": 100, ""pos"": [300, 240] } ], ""pos"": [0, 240] },
                { ""type"": ""reward"", ""credits"": 500, ""pos"": [0, 400] }
            ],
            ""values"": [ { ""id"": ""v1"", ""type"": ""number"", ""value"": 3, ""pos"": [-240, 0] } ],
            ""loose"": [ [ { ""type"": ""wait"", ""seconds"": 2, ""pos"": [900, 0] } ] ]
        }");

        static JArray Ops(string json) => JArray.Parse(json);

        static JToken Patch(JObject q, string ops)
        {
            var r = EntryPatch.Apply(q, Ops(ops), out string error);
            Assert.IsNotNull(r, error);
            return r;
        }

        [Test]
        public void SetChangesOneValueAndLeavesTheRest()
        {
            var q = Quest();
            var r = Patch(q, @"[ { ""op"": ""set"", ""path"": ""steps/0/pages/0/text"", ""value"": ""Hello."" } ]");
            Assert.AreEqual("Hello.", (string)r["steps"][0]["pages"][0]["text"]);
            // everything else as it was, the layout too
            r["steps"][0]["pages"][0]["text"] = "Hi.";
            Assert.IsTrue(JToken.DeepEquals(q, r));
            Assert.AreEqual("Hi.", (string)q["steps"][0]["pages"][0]["text"], "the original isn't changed");
        }

        [Test]
        public void SetMakesMissingKeysAndAppendsAtDash()
        {
            var r = Patch(Quest(), @"[ { ""op"": ""set"", ""path"": ""settings/difficulty/level"", ""value"": 2 },
                                       { ""op"": ""set"", ""path"": ""steps/0/pages/-"", ""value"": { ""speaker"": ""Keith"", ""text"": ""Bye."" } } ]");
            Assert.AreEqual(2, (int)r["settings"]["difficulty"]["level"]);
            Assert.AreEqual(2, ((JArray)r["steps"][0]["pages"]).Count);
        }

        [Test]
        public void InsertIntoABlockAndRemove()
        {
            var r = Patch(Quest(), @"[ { ""op"": ""insert"", ""path"": ""steps/1/then/0"", ""value"": { ""type"": ""radio"", ""speaker"": ""Keith"", ""text"": ""Nice."" } },
                                       { ""op"": ""remove"", ""path"": ""steps/2"" } ]");
            var then = (JArray)r["steps"][1]["then"];
            Assert.AreEqual(2, then.Count);
            Assert.AreEqual("radio", (string)then[0]["type"]);
            Assert.AreEqual("reward", (string)then[1]["type"]);
            Assert.AreEqual(2, ((JArray)r["steps"]).Count);
        }

        [Test]
        public void MoveReadsTheTargetAfterTheRemoval()
        {
            // the last step into the if block's list, at its start
            var r = Patch(Quest(), @"[ { ""op"": ""move"", ""from"": ""steps/2"", ""path"": ""steps/1/then/0"" } ]");
            Assert.AreEqual(2, ((JArray)r["steps"]).Count);
            Assert.AreEqual(500, (int)r["steps"][1]["then"][0]["credits"]);
            Assert.AreEqual(100, (int)r["steps"][1]["then"][1]["credits"]);
            // within one list: the first step to the end
            var r2 = Patch(Quest(), @"[ { ""op"": ""move"", ""from"": ""steps/0"", ""path"": ""steps/-"" } ]");
            Assert.AreEqual("dialog", (string)r2["steps"][2]["type"]);
        }

        [Test]
        public void AFailingOpChangesNothing()
        {
            var q = Quest();
            var r = EntryPatch.Apply(q, Ops(@"[ { ""op"": ""set"", ""path"": ""title"", ""value"": ""New"" },
                                                { ""op"": ""set"", ""path"": ""steps/9/text"", ""value"": ""x"" } ]"), out string error);
            Assert.IsNull(r);
            StringAssert.StartsWith("op 1 (set steps/9/text)", error);
            StringAssert.Contains("the list has 3", error);
            Assert.AreEqual("T", (string)q["title"]);
        }

        [Test]
        public void TestGuardsAgainstAStaleEdit()
        {
            Assert.IsNotNull(EntryPatch.Apply(Quest(), Ops(@"[ { ""op"": ""test"", ""path"": ""steps/2/credits"", ""value"": 500 },
                                                               { ""op"": ""set"", ""path"": ""steps/2/credits"", ""value"": 900 } ]"), out _));
            var r = EntryPatch.Apply(Quest(), Ops(@"[ { ""op"": ""test"", ""path"": ""steps/2/type"", ""value"": ""dialog"" },
                                                     { ""op"": ""remove"", ""path"": ""steps/2"" } ]"), out string error);
            Assert.IsNull(r);
            StringAssert.Contains("read the entry again", error);
        }

        [Test]
        public void BadOperationsAreRefused()
        {
            void Refused(string ops, string part)
            {
                Assert.IsNull(EntryPatch.Apply(Quest(), Ops(ops), out string error), ops);
                StringAssert.Contains(part, error);
            }
            Refused(@"[ { ""op"": ""frobnicate"", ""path"": ""title"" } ]", "op: set, insert");
            Refused(@"[ { ""op"": ""insert"", ""path"": ""title"", ""value"": 1 } ]", "into a list");
            Refused(@"[ { ""op"": ""remove"", ""path"": ""nothing"" } ]", "nothing there");
            Refused(@"[ { ""op"": ""move"", ""from"": ""steps/1"", ""path"": ""steps/1/then/0"" } ]", "into itself");
            Refused(@"[ { ""op"": ""set"", ""path"": ""steps/x"", ""value"": 1 } ]", "takes numbers");
            Assert.IsNull(EntryPatch.Apply(Quest(), new JArray(), out _));
        }

        [Test]
        public void GetReadsAPart()
        {
            var part = EntryPatch.Get(Quest(), "steps/1/then", out string error);
            Assert.IsNotNull(part, error);
            Assert.AreEqual(1, ((JArray)part).Count);
            Assert.IsNull(EntryPatch.Get(Quest(), "steps/7", out error));
            Assert.IsNotNull(EntryPatch.Get(Quest(), "/steps/0/pages/0", out _), "a leading slash is allowed");
        }
    }
}
