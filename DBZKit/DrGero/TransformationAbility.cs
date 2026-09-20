using DrGero.IO;

namespace DrGero.Engine
{
    /// <summary>
    /// A NEW ability that turns the active character straight into a chosen display row (e.g. Goku base -> SSJ God), costing EP, without touching
    /// the stock Transformation ability.
    ///
    /// How it works (all read from the ROM; see Roster-and-Abilities.md):
    ///  * Player_UseAbility calls AbilityDef.isReady(entry) and, if true, AbilityDef.onUse(entry, entity). `entry` is the slot's CharacterEntry
    ///    (+4 currentEp s16, +8 displayDataIndex).
    ///  * The stock Transformation onUse just plays animation 0x25 (0x24 when the row is already "transformed"); which row that animation
    ///    reaches is fixed by the row's own transformed/detransformed links, so it cannot reach a second, different form.
    ///  * BytecodeVM_SetCharacterTransformation (0x0800A8EC, script opcode 73) does the actual switch: pops (slot, spriteId), looks the row up
    ///    by sprite id (CharacterData_FindBySprite, first row with that sprite), stores it in the slot, and refreshes the entity's sprite.
    ///  * The generated onUse subtracts the EP cost, builds a two-entry VM stack {count=2, slot=active character, spriteId} on the Thumb stack
    ///    and calls that handler. isReady is true while currentEp >= cost and the slot is not already in the target row.
    ///  * Reverting: the target row is given flag bit0 ("is a transformed form") and its detransformedIndex points at <c>revertRow</c>, so the
    ///    stock Transformation ability turns it back.
    ///
    /// CERTAINTY: HIGH that the Thumb encodings do what is described (run in an emulator against a stub of the handler: EP, the VM stack, the
    /// preserved registers and the stack pointer all check out), MEDIUM that the game behaves well with it (the handler itself is
    /// unmodified game code, but nothing here has been run in a real emulator).
    /// </summary>
    public static class TransformationAbility
    {
        private const uint PartyStateAddress = 0x03000E90;      // g_PartyState (byte 0 = active character index)
        private const uint SetCharacterTransformation = 0x0800A8ED;   // BytecodeVM_SetCharacterTransformation (Thumb)

        private static byte[] Halfwords(params ushort[] v)
        {
            var b = new byte[v.Length * 2];
            for (int i = 0; i < v.Length; i++) { b[2 * i] = (byte)v[i]; b[2 * i + 1] = (byte)(v[i] >> 8); }
            return b;
        }

        /// <summary>isReady(entry): 1 when currentEp >= cost and displayDataIndex != targetRow, else 0. 28 bytes (24 of code + 1 literal).</summary>
        public static byte[] BuildIsReady(int epCost, int targetRow)
        {
            if (epCost is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(epCost));
            if (targetRow is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(targetRow));
            var code = Halfwords(
                0x2304,                        // movs r3,#4
                0x5EC1,                        // ldrsh r1,[r0,r3]        currentEp
                0x4A04,                        // ldr  r2,[pc,#16]        cost
                0x4291,                        // cmp  r1,r2
                0xDB04,                        // blt  not_ready
                0x7A01,                        // ldrb r1,[r0,#8]         displayDataIndex
                (ushort)(0x2900 | targetRow),  // cmp  r1,#targetRow
                0xD001,                        // beq  not_ready
                0x2001,                        // movs r0,#1
                0x4770,                        // bx   lr
                0x2000,                        // not_ready: movs r0,#0
                0x4770);                       //            bx lr
            return [.. code, .. BitConverter.GetBytes((uint)epCost)];
        }

        /// <summary>onUse(entry, entity): spends the EP and calls BytecodeVM_SetCharacterTransformation(slot = active character, sprite). 60 bytes.</summary>
        public static byte[] BuildOnUse(int epCost, int targetSprite)
        {
            if (epCost is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(epCost));
            if (targetSprite is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(targetSprite));
            var code = Halfwords(
                0xB510,   // push {r4,lr}
                0xB083,   // sub  sp,#12                      fake VM state: [count][stack0][stack1]
                0x4A09,   // ldr  r2,[pc,#36]                 cost
                0x8883,   // ldrh r3,[r0,#4]                  currentEp
                0x1A9B,   // subs r3,r3,r2
                0x8083,   // strh r3,[r0,#4]
                0x2202,   // movs r2,#2
                0x9200,   // str  r2,[sp,#0]                  count = 2
                0x4A07,   // ldr  r2,[pc,#28]                 &g_PartyState
                0x7812,   // ldrb r2,[r2]                     active character index
                0x9201,   // str  r2,[sp,#4]                  stack0 = slot
                0x4A07,   // ldr  r2,[pc,#28]                 sprite id
                0x9202,   // str  r2,[sp,#8]                  stack1 = sprite
                0x4668,   // mov  r0,sp
                0x4B06,   // ldr  r3,[pc,#24]                 handler
                0x467A,   // mov  r2,pc
                0x3205,   // adds r2,#5                       return address (Thumb) = the add sp below
                0x4696,   // mov  lr,r2
                0x4718,   // bx   r3
                0xB003,   // add  sp,#12
                0xBD10,   // pop  {r4,pc}
                0x46C0);  // nop (pool alignment)
            return [.. code, .. BitConverter.GetBytes((uint)epCost), .. BitConverter.GetBytes(PartyStateAddress),
                    .. BitConverter.GetBytes((uint)targetSprite), .. BitConverter.GetBytes(SetCharacterTransformation)];
        }

