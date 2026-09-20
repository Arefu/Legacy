# Editing Triggers, Scripts, and Dialogue in Dragon Radar

What's now possible from the Properties panel when you select a trigger on the
map, and what's actually confirmed behind it.

## What's editable

Select a trigger, and the Properties panel shows up to four buttons:

- **Edit Script...** -- decode the trigger's current payload as plain VM
  bytecode (using the real Zenkai IDE editor: highlighting, autocomplete,
  `var`/`if`/`else`), edit it, compile, and write it back.
- **Edit Dialogue...** -- same trigger, but authored as a conversation
  (plain text lines, `#script:` lines for opcode steps) instead of raw
  bytecode.
- **Duplicate Trigger (New)** -- clone the selected trigger into a brand new
  trigger on the same map.
- **Delete Trigger** -- remove it.

All of these write directly into the currently-loaded ROM in memory (no
"saved!" popup -- it just does it), the same way every other edit in Dragon
Radar works. Use File -> Save ROM As... to write the result out to a file.

## The trigger record, confirmed via IDA

IDA already had these fully typed from earlier work (`search_structs` /
`type_inspect`, 2026-09-19) -- four trigger variants, all sharing the same
leading shape:

```
MapTriggerDialog  (20 bytes)      MapTriggerScript  (20 bytes)
  condition   void*  +0x00          condition   void*  +0x00
  function    void*  +0x04          function    void*  +0x04
  x1          int16  +0x08          x1          int16  +0x08
  y1          int16  +0x0A          y1          int16  +0x0A
  x2          int16  +0x0C          x2          int16  +0x0C
  y2          int16  +0x0E          y2          int16  +0x0E
  dialogArray void*  +0x10          scriptEntry void*  +0x10

MapTriggerSavePoint (16 bytes)    MapTriggerDualRect (24 bytes)
  condition/function/x1/y1/x2/y2     condition/function/x1/y1/x2/y2
  (no +0x10 field)                   + 4 more int16 fields at +0x10..+0x16
```

`condition` and `function` are both **function pointers**, not data --
confirmed via `call_ctor` (`0x8021D28`), which is a one-line thunk: `a1(a1)`,
i.e. it just calls whatever `function` points to. This is why the editor
clones a trigger's `condition`/`function` bytes **verbatim** when duplicating
rather than trying to build new ones: they're executable code, and reusing an
already-working pair is both simpler and safer than synthesizing new ones from
a guess.

The outer array each of these lives inside (`MapEntry.mapTriggers[]`) is a
flat list of 8-byte `{vTable, result}` pairs (`MapTriggerEntry`) -- `result`
is the pointer to one of the structs above.

## Duplicate / Delete: how they actually work

**Duplicate**: reads the selected trigger's `{vTable, dataPtr}` slot, clones
24 bytes from `dataPtr` (the largest confirmed variant, so it's a safe
superset regardless of which variant this particular trigger actually is) to
free space, appends a new `{vTable, newDataPtr}` entry to the map's
`mapTriggers[]` array (also rebuilt in free space, one slot bigger), and
patches the map's own `TriggerCount` byte and `MapTriggers` pointer. The new
trigger starts as an exact copy in the same spot -- move it and edit its
script/dialogue afterward.

**Delete**: rebuilds `mapTriggers[]` one slot shorter (excluding the deleted
trigger), and decrements `TriggerCount`. The old array and the deleted
trigger's own record bytes are left as unreachable data in the ROM -- nothing
in this codebase reclaims freed space, so this isn't different from every
other "delete" already in the project.

**What this does NOT do**: build a trigger from nothing. There's no code here
that invents a `condition`/`function` pair -- every new trigger is a clone of
one that already works. If you need a trigger with genuinely different
condition logic, that's further RE work (tracing what `condition` functions
actually check) not yet done.

## Scripts: what Edit Script does

Unchanged from before -- decodes the current payload with
`ZenkaiDisassembler`, lets you edit/write real Zenkai source (including
`var`/`if`/`else`), compiles with `ZenkaiAssembler`, writes the bytes to free
space, and repoints the trigger's payload field at them.

## Dialogue: the line format

`Edit Dialogue...` uses a plain-text format, one line per dialog entry:

```
Do you have a Senzu Bean for me?
#script: PickUpItem(5)
Thanks! That should help.
[42] A line spoken by CharacterId 42.
```

- A plain line becomes a mode-1 `DIALOG_TEXT` entry (CharacterId 0).
- `[N] text` sets CharacterId to N.
- `#script: Call(args)` becomes a mode-0 `DIALOG_SCRIPT` entry -- runs that
  one opcode call with no text box, exactly like the real Senzu Bean
  conversation's `PickUpItem` step (see `Dialog_Format.md`).
- Blank lines are just spacing in the editor and don't become an entry.

Entries are chained in the order written (`NextIndex` = next array slot),
ending in the sentinel `Dialog_ProcessNext` checks for.

**What this does NOT do**: modes 2/3/4 (compressed and/or `%s`-interpolated
text) or mode 5 (jump). If an existing conversation uses one of those, Edit
Dialogue reads as far as it can, tells you exactly where it stopped and why,
and warns that saving from there will replace the rest. This mirrors
`Dialog_Format.md`'s own note that mode 5 in particular was never fully
traced (its poll vtable wasn't resolved) -- authoring against it would be
guessing, not building.

## Code

- `DrGero/Dialog.cs` -- `DialogReader`/`DialogWriter`/`DialogLine`.
- `Dragon Radar/ScriptEditorForm.cs`, `DialogEditorForm.cs` -- the two editor
  dialogs.
- `Dragon Radar/DragonRadarUI.cs` -- `editScriptButton_Click`,
  `editDialogueButton_Click`, `duplicateTriggerButton_Click`,
  `deleteTriggerButton_Click`, `ResolveTriggerScriptPayload`,
  `CurrentMapEntryAddress`.
