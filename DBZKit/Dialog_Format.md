# Dialog Format Reference

Derived from IDA analysis of `1085 - Dragon Ball Z - The Legacy of Goku II (U)(TrashMan).gba.i64`,
session 2026-09-14. Ground-truthed against real ROM data at `0x083B5FB4` (the YAJIROBE "Senzu Bean
gift" conversation) and cross-checked against `DBZKit/DBZKit/Assets/Dialog.cs`. Uses the opcode
table from `BytecodeVM_OpCodes.md` — read that first if you haven't.

## 1. The three layers

1. **Sequence array** — a flat table of 4-byte ROM pointers, one per line/step of *one*
   conversation. This is what you index to walk a conversation forward or jump around in it.
2. **Dialog entry** — what each pointer in the sequence array points to: a small header
   (`[u16 NextIndex][u8 Mode]`) followed by mode-specific payload (text, a sub-script, or a jump
   target). **The entry itself carries the index of the *next* entry to run** — this is the whole
   "redirect to events" mechanism you asked about.
3. **Bytecode VM script** — the same interpreter documented in `BytecodeVM_OpCodes.md`, reused in
   two places: standalone `DIALOG_SCRIPT` entries (run a game action, no text), and embedded inside
   `DIALOG_TEXT_INTERPOLATE`/`_JCALG1_INTERPOLATE` entries to compute the `%s` arguments for
   `vsprintf_impl`.

## 2. Runtime structs (confirmed via Hex-Rays type info)

```c
struct DialogState {         // 24 bytes total
    void   **sequence;       // +0x00  pointer to the sequence array (see below)
    void    *unk_4;          // +0x04  written back to on natural completion (see Dialog_ProcessNext)
    u16      currentIndex;   // +0x08  index into `sequence`
    u16      gap;            // +0x0A  padding
    Vec      position;       // +0x0C  8 bytes, screen anchor for text boxes
    void    *activeElement;  // +0x14  the in-flight UI/script "element" object (see below)
};

struct DialogEntryText {     // header is 3 bytes; payload follows immediately, mode-dependent
    u16       Index;         // +0x00  NEXT slot to set DialogState.currentIndex to once this entry finishes
    DialogMode mode;         // +0x02  selects the handler (see table below)
    ...                      // +0x03  mode-specific payload, see section 4
};
```

## 3. The sequence array and how chaining actually works (`Dialog_ProcessNext`, `0x800B520`)

```c
// simplified from decompilation
if (state->activeElement) {
    if (BytecodeVM_CallFuncPtr(elem, elem->vtbl[2] /* IsDone/Poll */)) {
        Ptr_InvokeHandler(elem, elem->vtbl[0] /* Destroy */);
        state->activeElement = nullptr;
    }
    return; // still busy, come back next frame
}

for (;;) {
    entry = state->sequence[state->currentIndex];
    if ((entry >> 16) == 0) break;          // sentinel: small value, not a real ROM pointer -> end of conversation

    state->currentIndex = entry->Index;      // <-- THE LINK: bake in the NEXT slot before we even run this one
    elem = Dialog_HandlerTable[entry->mode](state, entry); // create the text box / run the script / etc
    state->activeElement = elem;
    if (BytecodeVM_CallFuncPtr(elem, elem->vtbl[1])) continue; // finished immediately (e.g. a script with no UI) -> loop again
    // otherwise it's a UI element (text box) -> wait for player input, handled by the block above on later frames
}
```

So a "conversation" is:
- one **sequence array** = `DialogEntryText* sequence[]`, terminated by a sentinel pointer whose
  top 16 bits are 0 (i.e. not a real `0x08xxxxxx` ROM address — `0x0000` works, so does any small
  placeholder),
- walked by `currentIndex`, which is **not** auto-incremented — it's set explicitly from the
  `Index` field baked into the entry you just ran,
- each entry can point to any slot (forward, backward, skip) — this is how branches/loops in
  dialog are authored: you don't need contiguous entries, you just need to know your own next slot
  number when you write the entry.

`Dialog_HandlerTable` (`0x83EC84C`, 6× 4-byte thumb funcptrs) maps `mode` 0–5 directly onto:

| Mode | Enum | Handler |
|---|---|---|
| 0 | `DIALOG_SCRIPT` | `Dialog_CreateScriptedElement` (`0x800B428`) |
| 1 | `DIALOG_TEXT` | `Dialog_CreateText` (`0x800B438`) |
| 2 | `DIALOG_TEXT_JCALG1` | `Dialog_CreateTextFromCompressed` (`0x800B474`) |
| 3 | `DIALOG_TEXT_INTERPOLATE` | `Dialog_CreateInterpolatedTextBox` (`0x800B464`) |
| 4 | `DIALOG_TEXT_JCALG1_INTERPOLATE` | `Dialog_CreateInterpolatedTextFromCompressed` (`0x800B484`) |
| 5 | `DIALOG_JUMP` | `Dialog_CreateJump` (`0x800B494`) |

confirmed 1:1 against `DialogMode` in `Dialog.cs` — no reordering, no gaps.

## 4. Entry payload layout per mode (byte-exact, ROM-verified)

All offsets below are relative to the entry's own address (where its pointer in `sequence[]`
points).