        /// <summary>
        /// One step for "give Goku a new form": copies display row <paramref name="copyRow"/> (e.g. his SSJ row) into a NEW row whose sprite is a NEW
        /// sprite id (a copy of that row's sprite record, see FrameImport.CloneSprite), flags it as a revertible transformed form, then adds the
        /// ability for it. Recolour / edit the new sprite's frames afterwards in the Sprite editor tab.
        /// </summary>
        public static (Result Ability, int NewRow, int NewSprite) AddWithNewForm(ROM rom, int copyRow, int epCost, int iconsFrom, int revertRow)
        {
            var rows = RosterTables.ReadDisplayRows(rom);
            if (copyRow < 0 || copyRow >= rows.Count) throw new ArgumentOutOfRangeException(nameof(copyRow));
            int newSprite = FrameImport.CloneSprite(rom, rows[copyRow].SpriteId);
            var row = DisplayRow.FromBytes(rows[copyRow].ToBytes());
            row.SpriteId = (byte)newSprite;
            int newRow = RosterTables.AddDisplayRow(rom, row);
            var ability = Add(rom, newRow, epCost, iconsFrom, makeRevertible: true, revertRow);
            return (ability, newRow, newSprite);
        }

        public sealed record Result(int AbilityIndex, uint IsReadyAddress, uint OnUseAddress, string Notes);

        /// <summary>
        /// Adds the ability. <paramref name="iconsFrom"/> is an existing ability whose two HUD icons are shared (icons are 0x200-byte resources;
        /// give the ability its own by editing the icon columns afterwards). With <paramref name="makeRevertible"/> the target row becomes a
        /// "transformed form" whose revert row is <paramref name="revertRow"/>, so the stock Transformation ability undoes it.
        /// </summary>
        public static Result Add(ROM rom, int targetRow, int epCost, int iconsFrom, bool makeRevertible, int revertRow)
        {
            var rows = RosterTables.ReadDisplayRows(rom);
            if (targetRow < 0 || targetRow >= rows.Count) throw new ArgumentOutOfRangeException(nameof(targetRow));
            var target = rows[targetRow];
            if (target.SpriteId == 0) throw new InvalidOperationException("That display row has no sprite id.");
            int first = rows.FindIndex(r => r.SpriteId == target.SpriteId);
            if (first != targetRow)
                throw new InvalidOperationException($"Display row {first} already uses sprite {target.SpriteId}. The game finds a form by sprite id (first match), so the target row needs its own sprite id.");
            var abilities = RosterTables.ReadAbilities(rom);
            if (iconsFrom < 0 || iconsFrom >= abilities.Count) throw new ArgumentOutOfRangeException(nameof(iconsFrom));

            string notes = "";
            if (makeRevertible)
            {
                if (revertRow < 0 || revertRow >= rows.Count) throw new ArgumentOutOfRangeException(nameof(revertRow));
                target.Flags |= 1;
                target.DetransformedIndex = (byte)revertRow;
                RosterTables.WriteDisplayRow(rom, targetRow, target);
                notes = $"Row {targetRow} is now flagged transformed and reverts to row {revertRow} with the stock Transformation ability. ";
            }

            var isReady = rom.AllocateFreeSpace(28);
            rom.WriteBytesAt(isReady, BuildIsReady(epCost, targetRow));
            var onUse = rom.AllocateFreeSpace(60);
            rom.WriteBytesAt(onUse, BuildOnUse(epCost, target.SpriteId));

            var entry = new AbilityEntry
            {
                ReadyIcon = abilities[iconsFrom].ReadyIcon, CoolingIcon = abilities[iconsFrom].CoolingIcon,
                IsReady = 0x08000000u | (uint)isReady | 1, OnUse = 0x08000000u | (uint)onUse | 1,
            };
            int index = RosterTables.AddAbility(rom, entry);
            return new Result(index, entry.IsReady, entry.OnUse, notes + $"Ability {index} spends {epCost} EP and switches to display row {targetRow} (sprite {target.SpriteId}); if you later change that row's sprite id, add the ability again.");
        }
    }
}
