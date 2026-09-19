namespace Legacy.Zenkai
{
    /// <summary>
    /// Round-trip proof harness. Run via `Legacy.exe --test-zenkai`. Not a real
    /// xunit/nunit suite (none exists yet in this solution) -- console-checked like
    /// DBZKit/RenderTest/Program.cs.
    /// </summary>
    internal static class ZenkaiRoundTripTests
    {
        // Intro Sequence Script #1, ROM address 0x083B827B, from
        // Legacy.wiki/Zenkai/Zenkai-Example-Scripts.md.
        private static readonly byte[] Script1 = new byte[]
        {
            0x1D, 5, 2, 6, 2, 0x81, 0x70, 0x81, 0x50, 2, 0x23, 0, 0xB4, 2, 0x45, 0, 0xF0, 2, 0x28, 2, 0x2D, 0, 0x17, 2, 0x31, 0x11
        };

        // Intro Sequence Script #2, ROM address 0x083B831B, same source. Exercises
        // PushMultiVarint with negative-looking large values and multiple opcodes.
        private static readonly byte[] Script2 = new byte[]
        {
            0, 2, 2, 0x18, 0, 0x78, 2, 0x2A, 0x1D, 2, 6, 0xC, 2, 0x62, 0, 3, 2, 0x46,
            0x1D, 5, 2, 2, 2, 0x83, 0x10, 0x8A, 0x30, 2, 0x27, 0, 0x78, 2, 0x2B, 0x11
        };

        public static void RunAll()
        {
            int pass = 0, fail = 0;

            void Check(string name, bool ok, string detail)
            {
                Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}{(ok ? "" : " -- " + detail)}");
                if (ok) pass++; else fail++;
            }

            RoundTrip("Script1 (Intro Sequence #1)", Script1, Check);
            RoundTrip("Script2 (Intro Sequence #2)", Script2, Check);

            // Explicit label/Jump round trip not covered by the two real scripts above
            // (neither uses Jump/JumpIfFalse) -- synthetic test, forward and backward.
            TestJumpRoundTrip(Check);

            // The Math-Book-style "have it? take it : give it" shape, and a bare
            // quest-condition script -- disassembly must read like source (no temp vars,
            // no raw-opcode comments) AND reassemble byte-identical.
            TestReadableRoundTrip("if/else with inline compare", @"
if (StackGetItemCount(0) == 1)
    RemoveItem(0);
else
    PickUpItem(1);
", Check);
            TestReadableRoundTrip("bare flag condition (script result)", @"
StackTestStoryFlag(191); // script result
", Check);
            // Empty else (Jump over nothing) must not print an `else { }` block.
            {
                byte[] b = ZenkaiAssembler.Assemble("if (StackTestStoryFlag(1)) SetEntityFacing(0, 1); else { }");
                string t = ZenkaiDisassembler.Disassemble(b);
                Check("empty else is omitted", !t.Contains("else"), t);
            }
            // The reported shape: an if, then a bare literal left on the stack at END.
            TestReadableRoundTrip("return literal after if", @"
if (StackTestStoryFlag(1))
    SetEntityFacing(0, 1);
return 2;
", Check);
            // `;` is optional on return; a bare `return` followed by a call on the next line
            // must not swallow the call's name as a returned variable.
            {
                bool same = ZenkaiAssembler.Assemble("return 2").AsSpan().SequenceEqual(ZenkaiAssembler.Assemble("return 2;"));
                Check("return: semicolon is optional", same, "bytes differ");

                byte[] bare = ZenkaiAssembler.Assemble("return\nPlayMusic(1);");
                Check("return: bare return before a call", bare.Length > 0 && bare[0] == 0x11 && bare.Length > 2, string.Join(" ", bare.Select(b => b.ToString("X2"))));
            }
            TestReadableRoundTrip("if on flag test", @"
if (StackTestStoryFlag(4))
    SetStoryFlag(2);
", Check);

            Console.WriteLine();
            Console.WriteLine($"{pass} passed, {fail} failed.");
        }

        private static void RoundTrip(string name, byte[] original, Action<string, bool, string> check)
        {
            string text;
            try
            {
                text = ZenkaiDisassembler.Disassemble(original);
            }
            catch (Exception ex)
            {
                check($"{name}: disassemble", false, ex.Message);
                return;
            }

            Console.WriteLine($"--- {name} disassembly ---");
            Console.WriteLine(text);

            byte[] reassembled;
            try
            {
                reassembled = ZenkaiAssembler.Assemble(text);
            }
            catch (Exception ex)
            {
                check($"{name}: reassemble", false, ex.Message);
                return;
            }

            bool same = original.AsSpan().SequenceEqual(reassembled);
            check($"{name}: byte-identical round trip", same,
                same ? "" : $"original=[{string.Join(" ", original.Select(b => b.ToString("X2")))}] reassembled=[{string.Join(" ", reassembled.Select(b => b.ToString("X2")))}]");
        }

        private static void TestReadableRoundTrip(string name, string source, Action<string, bool, string> check)
        {
            try
            {
                byte[] bytes = ZenkaiAssembler.Assemble(source);
                string text = ZenkaiDisassembler.Disassemble(bytes);
                Console.WriteLine($"--- {name} ---");
                Console.WriteLine(text);

                static string Norm(string t) => string.Concat(t.Where(c => !char.IsWhiteSpace(c)));
                check($"{name}: disassembles to the original source", Norm(text) == Norm(source), $"got: {text}");
                check($"{name}: byte-identical round trip", ZenkaiAssembler.Assemble(text).AsSpan().SequenceEqual(bytes), "bytes differ");
            }
            catch (Exception ex)
            {
                check($"{name}", false, ex.Message);
            }
        }

        private static void TestJumpRoundTrip(Action<string, bool, string> check)
        {
            // Forward jump over a PlayMusic call, then a backward LoopOrJump back to
            // the top -- proves label resolution works in both directions.
            string source = @"
start:
    PlayMusic(1);
    Jump(skip);
    PlayMusic(2);
skip:
    PlayMusic(3);
    LoopOrJump(start);
";
            byte[] bytes;
            try
            {
                bytes = ZenkaiAssembler.Assemble(source);
            }
            catch (Exception ex)
            {
                check("Jump/LoopOrJump: assemble", false, ex.Message);
                return;
            }

            Console.WriteLine("--- Jump/LoopOrJump synthetic assembly ---");
            Console.WriteLine(string.Join(" ", bytes.Select(b => b.ToString("X2"))));

            string redis;
            try
            {
                redis = ZenkaiDisassembler.Disassemble(bytes);
            }
            catch (Exception ex)
            {
                check("Jump/LoopOrJump: disassemble", false, ex.Message);
                return;
            }
            Console.WriteLine(redis);

            byte[] reassembled;
            try
            {
                reassembled = ZenkaiAssembler.Assemble(redis);
            }
            catch (Exception ex)
            {
                check("Jump/LoopOrJump: reassemble", false, ex.Message);
                return;
            }

            bool same = bytes.AsSpan().SequenceEqual(reassembled);
            check("Jump/LoopOrJump: byte-identical round trip", same,
                same ? "" : $"first=[{string.Join(" ", bytes.Select(b => b.ToString("X2")))}] second=[{string.Join(" ", reassembled.Select(b => b.ToString("X2")))}]");
        }
    }
}