### Mode 0 — `DIALOG_SCRIPT` (no text, runs a bytecode script, no text box created)
```
+0x00 u16 NextIndex
+0x02 u8  mode = 0
+0x03 ... bytecode stream (BytecodeVM_MainDispatchTable ops), terminated by 0x11
```
`Dialog_CreateScriptedElement_Impl` runs this with a **fresh VM stack** (`stackPtr=0`), starting at
`entry+3`. If exactly one value is left on the stack at `END`, it's written back through a
caller-supplied output pointer (`*(elementPtr+1)`) — a way for a script entry to hand a computed
value back to whatever's using the dialog element (not needed for a plain "run this side effect"
script). Ends **immediately** (`vtbl[1]` returns true the same frame), so `Dialog_ProcessNext`'s
loop falls straight through to the next entry without waiting for player input — perfect for
"grant item, then show a textbox" chains.

**Real example** (`0x083B6010`, 8 bytes total): `02 00 | 00 | 00 00 02 1A 11`
→ `Index=2`, `mode=SCRIPT`, script = `PushByte(0); Step(0x1A); END`.
`Step` opcode `0x1A` (26) resolves to `BytecodeVM_PickUpItem` (`0x080096AA`), which pops the item
index and calls `g_ItemsInGame[idx].OnItemPickUp` — i.e. **this is the "give the player an item"
event**, invoked as a plain dialog-script entry, then chained (via `Index=2`) into the very next
sequence slot, which is the text box announcing it. This is the exact mechanism you were asking
about for wiring dialog to game events.

### Mode 1 — `DIALOG_TEXT` (plain text box)
```
+0x00 u16 NextIndex
+0x02 u8  mode = 1
+0x03 u8  CharacterId       (0 = narrator/no portrait; else selects portrait + box style, see Dialog_CreateTextBox)
+0x04 ... charmap text, NUL-terminated
```
**Real example** (`0x083B6018`): `03 00 | 01 | 00 | 5E 59 6F 75 ...` → `Index=3`, `CharacterId=0`,
text starts with a `0x5E` control byte then plain ASCII `"You received a Senzu Bean!"`. The leading
`0x5E` is **not** documented yet — `Dialog_CreateTextBox` only special-cases ASCII `!`/`@`/`#`
prefixes (box vertical position), so `0x5E` is something in this charmap's own control-code range;
treat text bytes ≥ 0x80 or unprintable-ASCII as codes, not literal chars, until you've walked
`Dialog_CreateTextBox`'s font/glyph table to confirm.

### Mode 2 — `DIALOG_TEXT_JCALG1` (jcalg1-compressed plain text)
```
+0x00 u16 NextIndex
+0x02 u8  mode = 2
+0x03 u8  CharacterId
+0x04 ... jcalg1 compressed blob, decompressed straight into a text box (no interpolation)
```
`Dialog_CreateTextFromCompressed_Impl` (`0x800B304`) calls `j_jcalg1_iwram_dst(dst, entry+4)`
directly into the text-box buffer, no `vsprintf_impl` pass — this is why the format has no format
string pointer.

### Mode 3 — `DIALOG_TEXT_INTERPOLATE` (text with `%s`/`%d` args computed by a bytecode script)
```
+0x00 u16 NextIndex
+0x02 u8  mode = 3
+0x03 u8  CharacterId
+0x04 u32 FormatStringPtr        (absolute GBA address of the printf-style format string)
+0x08 ... bytecode script, terminated by 0x11, pushes the vsprintf args in order
[FormatStringPtr target, wherever it physically sits — usually right after the script, 4-byte aligned]
```
This fixes the bug already flagged in `Dialog.cs`: the "ScriptData" field there is **not** a fixed
4 bytes — it's a real variable-length script starting at `entry+8`, executed with a fresh VM stack,
whose final stack contents become the varargs for `vsprintf_impl(dst, *(entry+4), &stack)`.

**Real example** (`0x083B5FB4`, the entry you originally pointed at):
```
+0x00: 01 00            Index = 1  (next: sequence[1] = the PickUpItem script above)
+0x02: 03               mode = TEXT_INTERPOLATE
+0x03: 71               CharacterId = 0x71 (portrait/speaker id — YAJIROBE)
+0x04: C0 5F 3B 08       FormatStringPtr = 0x083B5FC0 (points at the text immediately below)
+0x08: 02 0C 11          script: Step(0x0C) ; END
+0x0B: 00                (1-byte pad to 4-align entry length: header(11) rounds to 12)
+0x0C: "YAJIROBE: What's up, %s?  Korin sent me over here to give you a little gift!\0"
```
`Step(0x0C)` = `BytecodeVM_OpcodeTable[0x0C]`, already named `PushActiveCharName` — pushes the
active party member's name, which lands in `%s`. After the whole entry (header + script + text,
4-byte aligned) comes the next `sequence[]` target directly in the data blob, but note **the entry
does NOT have to be physically adjacent to what `sequence[1]` points to** — here it happens to
chain into the very next bytes (`0x083B6010`) because whoever authored the conversation laid it out
linearly, not because the format requires it.

### Mode 4 — `DIALOG_TEXT_JCALG1_INTERPOLATE`
```
+0x00 u16 NextIndex
+0x02 u8  mode = 4
+0x03 u8  CharacterId
+0x04 u32 CompressedTextPtr        (jcalg1 blob elsewhere, decompressed into a 2048-byte stack buffer)
+0x08 ... bytecode script (fresh VM stack) providing vsprintf args, terminated by 0x11
```
(`Dialog_CreateInterpolatedTextFromCompressed_Impl`, `0x800B396`): decompresses
`j_jcalg1_iwram_dst(buf, entry+8)` **first** — i.e. here the script pointer field at `+4` is
consumed as the format string source via `*(entry+4)` post-decompress, and the raw script bytes
start right at `entry+8` same as mode 3, just with the format text itself compressed instead of
plain. Same VM-script-computes-args pattern as mode 3.

### Mode 5 — `DIALOG_JUMP`
```
+0x00 u16 NextIndex   (unused by the jump itself — set for whatever runs after the jump element finishes, if anything)
+0x02 u8  mode = 5
+0x03 u8  pad
+0x04 u32 target
```
`Dialog_CreateJump` just stashes `target` into a tiny heap object with a distinct vtable
(`0x08024AA0`) and returns it as `activeElement` — the actual redirection behavior lives in that
vtable's poll function, which wasn't fully traced this session (my address resolution for its
relative-offset vtable landed on unrelated code — needs another pass with `read_struct`/manual
offset math rather than guessing). **What's confirmed:** it's a 4-byte absolute value at `entry+4`,
not 2 bytes at `entry+3` like `Dialog.cs` currently assumes (already flagged as a bug via an IDB
comment on `Dialog_CreateJump` itself). Treat mode 5 as "not yet safe to author against" until that
vtable is traced — everything in modes 0/1/3 above is solid enough to build on right now.

## 5. What you need to write your own dialog + a custom "event" opcode

1. Build a `sequence[]` array of ROM (or, for a rebuild, wherever you load your patch data) pointers
   to your entries, ending in a sentinel whose top 16 bits are 0.
2. Write each entry as `[u16 NextIndex][u8 Mode][payload]`, using mode 1 for plain lines and mode 0
   for "run an effect, no textbox" steps (this is your `event` hook point) — chain them purely via
   the `Index` field, not by physical order.
3. For a *new* event/action (not just calling `PickUpItem`), add an entry to
   `BytecodeVM_OpcodeTable` (`0x83B5D28`, currently ~146 slots) pointing at your new handler
   function, following the calling convention already reverse-engineered in
   `BytecodeVM_OpCodes.md` (handler pops its own args off the shared VM stack via
   `*stackPtr - 1`, no return value needed unless you want to push one back for a script entry to
   read). A mode-0 script entry of `PushByte(args...); Step(yourNewOpcode); END` is then a fully
   working "trigger my custom event from dialog" node, chaining into whatever text/entry comes
   next via its `Index` field — exactly like the Senzu Bean example above.
4. If you need branching dialog (player choices), opcode #31 in `BytecodeVM_OpcodeTable`
   (`sub_800977A`) was once guessed to be a yes/no prompt (`ShowChoicePrompt`), but an in-game test shows it is a camera pan (`PanCameraToPosition`), so it is NOT a choice mechanism. If you find a real choice mechanism, pair it with
   `JumpIfFalse` (`0x13`, 1 signed byte, VM-internal jump — not the same thing as mode-5
   `DIALOG_JUMP`, which operates at the entry level) inside a mode-0 script entry to pick which
   `Index` gets set based on the player's answer, or just have that script push a different pending
   Index by whatever means you extend the VM with.

## Open items for a follow-up pass
- Trace `Dialog_CreateJump`'s poll vtable (`0x08024AA0`) properly to confirm whether `target` is a
  raw `currentIndex` value, a pointer to a *different* `sequence[]` array, or something else.
- Confirm the `0x5E` control byte seen at the start of mode-1 charmap text — likely a sound/portrait
  cue, not a literal character.
- `CharacterId` byte range and its mapping to actual portraits/speaker names beyond the ranges
  already handled in `Dialog_CreateTextBox`'s branching (0, 1, 2, 26/28-32/38/39, 40, 49/50/53,
  70, 84/86/96) would help you pick a safe custom ID that doesn't collide.
